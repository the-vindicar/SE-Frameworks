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
    class JobSmartAirlock : IHasOutput
    {
        public string ID { get; set; } = "Airlock";
        public IReadOnlyDictionary<IMyBlockGroup, Airlock> Airlocks;
        Scheduler Owner;
        string Tick;
        GridPolicy Policy;
        public JobSmartAirlock(Scheduler owner, GridPolicy policy, string tick)
        {
            Owner = owner;
            Policy = policy;
            Owner.GroupFound += GroupFound;
            Airlocks = _Airlocks;
        }

        void Command(MyCommandLine cmd)
        {
            switch (cmd.Items[1])
            {
                case "status":
                    foreach (var kv in _Airlocks)
                        Owner.Log($"{kv.Key.Name}: {kv.Value.CurrentState}");
                    break;
                case "inner":
                case "outer":
                    {
                        foreach (var kv in _Airlocks)
                            if (string.Equals(kv.Key.Name, cmd.Items[2], StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (kv.Value.CurrentState == "Idle")
                                    kv.Value.CurrentState = (cmd.Items[1] == "inner") ? "Inner Doors Opening" : "Outer Doors Opening";
                                else
                                    Owner.Log($"Can't trigger '{cmd.Items[2]}' - airlock is not idle.");
                                return;
                            }
                        Owner.Log($"No such airlock: '{cmd.Items[2]}'");
                    }; break;
            }
        }
        #region Save/Load
        Dictionary<string, string> SavedStates = new Dictionary<string, string>();
        void Save(MyIni state)
        {
            string cat = ID + ".Airlocks";
            foreach (var kv in _Airlocks)
                state.Set(cat, kv.Key.Name, kv.Value.CurrentState);
        }
        void Load(MyIni state)
        {
            string cat = ID + ".Airlocks";
            List<MyIniKey> keys = new List<MyIniKey>();
            state.GetKeys(cat, keys);
            foreach (var key in keys)
                SavedStates[key.Name] = state.Get(key).ToString();
        }
        #endregion
        #region Blocks
        Dictionary<IMyBlockGroup, Airlock> _Airlocks = new Dictionary<IMyBlockGroup, Airlock>();

        void GroupFound(GridScanArgs<KeyValuePair<IMyBlockGroup, List<IMyTerminalBlock>>> item)
        {
            if (item.First)
            {
                foreach (var kv in _Airlocks)
                    SavedStates[kv.Key.Name] = kv.Value.CurrentState;
                _Airlocks.Clear();
            }
            if (Owner.PolicyCheck(Policy, item.Item.Key) && Airlock.IsAirlock(item.Item.Value))
            {
                var a = new Airlock(item.Item.Value);
                try
                { a.CurrentState = SavedStates[item.Item.Key.Name]; }
                catch (KeyNotFoundException)
                { a.CurrentState = "Idle"; }
                _Airlocks.Add(item.Item.Key, a);
            }
            if (item.Last)
                SavedStates.Clear();
        }

        void Update(UpdateFrequency freq)
        {
            foreach (var a in _Airlocks.Values)
            {
                if (a.CurrentState == "Outer Doors Open" || a.CurrentState == "Inner Doors Open")
                    a.Interval += Owner.PB.Runtime.TimeSinceLastRun.TotalSeconds;
                a.Update();
            }
        }

        public class Airlock : StateMachine
        {
            public static string OuterTag = "Outer";
            public static string InnerTag = "Inner";
            public static double Timeout = 5.0;
            public double Interval = 0;
            List<IMyDoor> OuterDoors = new List<IMyDoor>();
            List<IMyDoor> InnerDoors = new List<IMyDoor>();
            List<IMyAirVent> Vents = new List<IMyAirVent>();
            List<IMyLightingBlock> Lights = new List<IMyLightingBlock>();
            public Airlock(List<IMyTerminalBlock> blocks)
            {
                foreach (var b in blocks)
                    if (IsOuterDoor(b))
                        OuterDoors.Add(b as IMyDoor);
                    else if (IsInnerDoor(b))
                        InnerDoors.Add(b as IMyDoor);
                    else if (IsVent(b))
                        Vents.Add(b as IMyAirVent);
                    else if (b.IsFunctional && b is IMyLightingBlock)
                        Lights.Add(b as IMyLightingBlock);
                States.Add("Idle", Idle);
                States.Add("Outer Doors Opening", OuterOpening);
                States.Add("Outer Doors Open", OuterOpen);
                States.Add("Outer Doors Closing", OuterClosing);
                States.Add("Inner Doors Opening", InnerOpening);
                States.Add("Inner Doors Open", InnerOpen);
                States.Add("Inner Doors Closing", InnerClosing);
                States.Add("Depressurizing", Depressurizing);
            }
            static bool IsOuterDoor(IMyTerminalBlock b) => b.IsFunctional && b is IMyDoor && b.CustomName.IndexOf(OuterTag, StringComparison.CurrentCultureIgnoreCase) >= 0;
            static bool IsInnerDoor(IMyTerminalBlock b) => b.IsFunctional && b is IMyDoor && b.CustomName.IndexOf(InnerTag, StringComparison.CurrentCultureIgnoreCase) >= 0;
            static bool IsVent(IMyTerminalBlock b) => b.IsFunctional && b is IMyAirVent;
            public static bool IsAirlock(List<IMyTerminalBlock> blocks) => blocks.Count(IsOuterDoor) > 0 && blocks.Count(IsInnerDoor) > 0 && blocks.Count(IsVent) > 0;

            IEnumerable<string> Idle()
            {
                foreach (var l in Lights) l.Color = Color.Green;
                foreach (var d in OuterDoors) { d.Enabled = true; d.CloseDoor(); }
                foreach (var d in InnerDoors) { d.Enabled = true; d.CloseDoor(); }
                foreach (var v in Vents) v.Depressurize = true;
                while (true)
                {
                    if (OuterDoors.Any(d => d.Status != DoorStatus.Closed))
                        yield return "Outer Doors Opening";
                    if (InnerDoors.Any(d => d.Status != DoorStatus.Closed))
                        yield return "Inner Doors Opening";
                    yield return null;
                }
            }

            IEnumerable<string> OuterOpening()
            {
                foreach (var l in Lights) l.Color = Color.Orange;
                foreach (var d in OuterDoors) { d.Enabled = true; d.OpenDoor(); }
                foreach (var d in InnerDoors) d.Enabled = false;
                while (OuterDoors.All(d => d.Status != DoorStatus.Open))
                    yield return null;
                yield return "Outer Doors Open";
            }

            IEnumerable<string> OuterOpen()
            {
                Interval = 0;
                while ((Interval < Timeout) && OuterDoors.All(d => d.Status == DoorStatus.Open))
                    yield return null;
                yield return "Outer Doors Closing";
            }

            IEnumerable<string> OuterClosing()
            {
                foreach (var d in OuterDoors) d.CloseDoor();
                while (OuterDoors.Any(d => d.Status != DoorStatus.Closed))
                    yield return null;
                yield return "Idle";
            }

            IEnumerable<string> InnerOpening()
            {
                foreach (var l in Lights) l.Color = Color.Orange;
                foreach (var d in InnerDoors) { d.Enabled = true; d.OpenDoor(); }
                foreach (var d in OuterDoors) d.Enabled = false;
                foreach (var v in Vents) v.Depressurize = false;
                while (InnerDoors.Any((d) => d.Status != DoorStatus.Open))
                    yield return null;
                yield return "Inner Doors Open";
            }

            IEnumerable<string> InnerOpen()
            {
                Interval = 0;
                while ((Interval < Timeout) && InnerDoors.All(d => d.Status == DoorStatus.Open))
                    yield return null;
                yield return "Inner Doors Closing";
            }

            IEnumerable<string> InnerClosing()
            {
                foreach (var d in InnerDoors) d.CloseDoor();
                while (InnerDoors.Any(d => d.Status != DoorStatus.Closed))
                    yield return null;
                yield return "Depressurizing";
            }

            IEnumerable<string> Depressurizing()
            {
                foreach (var d in InnerDoors) d.Enabled = false;
                foreach (var v in Vents) v.Depressurize = true;
                int ticks = 0;
                float previous = 0;
                float last = 0;
                while (ticks < 3)
                {
                    previous = last;
                    last = Vents.Sum((v) => v.GetOxygenLevel());
                    if (previous == last) ticks++;
                    else ticks = 0;
                    yield return null;
                }
                yield return "Idle";
            }
        }
        #endregion
        #region Screen output
        public bool TryParseMode(string mode, out object data) { data = null; return string.IsNullOrEmpty(mode); }
        public string SerializeMode(object data) { return string.Empty; }
        StringBuilder Buffer = new StringBuilder();
        public void Render(Window window, StringBuilder text, ref MySpriteDrawFrame frame)
        {
            int max = _Airlocks.Keys.Max((g) => g.Name.Length);
            foreach (var kv in _Airlocks)
                Buffer.Append(kv.Key.Name).Append(' ', max - kv.Key.Name.Length)
                    .Append(" : ").Append(kv.Value.CurrentState).Append('\n');
            frame.Add(window.Surface.FitText(Buffer.ToString(), window.Area, "Monospace", window.Surface.ScriptForegroundColor));
        }
        #endregion
    }
}
