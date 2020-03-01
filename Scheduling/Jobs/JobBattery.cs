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
    class JobBattery
    {
        public string ID = "Battery";
        public bool Updating { get; private set; } = true;
        public float ChargeRatio { get { return Charge.GetRatio(); } }
        public Ratio Charge = new Ratio();
        public Ratio Input = new Ratio();
        public Ratio Output = new Ratio();
        public IReadOnlyList<BatteryCache> Batteries;

        Scheduler Owner;
        GridPolicy Policy;
        string Tick;
        public JobBattery(Scheduler owner, GridPolicy policy, string tick)
        {
            Owner = owner;
            Owner.BlockFound += BlockFound;
            Batteries = Cache;
            Policy = policy;
            Tick = tick;
            Owner.Subscribe(UpdateBlock, tick);
        }

        public JobBattery(Scheduler owner)
        {
            Owner = owner;
            Owner.BlockFound += BlockFound;
            Owner.Loading += Load;
            Owner.Saving += Save;
            Batteries = Cache;
            Policy = GridPolicy.Types.SameConstruct;
            Tick = "update100s";
        }

        public void SetThreshold(double low, double high, Action<bool> handler) { Thresholds.Add(new MyTuple<double, double, Action<bool>>(low, high, handler)); }

        void Save(MyIni state)
        {
            state.Set(ID, "Policy", Policy.ToString());
            state.Set(ID, "Update", Tick);
        }

        void Load(MyIni state)
        {
            if (!GridPolicy.TryParse(state.Get(ID, "Policy").ToString(), out Policy))
                Policy = GridPolicy.Types.SameConstruct;
            Owner.Unsubscribe(UpdateBlock, Tick);
            Tick = Owner.Subscribe(UpdateBlock, state.Get(ID, "Update").ToString(), "update100s");
        }

        #region Block updates
        List<BatteryCache> Cache = new List<BatteryCache>();
        List<MyTuple<double, double, Action<bool>>> Thresholds = new List<MyTuple<double, double, Action<bool>>>();
        float _PreviousRatio;
        int Index;

        void BlockFound(GridScanArgs<IMyTerminalBlock> item)
        {
            if (item.First)
            {
                Updating = true;
                Charge.Current = Charge.Total = 0;
                Input.Current = Input.Total = 0;
                Output.Current = Output.Total = 0;
                Cache.Clear();
            }
            var b = item.Item as IMyBatteryBlock;
            if (b != null && Owner.PolicyCheck(Policy, item.Item))
            {
                BatteryCache cache = new BatteryCache(b);
                cache.Update();
                Cache.Add(cache);
                Input.Add(cache.Input);
                Output.Add(cache.Output);
                Charge.Add(cache.Charge);
            }
            if (item.Last)
            {
                _PreviousRatio = Charge.GetRatio();
                Index = Cache.Count - 1;
                Updating = false;
            }
        }

        void UpdateBlock(UpdateFrequency freq)
        {
            if (Updating) return;
            _PreviousRatio = Charge.GetRatio();
            for (int j = 0; j < 100; j++)
            {
                Charge.Subtract(Cache[Index].Charge);
                Input.Subtract(Cache[Index].Input);
                Output.Subtract(Cache[Index].Output);
                if (!Cache[Index].Battery.IsAlive())
                    Cache.RemoveAt(Index);
                else
                {
                    Cache[Index].Update();
                    Charge.Add(Cache[Index].Charge);
                    Input.Add(Cache[Index].Input);
                    Output.Add(Cache[Index].Output);
                }
                Index--;
                if (Index < 0)
                {
                    Index = Cache.Count - 1;
                    break;
                }
            }
            if (Charge.Total > 0)
            {
                float ratio = Charge.GetRatio();
                foreach (var item in Thresholds)
                    if (_PreviousRatio > item.Item1 && item.Item1 >= ratio)
                        item.Item3(false);
                    else if (_PreviousRatio < item.Item2 && item.Item2 <= ratio)
                        item.Item3(true);
            }
        }
        public struct BatteryCache
        {
            public readonly IMyBatteryBlock Battery;
            public Ratio Charge, Input, Output;
            public BatteryCache(IMyBatteryBlock b)
            {
                Battery = b;
                Charge = new Ratio();
                Input = new Ratio();
                Output = new Ratio();
            }
            public void Update()
            {
                Charge.Current = Battery.CurrentStoredPower;
                Charge.Total = Battery.MaxStoredPower;
                Input.Current = Battery.CurrentInput;
                Input.Total = (Battery.ChargeMode == ChargeMode.Discharge) ? 0 : Battery.MaxInput;
                Output.Current = Battery.CurrentOutput;
                Output.Total = (Battery.ChargeMode == ChargeMode.Recharge) ? 0 : Battery.MaxOutput;
            }
        }
        #endregion
    }
}
