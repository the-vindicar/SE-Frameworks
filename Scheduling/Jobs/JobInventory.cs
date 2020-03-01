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
    class JobInventory
    {
        public string JobName = "InventoryTracker";
        /// <summary>If true, the job is currently scanning for inventories. Stock contains inaccurate data.</summary>
        public bool Updating { get; private set; } = true;
        public int InventorySlice = 50;
        public IReadOnlyDictionary<MyItemType, ItemTypeDescriptor> Stock;
        public ICollection<IInventorySnapshot> Inventories;
        Scheduler Owner;
        GridPolicy Policy;
        string Tick;
        bool IgnoreTools;
        /// <summary>This constructor uses provided paramaters to configure the job.</summary>
        /// <param name="owner">Scheduler to attach to.</param>
        /// <param name="block_policy">Which blocks to use.</param>
        /// <param name="ignore_tools">If True, ship tools (welders/grinders/drills) are ignored.</param>
        public JobInventory(Scheduler owner, GridPolicy block_policy, bool ignore_tools, string tick)
        {
            Owner = owner;
            Stock = _Stock;
            Owner.BlockFound += BlockFound;
            Owner.RegisterCommand(JobName, Command);
            Inventories = Snapshots;
            Tick = Owner.Subscribe(UpdateInventoryTick, tick);
            Policy = block_policy;
            IgnoreTools = ignore_tools;
        }
        /// <summary>This constructor will have job config loaded from saved state instead.</summary>
        /// <param name="owner">Scheduler to attach to.</param>
        public JobInventory(Scheduler owner)
        {
            Owner = owner;
            Stock = _Stock;
            Owner.BlockFound += BlockFound;
            Owner.RegisterCommand(JobName, Command);
            Inventories = Snapshots;
            Policy = GridPolicy.Types.SameConstruct;
            IgnoreTools = true;
            Tick = "update100s";
            Owner.Loading += Load;
            Owner.Saving += Save;
        }
        const string Help =
@"
Available subcommands:
  help - show this message.
  amount 'Item Name' - show current stock on certain item.
  reset - reset stored stock info and let it rebuild from scratch.
";
        void Command(MyCommandLine cmd)
        {
            switch (cmd.Argument(1))
            {
                case "help": Owner.Log(Help); break;
                case "amount":
                    {
                        string name = cmd.Argument(2);
                        bool found = false;
                        foreach (var item in _Stock)
                            if (item.Value.Name.IndexOf(name, StringComparison.CurrentCultureIgnoreCase) >= 0)
                            {
                                found = true;
                                Owner.Log($"{item.Value.Name}: {item.Value.FormatAmount()}");
                            }
                        if (!found) Owner.Log("No items found.");
                    }; break;
                case "reset":
                    {
                        Updating = true;
                        _Stock.Clear();
                        Owner.ScanGrid();
                        Owner.Log("Stock reset. Scanning grid...");
                    }; break;
            }
        }
        #region Inventory updates
        Dictionary<MyItemType, ItemTypeDescriptor> _Stock = new Dictionary<MyItemType, ItemTypeDescriptor>();
        List<IInventorySnapshot> Snapshots = new List<IInventorySnapshot>();
        int SnapshotIndex = 0;

        void BlockFound(GridScanArgs<IMyTerminalBlock> args)
        {
            if (args.First)
            {
                Updating = true;
                Snapshots.Clear();
            }
            if (Owner.PolicyCheck(Policy, args.Item) && (!IgnoreTools || !(args.Item is IMyShipToolBase)))
            {
                for (int i = 0; i < args.Item.InventoryCount; i++)
                {
                    var snap = new StorageSnapshot(args.Item, i);
                    snap.UpdateStock(_Stock);
                    Snapshots.Add(snap);
                }
                if (args.Item is IMyGasTank)
                {
                    var snap = new TankSnapshot(args.Item);
                    Snapshots.Add(snap);
                    snap.UpdateStock(_Stock);
                }
            }
            if (args.Last)
            {
                SnapshotIndex = Snapshots.Count - 1;
                Updating = false;
            }
        }

        void UpdateInventoryTick(UpdateFrequency freq)
        {
            if (Updating) return;
            for (int i = 0; (i < InventorySlice) && (SnapshotIndex >= 0); i++)
            {
                if (!Snapshots[SnapshotIndex].UpdateStock(_Stock))
                    Snapshots.RemoveAt(SnapshotIndex);
                SnapshotIndex--;
            }
            if (SnapshotIndex < 0) SnapshotIndex = Snapshots.Count - 1;
        }
        #endregion

        #region Save/Load state
        void Save(MyIni state)
        {
            state.Set(JobName, "InventorySlice", InventorySlice);
            state.Set(JobName, "Policy", Policy.ToString());
            state.Set(JobName, "IgnoreTools", IgnoreTools);
            state.Set(JobName, "Update", Tick);
        }

        void Load(MyIni state)
        {
            InventorySlice = state.Get(JobName, "InventorySlice").ToUInt16(50);
            Policy = state.Get(JobName, "Policy").ToString("SameConstruct");
            Owner.Unsubscribe(UpdateInventoryTick, Tick);
            Tick = Owner.Subscribe(UpdateInventoryTick, state.Get(JobName, "Update").ToString(), "update100s");
        }

        #endregion
        //Stores all necessary info on item types.
        public class ItemTypeDescriptor : IComparable<ItemTypeDescriptor>
        {
            #region mdk preserve
            [Flags]
            public enum ItemCategory : byte { None = 0, Other = 1, Ore = 2, Ingot = 4, Gas = 8, Component = 16, Ammo = 32, Tool = 64, Material = 14 }
            #endregion
            public readonly string Name;
            public readonly ItemCategory Category;
            public readonly MyItemInfo Info;
            public readonly MyItemType Type;
            public readonly string[] Units;
            public double Amount;
            public ItemTypeDescriptor(MyItemType type, double value = 0)
            {
                Amount = value;
                Type = type;
                Info = type.GetItemInfo();
                if (Type.TypeId == "MyObjectBuilder_GasProperties") Category = (Type.SubtypeId == "Electricity") ? ItemCategory.None : ItemCategory.Gas;
                else if (Info.IsOre) Category = ItemCategory.Ore;
                else if (Info.IsIngot) Category = ItemCategory.Ingot;
                else if (Info.IsAmmo) Category = ItemCategory.Ammo;
                else if (Info.IsComponent) Category = ItemCategory.Component;
                else if (Info.IsTool) Category = ItemCategory.Tool;
                else Category = ItemCategory.Other;
                if (Type.TypeId == "MyObjectBuilder_GasProperties")
                    Units = new string[] { "L ", "kL", "ML" };
                else if (Info.UsesFractions)
                    Units = new string[] { "kg", "t ", "kt" };
                else
                    Units = new string[] { "", "K", "M" };
                switch (Category)
                {
                    case ItemCategory.Ore:
                        switch (Type.SubtypeId)
                        {
                            case "Scrap":
                            case "Stone":
                            case "Ice":
                            case "Organic": Name = Type.SubtypeId; break;
                            default: Name = $"{Type.SubtypeId} Ore"; break;
                        }; break;
                    case ItemCategory.Ingot:
                        switch (Type.SubtypeId)
                        {
                            case "Stone": Name = "Gravel"; break;
                            case "Magnesium": Name = "Magnesium Powder"; break;
                            case "Silicon": Name = "Silicon Wafer"; break;
                            default: Name = $"{Type.SubtypeId} Ingot"; break;
                        }; break;
                    case ItemCategory.Component:
                        switch (Type.SubtypeId)
                        {
                            case "SmallTube": Name = "Small Tube"; break;
                            case "LargeTube": Name = "Large Tube"; break;
                            default: Name = Type.SubtypeId; break;
                        }; break;
                    default:
                        Name = Type.SubtypeId; break;
                }
            }
            public string FormatAmount()
            {
                double amount = Amount;
                int i;
                for (i = 0; i < Units.Length - 1 && amount >= 1000.0; i++)
                    amount /= 1000.0;
                return string.IsNullOrEmpty(Units[i]) ? $"{amount:F0}" : $"{amount:F1}{Units[i]}";
            }
            public override string ToString() { return $"{Name}: {Amount}"; }
            public int CompareTo(ItemTypeDescriptor other) { return this.Name.CompareTo(other.Name); }
        }
        /// <summary>Describes an inventory.</summary>
        public interface IInventorySnapshot
        {
            IMyTerminalBlock Block { get; }
            /// <summary>Name of the block storing the inventory.</summary>
            string Name { get; }
            /// <summary>Whether the storage block exists.</summary>
            bool Alive { get; }
            /// <summary>How full the storage block is.</summary>
            double Ratio { get; }
            /// <summary>Updates stock dictionary according to the current and previous state of the storage.</summary>
            /// <param name="stock">Stock dictionary to update.</param>
            /// <returns>True if stock has been updated, False if the storage is gone.</returns>
            bool UpdateStock(Dictionary<MyItemType, ItemTypeDescriptor> stock);
            void ClearStock(Dictionary<MyItemType, ItemTypeDescriptor> stock);
        }
        class StorageSnapshot : IInventorySnapshot
        {
            IMyInventory Inventory;
            List<MyInventoryItem> Items;
            public IMyTerminalBlock Block { get { return Inventory?.Owner as IMyTerminalBlock; } }
            public string Name { get { return (Block?.CustomName ?? "N/A"); } }
            public bool Alive { get { return (Block != null) && (Block.WorldMatrix != MatrixD.Identity); } }
            public double Ratio { get { return (Inventory != null) ? ((double)Inventory.CurrentVolume / (double)Inventory.MaxVolume) : 0; } }
            public StorageSnapshot(IMyTerminalBlock block, int index)
            {
                Inventory = block.GetInventory(index);
                Items = new List<MyInventoryItem>();
            }
            public void ClearStock(Dictionary<MyItemType, ItemTypeDescriptor> stock)
            {
                foreach (var item in Items)
                    if (stock.ContainsKey(item.Type))
                        stock[item.Type].Amount -= (double)item.Amount;
            }
            public bool UpdateStock(Dictionary<MyItemType, ItemTypeDescriptor> stock)
            {
                ClearStock(stock);
                Items.Clear();
                if (Alive)
                {
                    Inventory.GetItems(Items);
                    foreach (var item in Items)
                        if (stock.ContainsKey(item.Type))
                            stock[item.Type].Amount += (double)item.Amount;
                        else
                            stock[item.Type] = new ItemTypeDescriptor(item.Type, (double)item.Amount);
                    return true;
                }
                else
                {
                    Inventory = null;
                    return false;
                }
            }
        }
        class TankSnapshot : IInventorySnapshot
        {
            static readonly MyItemType Hydrogen = new MyItemType("MyObjectBuilder_GasProperties", "Hydrogen");
            static readonly MyItemType Oxygen = new MyItemType("MyObjectBuilder_GasProperties", "Oxygen");
            IMyGasTank Tank;
            MyItemType type;
            double Amount;
            public IMyTerminalBlock Block { get { return Tank; } }
            public string Name { get { return (Tank?.CustomName ?? "N/A"); } }
            public bool Alive { get { return (Tank != null) && (Tank.WorldMatrix != MatrixD.Identity); } }
            public double Ratio { get { return (Tank != null) ? Tank.FilledRatio : 0; } }
            public TankSnapshot(IMyTerminalBlock block)
            {
                Tank = (IMyGasTank)block;
                Amount = 0;
                var sink = Tank.Components.Get<MyResourceSinkComponent>();
                type = sink.AcceptedResources.Contains(Oxygen) ? Oxygen : Hydrogen;
            }
            public void ClearStock(Dictionary<MyItemType, ItemTypeDescriptor> stock)
            {
                if (stock.ContainsKey(type))
                    stock[type].Amount -= Amount;
            }
            public bool UpdateStock(Dictionary<MyItemType, ItemTypeDescriptor> stock)
            {
                ClearStock(stock);
                if (Alive)
                {
                    Amount = Tank.Capacity * Tank.FilledRatio;
                    if (stock.ContainsKey(type))
                        stock[type].Amount += Amount;
                    else
                        stock[type] = new ItemTypeDescriptor(type, Amount);
                    return true;
                }
                else
                {
                    Tank = null;
                    Amount = 0;
                    return false;
                }
            }
        }
    }
}
