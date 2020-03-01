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
    /// <summary>Displays inventory capacity on screens.</summary>    
    class JobCapacityMonitor : IHasOutput
    {
        public string ID { get; set; } = "Storage";
        public Color BgColor = Color.Transparent;
        public int InventorySlice = 50;
        bool Updating = false;
        Scheduler Owner;
        ScreenManager Manager;
        GridPolicy Policy;
        string Tick;
        public JobCapacityMonitor(Scheduler owner, ScreenManager manager, GridPolicy policy, string updateTick)
        {
            Owner = owner;
            Manager = manager;
            Policy = policy;
            Tick = updateTick;
            Owner.Subscribe(UpdateBlocks, Tick);
            owner.BlockFound += BlockFound;
        }
        public JobCapacityMonitor(Scheduler owner, ScreenManager manager)
        {
            Owner = owner;
            Manager = manager;
            Policy = GridPolicy.Types.SameConstruct;
            owner.BlockFound += BlockFound;
            owner.Saving += Save;
            owner.Loading += Load;
        }
        #region Save/Load
        void Save(MyIni state)
        {
            state.Set(ID, "InventorySlice", InventorySlice);
            state.Set(ID, "Policy", Policy.ToString());
            state.Set(ID, "Update", Tick);
        }

        void Load(MyIni state)
        {
            InventorySlice = state.Get(ID, "InventorySlice").ToInt32(50);
            if (!GridPolicy.TryParse(state.Get(ID, "Policy").ToString("SameGrid"), out Policy))
                Policy = GridPolicy.Types.SameGrid;
            Owner.Unsubscribe(UpdateBlocks, Tick);
            Tick = Owner.Subscribe(UpdateBlocks, state.Get(ID, "Update").ToString(), "update100s");
        }
        #endregion
        #region Blocks
        static string GetCategoryName(IMyTerminalBlock block) { return block.DefinitionDisplayNameText; }
        class InvRatio: Ratio
        {
            public IMyInventory Inventory;
            public IMyTerminalBlock Block;
            public InvRatio(IMyInventory inv, IMyTerminalBlock b, float t, float c, int n) 
                : base(t, c, n)
            { Inventory = inv; Block = b; }
        }
        List<InvRatio> Inventories = new List<InvRatio>();
        int InvIndex, TankIndex;
        List<Ratio<IMyGasTank>> Tanks = new List<Ratio<IMyGasTank>>();
        IDictionary<string, Ratio> ByCategory = new SortedDictionary<string, Ratio>(StringComparer.CurrentCultureIgnoreCase);
        void BlockFound(GridScanArgs<IMyTerminalBlock> block)
        {
            if (block.First)
            {
                Updating = true;
                Inventories.Clear();
                Tanks.Clear();
                ByCategory.Clear();
            }
            if (Owner.PolicyCheck(Policy, block.Item) && (block.Item.InventoryCount > 0 || block.Item is IMyGasTank))
            {
                string key = GetCategoryName(block.Item);
                if (!ByCategory.ContainsKey(key)) ByCategory[key] = new Ratio(0, 0, 0);
                for (int i = 0; i < block.Item.InventoryCount; i++)
                {
                    var inv = block.Item.GetInventory(i);
                    var cache = new InvRatio(inv, block.Item, (float)inv.MaxVolume, (float)inv.CurrentVolume, 1);
                    ByCategory[key].Add(cache);
                    Inventories.Add(cache);
                }
                if (block.Item is IMyGasTank)
                {
                    var tank = block.Item as IMyGasTank;
                    var cache = new Ratio<IMyGasTank>(tank, tank.Capacity, (float)tank.FilledRatio * tank.Capacity, 1);
                    ByCategory[key].Add(cache);
                    Tanks.Add(cache);
                }
            }
            if (block.Last)
            {
                Inventories.Sort((a, b) => a.Block.CustomName.CompareTo(b.Block.CustomName));
                Tanks.Sort((a, b) => a.Target.CustomName.CompareTo(b.Target.CustomName));
                InvIndex = Inventories.Count - 1;
                TankIndex = Tanks.Count - 1;
                Updating = false;
            }
        }

        void UpdateBlocks(UpdateFrequency freq)
        {
            if (Updating) return;
            for (int i = 0; (InvIndex >= 0) && (i < InventorySlice); i++)
            {
                var inv = Inventories[InvIndex];
                string key = GetCategoryName(inv.Block);
                ByCategory[key].Subtract(inv);
                if (inv.Block.IsAlive())
                {
                    inv.Total = (float)inv.Inventory.MaxVolume;
                    inv.Current = (float)inv.Inventory.CurrentVolume;
                    ByCategory[key].Add(inv);
                }
                else
                    Inventories.RemoveAt(InvIndex);
                InvIndex--;
            }
            if (InvIndex < 0) InvIndex = Inventories.Count - 1;
            for (int i = 0; (TankIndex >= 0) && (i < InventorySlice); i++)
            {
                var tank = Tanks[TankIndex];
                string key = GetCategoryName(tank.Target);
                ByCategory[key].Subtract(tank);
                if (tank.Target.IsAlive())
                {
                    tank.Total = tank.Target.Capacity;
                    tank.Current = (float)tank.Target.FilledRatio * tank.Target.Capacity;
                    ByCategory[key].Add(tank);
                }
                else
                    Tanks.RemoveAt(TankIndex);
                TankIndex--;
            }
            if (TankIndex < 0) TankIndex = Tanks.Count - 1;
        }
        #endregion
        #region Screens
        StringBuilder Buffer = new StringBuilder();

        public void Render(Window window, StringBuilder text, ref MySpriteDrawFrame frame)
        {
            if (Updating) return;
            Buffer.Clear();
            if (BgColor != Color.Transparent)
                frame.Add(window.Surface.FitSprite("SquareSimple", window.Area, BgColor));
            if (window.GetData<string>() == "compact")
            {
                int maxlen = ByCategory.Keys.Max((k) => k.Length);
                foreach (var kv in ByCategory)
                    Buffer.Append(kv.Key).Append(' ', maxlen - kv.Key.Length + 1)
                        .AppendFormat("x{0,-3} {1,6:P1}", kv.Value.Count, kv.Value.GetRatio(0)).Append('\n');
            }
            else
            {
                int maxlen = Inventories.Max((i) => i.Block.CustomName.Length);
                maxlen = Math.Max(maxlen, Tanks.Max((t) => t.Target.CustomName.Length));
                foreach (var inv in Inventories)
                {
                    var name = inv.Block.CustomName;
                    Buffer.Append(name).Append(' ', maxlen - name.Length + 1)
                        .AppendFormat("{0,6:P1}", inv.GetRatio(0)).Append('\n');
                }
                foreach (var tank in Tanks)
                {
                    var name = tank.Target.CustomName;
                    Buffer.Append(name).Append(' ', maxlen - name.Length + 1)
                        .AppendFormat("{0,6:P1}", tank.GetRatio(0)).Append('\n');
                }
            }
            frame.Add(window.Surface.FitText(Buffer.ToString(), window.Area, "Monospace", window.Surface.ScriptForegroundColor, TextAlignment.CENTER));
        }

        public bool TryParseMode(string mode, out object data)
        {
            data = null;
            if (string.Equals(mode, "compact", StringComparison.CurrentCultureIgnoreCase))
                data = "compact";
            else if (string.IsNullOrWhiteSpace(mode)
                || string.Equals(mode, "full", StringComparison.CurrentCultureIgnoreCase))
                data = "full";
            return data != null;
        }
        public string SerializeMode(object data) { return (string)data; }
        #endregion
    }
}
