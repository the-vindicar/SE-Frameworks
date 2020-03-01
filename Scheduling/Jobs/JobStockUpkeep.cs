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
    class JobStockUpkeep
    {
        public string ID = "Stock";
        public int MaxQueue = 1000;
        Scheduler Owner;
        JobInventory Tracker;
        GridPolicy AssemblerPolicy;
        bool IgnoreSurvivalKits;
        string Tick;
        bool Updating = false;
        Dictionary<MyItemType, int> DesiredStock = new Dictionary<MyItemType, int>();
        public JobStockUpkeep(Scheduler owner, JobInventory tracker, GridPolicy assmpolicy, bool ignoresurvkits, string tick)
        {
            Owner = owner;
            Tracker = tracker;
            AssemblerPolicy = assmpolicy;
            IgnoreSurvivalKits = ignoresurvkits;
            Tick = Owner.Subscribe(RecalculateStock, tick);
            Owner.BlockFound += BlockFound;
        }
        public JobStockUpkeep(Scheduler owner, JobInventory tracker)
        {
            Owner = owner;
            Tracker = tracker;
            AssemblerPolicy = GridPolicy.Types.SameGrid;
            IgnoreSurvivalKits = true;
            Tick = "update100s";
            Owner.BlockFound += BlockFound;
            Owner.Loading += Load;
            Owner.Saving += Save;
        }
        public int this[MyItemType type]
        {
            get { return DesiredStock[type]; }
            set
            {
                if (!Blueprints.ContainsKey(type))
                {
                    MyDefinitionId? bp = CreateBlueprint(type.SubtypeId);
                    if (!bp.HasValue)
                        throw new ArgumentException($"Failed to create blueprint for {type.ToString()}");
                    Blueprints[type] = bp.Value;
                    InverseBlueprints[bp.Value] = type;
                }
                DesiredStock[type] = value;
            }
        }
        public bool SetLimit(string name, int limit)
        {
            MyItemType type;
            if (AddBlueprint(name, out type))
            {
                DesiredStock[type] = limit;
                return true;
            }
            else
                return false;
        }
        #region Blocks
        List<IMyAssembler> Assemblers = new List<IMyAssembler>();
        int[] LoadReserve;
        List<MyProductionItem> Queue = new List<MyProductionItem>();
        void BlockFound(GridScanArgs<IMyTerminalBlock> item)
        {
            if (item.First)
            {
                Updating = true;
                Assemblers.Clear();
            }
            if ((item.Item is IMyAssembler) && 
                (!IgnoreSurvivalKits || (item.Item.BlockDefinition.TypeIdString != "MyObjectBuilder_SurvivalKit")) &&
                Owner.PolicyCheck(AssemblerPolicy, item.Item))
            {
                Assemblers.Add(item.Item as IMyAssembler);
            }
            if (item.Last)
            {
                LoadReserve = new int[Assemblers.Count];
                Updating = false;
            }
        }
        #endregion
        #region Work orders
        Dictionary<MyItemType, int> Deficit = new Dictionary<MyItemType, int>();
        List<KeyValuePair<MyItemType, int>> Order = new List<KeyValuePair<MyItemType, int>>();
        void RecalculateStock(UpdateFrequency tick)
        {
            if (Updating) return;
            bool has_deficit = false;
            foreach (var kv in DesiredStock)
            {
                JobInventory.ItemTypeDescriptor value;
                int amount;
                if (Tracker.Stock.TryGetValue(kv.Key, out value))
                    amount = (int)value.Amount;
                else
                    amount = 0;
                int lack = kv.Value - amount;
                Deficit[kv.Key] = lack;
                has_deficit = has_deficit || lack > 0;
            }
            if (!has_deficit) return;
            Queue.Clear();
            MyItemType ItemType;
            for (int i = Assemblers.Count-1; i >= 0; i--)
            {
                IMyAssembler asm = Assemblers[i];
                if (asm.IsAlive() && asm.IsWorking && asm.Mode == MyAssemblerMode.Assembly)
                {
                    LoadReserve[i] = MaxQueue;
                    asm.Repeating = false;
                    asm.GetQueue(Queue);
                    foreach (var item in Queue)
                    {
                        int load = (int)item.Amount;
                        if (InverseBlueprints.TryGetValue(item.BlueprintId, out ItemType) && Deficit.ContainsKey(ItemType))
                            Deficit[ItemType] -= load;
                        LoadReserve[i] -= load;
                    }
                    if (LoadReserve[i] < 0) LoadReserve[i] = 0;
                    Queue.Clear();
                }
                else
                    LoadReserve[i] = 0;
            }
            Order.Clear();
            foreach (var kv in Deficit)
                if (kv.Value > 0)
                    Order.Add(kv);
            Order.Sort((a, b) => a.Value.CompareTo(b.Value));
            Deficit.Clear();
            if (Order.Count > 0)
                QueueNewItems();
        }

        void QueueNewItems()
        {
            int total_reserve = LoadReserve.Sum();
            MyDefinitionId bp;
            if (total_reserve > 0)
                foreach (var kv in Order)
                {
                    int load_left = kv.Value;
                    if (Blueprints.TryGetValue(kv.Key, out bp))
                        for (int i = Assemblers.Count - 1; (i >= 0) && (load_left >= 1); i--)
                        {
                            int part = Math.Min(load_left, kv.Value * LoadReserve[i] / total_reserve);
                            if (part > 0 && Assemblers[i].CanUseBlueprint(bp))
                            {
                                Assemblers[i].AddQueueItem(bp, (decimal)part);
                                load_left -= part;
                            }
                        }
                    else
                        throw new ArgumentException($"No blueprint for {kv.Key.ToString()}");
                }
            Order.Clear();
        }
        #endregion
        #region Save/Load
        void Save(MyIni state)
        {
            state.Set(ID, "Policy", AssemblerPolicy.ToString());
            state.Set(ID, "MaxQueue", MaxQueue);
            state.Set(ID, "IgnoreSurvivalKits", IgnoreSurvivalKits);
            state.Set(ID, "Update", Tick);
            string cat = ID + ".Limits";
            if (DesiredStock.Count > 0)
                foreach (var kv in DesiredStock)
                    state.Set(cat, kv.Key.SubtypeId, kv.Value);
            else
            {
                state.Set(cat, "Dummy", 0);
                state.SetSectionComment(cat, "You can set item limits like this:\nComponent/SteelPlate=1000\nComponent/LargeTube=50\n");
                state.Delete(cat, "Dummy");
            }
        }

        void Load(MyIni state)
        {
            AssemblerPolicy = state.Get(ID, "Policy").ToString();
            IgnoreSurvivalKits = state.Get(ID, "IgnoreSurvivalKits").ToBoolean(true);
            MaxQueue = state.Get(ID, "MaxQueue").ToInt32(1000);
            Owner.Unsubscribe(RecalculateStock, Tick);
            Tick = Owner.Subscribe(RecalculateStock, state.Get(ID, "Update").ToString(), "update100s");
            string cat = ID + ".Limits";
            List<MyIniKey> keys = new List<MyIniKey>();
            state.GetKeys(cat, keys);
            MyItemType type;
            int amount;
            foreach (MyIniKey key in keys)
                if (AddBlueprint(key.Name, out type) && ((amount = state.Get(key).ToInt32(0)) > 0))
                    DesiredStock[type] = amount;
        }
        #endregion
        #region Blueprints
        public Dictionary<MyItemType, MyDefinitionId> Blueprints = new Dictionary<MyItemType, MyDefinitionId>();
        public Dictionary<MyDefinitionId, MyItemType> InverseBlueprints = new Dictionary<MyDefinitionId, MyItemType>();
        public bool AddBlueprint(string name, out MyItemType type)
        {
            MyDefinitionId item;
            MyDefinitionId? blueprint;
            if (MyDefinitionId.TryParse("MyObjectBuilder_" + name, out item) &&
                ((blueprint = CreateBlueprint(item.SubtypeName)) != null))
            {
                Blueprints[item] = blueprint.Value;
                InverseBlueprints[blueprint.Value] = item;
                type = item;
                return true;
            }
            type = default(MyItemType);
            return false;
        }
        static MyDefinitionId? CreateBlueprint(string name)
        {
            switch (name)
            {
                case "RadioCommunication":
                case "Computer":
                case "Reactor":
                case "Detector":
                case "Construction":
                case "Thrust":
                case "Motor":
                case "Explosives":
                case "Girder":
                case "GravityGenerator":
                case "Medical": name += "Component"; break;
                case "NATO_25x184mm":
                case "NATO_5p56x45mm": name += "Magazine"; break;
            }
            MyDefinitionId id;
            if (MyDefinitionId.TryParse("MyObjectBuilder_BlueprintDefinition/" + name, out id) && (id.SubtypeId != null))
                return id;
            else
                return null;
        }
        #endregion
    }
}
