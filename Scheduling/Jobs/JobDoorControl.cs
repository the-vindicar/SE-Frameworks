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
    class JobDoorControl
    {
        public string ID = "Door";
        public string Category = "DoorControl";
        public int Slice = 50;
        bool LoadConfig;
        Scheduler Owner;
        GridPolicy Policy;

        public JobDoorControl(Scheduler owner, GridPolicy policy)
        {
            Owner = owner;
            Policy = policy;
            Owner.BlockFound += BlockFound;
            Owner.Tick10 += Update;
            Owner.Loading += Load;
            Owner.Saving += Save;
            LoadConfig = false;
        }
        public JobDoorControl(Scheduler owner) : this(owner, GridPolicy.Types.SameGrid)
        {
            LoadConfig = true;
        }
        #region Save/Load
        void Load(MyIni state)
        {
            if (LoadConfig)
            {
                Slice = state.Get(ID, "Slice").ToInt32(50);
                if (!GridPolicy.TryParse(state.Get(ID, "Policy").ToString(), out Policy))
                    Policy = GridPolicy.Types.SameGrid;
            }
            List<MyIniKey> keys = new List<MyIniKey>();
            state.GetKeys(ID+".Doors", keys);
            long eid;
            foreach (var key in keys)
                if (long.TryParse(key.Name, out eid))
                    Intervals[eid] = Math.Max(0, state.Get(key).ToDouble(0));
        }

        void Save(MyIni state)
        {
            if (LoadConfig)
            {
                state.Set(ID, "Slice", Slice);
                state.Set(ID, "Policy", Policy.ToString());
            }
            string cat = ID + ".Doors";
            foreach (var d in Doors)
                state.Set(cat, d.Door.EntityId.ToString(), d.Interval);
        }
        #endregion
        List<DoorState> Doors = new List<DoorState>();
        Dictionary<long, double> Intervals = new Dictionary<long, double>();
        MyIni parser = new MyIni();
        bool Updating = true;

        void BlockFound(GridScanArgs<IMyTerminalBlock> item)
        {
            if (item.First)
            {
                Updating = true;
                foreach (var d in Doors)
                    Intervals[d.Door.EntityId] = d.Interval;
                Doors.Clear();
            }
            if (item.Item is IMyDoor 
                && Owner.PolicyCheck(Policy, item.Item)
                && !string.IsNullOrWhiteSpace(item.Item.CustomData)
                && parser.TryParse(item.Item.CustomData, Category))
            {
                var d = new DoorState(item.Item as IMyDoor, 
                    parser.Get(Category, "Opposite").ToString(null),
                    parser.Get(Category, "Timeout").ToDouble(0));
                if (!Intervals.TryGetValue(d.Door.EntityId, out d.Interval))
                    d.Interval = 0;
                Doors.Add(d);
            }
            if (item.Last)
            {
                Intervals.Clear();
                foreach (var d in Doors)
                    d.Opposite = Doors.Find((other) => string.Equals(d.OppositeDoorName, other.Door.CustomName, StringComparison.CurrentCultureIgnoreCase))?.Door;
                UpdateIdx = Doors.Count - 1;
                Updating = false;
            }
        }

        int UpdateIdx;
        void Update(UpdateFrequency freq)
        {
            if (Updating) return;
            for (int i = 0; (UpdateIdx >= 0) && (i < Slice); i++)
            {
                if (Doors[UpdateIdx].Door.IsAlive())
                    Doors[UpdateIdx].Update(Owner.PB.Runtime.TimeSinceLastRun);
                else
                    Doors.RemoveAt(UpdateIdx);
                UpdateIdx--;
            }
            if (UpdateIdx < 0)
                UpdateIdx = Doors.Count - 1;
        }

        class DoorState
        {
            public IMyDoor Door = null;
            public string OppositeDoorName = null;
            public IMyDoor Opposite = null;
            public double Timeout = 0;
            public double Interval = 0;
            public DoorState(IMyDoor door, string opposite, double timeout)
            {
                Door = door;
                OppositeDoorName = opposite;
                Timeout = timeout;
            }
            public void Update(TimeSpan span)
            {
                if (Timeout > 0 && Door.Status == DoorStatus.Open)
                {
                    Interval += span.TotalSeconds;
                    if (Interval >= Timeout)
                        Door.CloseDoor();
                }
                else
                    Interval = 0;
                if (Opposite != null)
                {
                    if (Opposite.IsFunctional && Opposite.Status != DoorStatus.Closed)
                    {
                        if (Door.Enabled = (Door.Status != DoorStatus.Closed))
                            Door.CloseDoor();
                    }
                    else
                        Door.Enabled = true;
                }
            }
        }
    }
}
