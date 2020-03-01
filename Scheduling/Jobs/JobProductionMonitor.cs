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
    class JobProductionMonitor : IHasOutput
    {
        public string ID { get; set; } = "Production";
        public Color BgColor = Color.Transparent;
        bool Updating = false;
        Scheduler Owner;
        ScreenManager Manager;
        bool LoadConfig;
        bool IgnoreSurvivalKits;
        GridPolicy Policy;
        string Tick;
        public JobProductionMonitor(Scheduler owner, ScreenManager manager, GridPolicy policy, string tick, bool ignore_survkits)
        {
            Owner = owner;
            Manager = manager;
            LoadConfig = false;
            Policy = policy;
            IgnoreSurvivalKits = ignore_survkits;
            Tick = Owner.Subscribe(UpdateProductionState, tick);
            Owner.Saving += Save;
            Owner.Loading += Load;
            Owner.BlockFound += AcquireBlock;
        }
        public JobProductionMonitor(Scheduler owner, ScreenManager manager) 
        {
            Owner = owner;
            Manager = manager;
            LoadConfig = true;
            Policy = GridPolicy.Types.SameConstruct;
            IgnoreSurvivalKits = true;
            Tick = "update100s";
            Owner.Saving += Save;
            Owner.Loading += Load;
            Owner.BlockFound += AcquireBlock;
        }
        #region Save/Load
        public bool TryParseMode(string mode, out object data) { data = new ScreenContent(); return true; }

        public string SerializeMode(object data) { return string.Empty; }

        void Save(MyIni state)
        {
            state.Set(ID, "Policy", Policy.ToString());
            state.Set(ID, "IgnoreSurvivalKits", IgnoreSurvivalKits);
            state.Set(ID, "Update", Tick);
            string cat = ID + ".Assemblers";
            foreach (var a in Assemblers)
                if (a.HasBlock)
                    state.Set(cat, a.Block.EntityId.ToString(), a.Save());
        }

        void Load(MyIni state)
        {
            if (LoadConfig)
            {
                Policy = state.Get(ID, "Policy").ToString();
                IgnoreSurvivalKits = state.Get(ID, "IgnoreSurvivalKits").ToBoolean(true);
                Owner.Unsubscribe(UpdateProductionState, Tick);
                Tick = Owner.Subscribe(UpdateProductionState, state.Get(ID, "Update").ToString(), "update100s");
            }
            List<MyIniKey> keys = new List<MyIniKey>();
            Assemblers.Clear();
            Refineries.Clear();
            state.GetKeys(ID + ".Assemblers", keys);
            foreach (var key in keys)
                Assemblers.Add(new AssemblerInfo(key.Name, state.Get(key).ToString()));
        }
        #endregion
        #region Blocks
        void AcquireBlock(GridScanArgs<IMyTerminalBlock> item)
        {
            if (item.First)
            {
                Updating = true;
                AssemblerUpdater.Dispose();
                foreach (var a in Assemblers) a.Block = null;
                Refineries.Clear();
            }
            if (Owner.PolicyCheck(Policy, item.Item))
            {
                if (item.Item is IMyRefinery)
                    Refineries.Add(item.Item as IMyRefinery);
                if ((item.Item is IMyAssembler) && (!IgnoreSurvivalKits || (item.Item.BlockDefinition.TypeIdString != "MyObjectBuilder_SurvivalKit")))
                {
                    bool found = false;
                    foreach (var a in Assemblers)
                        if (a.TryAttachBlock(item.Item))
                        {
                            found = true;
                            break;
                        }
                    if (!found)
                        Assemblers.Add(new AssemblerInfo(item.Item as IMyAssembler));
                }
            }
            if (item.Last)
            {
                for (int i = Assemblers.Count - 1; i >= 0; i--)
                    if (!Assemblers[i].HasBlock)
                        Assemblers.RemoveAt(i);
                Assemblers.Sort((a, b) => a.Block.CustomName.CompareTo(b.Block.CustomName));
                Assemblers.Sort((a, b) => a.Block.CubeGrid.EntityId.CompareTo(b.Block.CubeGrid.EntityId));
                Refineries.Sort((a, b) => a.CustomName.CompareTo(b.CustomName));
                Refineries.Sort((a, b) => a.CubeGrid.EntityId.CompareTo(b.CubeGrid.EntityId));
                AssemblerUpdater = Assemblers.GetEnumerator();
                Updating = false;
            }
        }

        List<IMyRefinery> Refineries = new List<IMyRefinery>();
        List<AssemblerInfo> Assemblers = new List<AssemblerInfo>();
        List<AssemblerInfo>.Enumerator AssemblerUpdater;

        void UpdateProductionState(UpdateFrequency freq)
        {
            if (Updating || Assemblers.Count == 0) return;
            for (int i = 0; i < 50; i++)
                if (AssemblerUpdater.MoveNext())
                    AssemblerUpdater.Current.Update();
                else
                {
                    AssemblerUpdater.Dispose();
                    AssemblerUpdater = Assemblers.GetEnumerator();
                    break;
                }
        }

        enum ProductionStatus { Producing, Waiting, Disabled }
        class AssemblerInfo
        {
            private List<MyProductionItem> Queue = new List<MyProductionItem>();
            public bool HasBlock { get { return Block != null && Block.WorldMatrix != MatrixD.Identity; } }
            long EID;
            public IMyAssembler Block;
            public Ratio Assembly = new Ratio();
            public Ratio Disassembly = new Ratio();
            public AssemblerInfo(IMyAssembler block)
            {
                Block = block;
                EID = Block.EntityId;
            }
            public AssemblerInfo(string eid, string saved)
            {
                EID = long.Parse(eid);
                Block = null;
                string[] parts = saved.Split('\n');
                if (parts.Length != 4)
                    throw new ArgumentException("Invalid saved string");
                if (!float.TryParse(parts[0], out Assembly.Current)
                    || !float.TryParse(parts[1], out Assembly.Total)
                    || !float.TryParse(parts[2], out Disassembly.Current)
                    || !float.TryParse(parts[3], out Disassembly.Total)
                    )
                {
                    Assembly.Reset();
                    Disassembly.Reset();
                }
            }
            public bool TryAttachBlock(IMyTerminalBlock block)
            {
                if (block != null && EID == block.EntityId)
                {
                    Block = block as IMyAssembler;
                    return true;
                }
                else
                    return false;
            }
            public string Save() { return $"{Assembly.Current}\n{Assembly.Total}\n{Disassembly.Current}\n{Disassembly.Total}"; }
            public void Update()
            {
                if (!HasBlock) return;
                if (Block.IsQueueEmpty)
                    Assembly.Reset();
                else
                {
                    Block.GetQueue(Queue);
                    Assembly.Current = Queue.Sum((item) => (float)item.Amount);
                    Queue.Clear();
                    Assembly.Total = Math.Max(Assembly.Current, Assembly.Total);
                }
                IMyInventory inv = Block.GetInventory(1);
                Disassembly.Current = (float)inv.CurrentVolume;
                Disassembly.Total = (Disassembly.Current == 0) ? 0 : Math.Max(Disassembly.Current, Disassembly.Total);
            }
            public float GetProgress()
            {
                if (!HasBlock) return float.NaN;
                if (Block.Mode == MyAssemblerMode.Assembly)
                    return 1.0f - Assembly.GetRatio();
                else
                    return 1.0f - Disassembly.GetRatio();
            }

            public ProductionStatus Status
            {
                get
                {
                    if (!HasBlock || !Block.IsWorking)
                        return ProductionStatus.Disabled;
                    else
                        return Block.IsProducing ? ProductionStatus.Producing : ProductionStatus.Waiting;
                }
            }
        }

        float CalculateProgressForRefinery(IMyRefinery block)
        {
            if (!block.IsAlive()) return float.NaN;
            IMyInventory inv = block.GetInventory(0);
            if ((double)inv.CurrentVolume > 0)
                return (float)(1.0 - (double)inv.CurrentVolume / (double)inv.MaxVolume);
            else
                return float.NaN;
        }

        ProductionStatus GetRefineryStatus(IMyRefinery block)
        {
            if (!block.IsAlive() || !block.IsWorking)
                return ProductionStatus.Disabled;
            else
                return block.IsProducing ? ProductionStatus.Producing : ProductionStatus.Waiting;
        }

        #endregion
        #region Screens
        public void Render(Window window, StringBuilder text, ref MySpriteDrawFrame frame)
        {
            var info = window.GetData<ScreenContent>();
            int refcount = Refineries.Count;
            int totalcount = refcount + Assemblers.Count;
            if (totalcount == 0) return;
            if (info.StaticSprites.Length != totalcount || info.ProgressBars.Length != totalcount)
            {
                var cells = window.Surface.MakeTable(totalcount, 2.0f, new Vector2(0.01f, 0.01f), new Vector2(0.01f, 0.01f), window.Area).GetEnumerator();
                info.StaticSprites = new MySprite[totalcount];
                info.ProgressBars = new ProgressBar[totalcount];
                for (int i = 0; i < totalcount; i++)
                {
                    cells.MoveNext();
                    IMyTerminalBlock b = (i < refcount) 
                        ? Refineries[i] as IMyTerminalBlock 
                        : Assemblers[i - Refineries.Count].Block as IMyTerminalBlock;
                    string name = b.IsSameConstructAs(Owner.PB.Me) ? b.CustomName : $"[{b.CustomName}]";
                    RectangleF top = cells.Current.SubRect(0.0f, 0.0f, 1.0f, 0.5f);
                    RectangleF bottom = cells.Current.SubRect(0.0f, 0.5f, 1.0f, 0.5f);
                    info.StaticSprites[i] = window.Surface.FitText(name, top, "Debug", window.Surface.ScriptForegroundColor);
                    info.ProgressBars[i] = new ProgressBar(window.Surface, bottom, Color.Black, Color.Transparent, window.Surface.ScriptForegroundColor);
                }
            }
            for (int i = 0; i < totalcount; i++)
                if (i < refcount)
                {
                    switch (GetRefineryStatus(Refineries[i]))
                    {
                        case ProductionStatus.Disabled: info.ProgressBars[i].ForegroundColor = Color.Red; break;
                        case ProductionStatus.Producing: info.ProgressBars[i].ForegroundColor = Color.Green; break;
                        case ProductionStatus.Waiting: info.ProgressBars[i].ForegroundColor = Color.Blue; break;
                    }
                    info.ProgressBars[i].Value = CalculateProgressForRefinery(Refineries[i]);
                }
                else
                {
                    switch (Assemblers[i - refcount].Status)
                    {
                        case ProductionStatus.Disabled: info.ProgressBars[i].ForegroundColor = Color.Red; break;
                        case ProductionStatus.Producing: info.ProgressBars[i].ForegroundColor = Color.Green; break;
                        case ProductionStatus.Waiting: info.ProgressBars[i].ForegroundColor = Color.Blue; break;
                    }
                    info.ProgressBars[i].Value = Assemblers[i - refcount].GetProgress();
                }
            if (BgColor != Color.Transparent)
                frame.Add(window.Surface.FitSprite("SquareSimple", window.Area, BgColor));
            frame.AddRange(info.StaticSprites);
            foreach (var pb in info.ProgressBars)
                frame.AddRange(pb);
        }

        class ScreenContent
        {
            public MySprite[] StaticSprites = new MySprite[0];
            public ProgressBar[] ProgressBars = new ProgressBar[0];
        }
        #endregion
    }
}
