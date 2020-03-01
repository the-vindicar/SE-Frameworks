using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    class JobSolarSimple
    {
        public string ID = "Solar";
        Scheduler Owner;
        GridPolicy Policy;
        string Tick;
        float Velocity;
        public JobSolarSimple(Scheduler owner, GridPolicy policy, string tick, float velocity, int every = 3)
        {
            Owner = owner;
            Policy = policy;
            Tick = Owner.Subscribe(Update, tick);
            Velocity = velocity;
            Owner.Fault += Fault;
            Owner.GroupFound += GroupFound;
            Farms = new List<Farm>[every];
            for (int i = 0; i < Farms.Length; i++) Farms[i] = new List<Farm>();
        }

        public JobSolarSimple(Scheduler owner)
        {
            Owner = owner;
            Policy = new GridPolicy(GridPolicy.Types.SameConstruct | GridPolicy.Types.GroupTag, "Solar");
            Tick = "update100";
            Owner.Fault += Fault;
            Owner.GroupFound += GroupFound;
            Owner.Loading += Load;
            Owner.Saving += Save;
        }
        #region Load/Save
        void Load(MyIni state)
        {
            if (!GridPolicy.TryParse(state.Get(ID, "Policy").ToString(), out Policy))
                Policy = new GridPolicy(GridPolicy.Types.SameConstruct | GridPolicy.Types.GroupTag, "Solar");
            Tick = Owner.Subscribe(Update, state.Get(ID, "Update").ToString(), "update100");
            Velocity = (float)state.Get(ID, "VelocityRad").ToDouble(0.04);
            Farms = new List<Farm>[state.Get(ID, "UpdateMultiplier").ToInt32(3)];
            for (int i = 0; i < Farms.Length; i++) Farms[i] = new List<Farm>();
        }
        void Save(MyIni state)
        {
            state.Set(ID, "Policy", Policy.ToString());
            state.Set(ID, "Update", Tick);
            state.Set(ID, "VelocityRad", Velocity);
            state.Set(ID, "UpdateMultiplier", Farms.Length);
        }
        #endregion
        #region Blocks
        bool Updating = true;
        class Farm
        {
            public IMyMotorStator Motor;
            public List<IMySolarPanel> Panels;
            public int Direction;
            public float Previous;
            public float Last;
            public Farm(IMyMotorStator m, List<IMySolarPanel> ps)
            {
                Motor = m;
                Panels = ps;
                Previous = Panels.Sum((p) => p.IsWorking ? p.MaxOutput : 0);
                Direction = 1;
            }
            public void Update(float velocity)
            {
                Previous = Last;
                Last = Panels.Sum((p) => p.IsWorking ? p.MaxOutput : 0);
                if (Last < Previous) Direction *= -1;
                Motor.TargetVelocityRad = Direction * velocity;
            }
            public static bool UsefulRotor(IMyTerminalBlock b)
            {
                var r = b as IMyMotorStator;
                if (r?.RotorLock ?? true)
                    return false;
                else
                    return r.Torque > 0;
            }
        }
        List<Farm>[] Farms;
        void GroupFound(GridScanArgs<KeyValuePair<IMyBlockGroup, List<IMyTerminalBlock>>> item)
        {
            if (item.First)
            {
                foreach (var fl in Farms) fl.Clear();
                Updating = true;
                UpdateIdx = Farms.Length - 1;
            }
            if (Owner.PolicyCheck(Policy, item.Item.Key))
            {
                IMyMotorStator rotor = item.Item.Value.Find(Farm.UsefulRotor) as IMyMotorStator;
                List<IMySolarPanel> panels = new List<IMySolarPanel>();
                foreach (var b in item.Item.Value)
                    if ((b as IMySolarPanel)?.IsWorking ?? false)
                        panels.Add(b as IMySolarPanel);
                if (rotor != null && panels.Count > 0)
                {
                    Farms[UpdateIdx].Add(new Farm(rotor, panels));
                    if (--UpdateIdx < 0) UpdateIdx = Farms.Length - 1;
                }
            }
            if (item.Last)
            {
                UpdateIdx = Farms.Length - 1;
                Updating = false;
            }
        }
        #endregion
        #region Updates
        int UpdateIdx;
        void Update(UpdateFrequency freq)
        {
            if (Updating) return;
            foreach (var f in Farms[UpdateIdx]) f.Update(0.04f);
            if (--UpdateIdx < 0)
                UpdateIdx = Farms.Length - 1;
        }

        void Fault(Exception err)
        {
            foreach (var fl in Farms)
                foreach (var f in fl)
                    f.Motor.TargetVelocityRad = 0;
        }
        #endregion
    }
}
