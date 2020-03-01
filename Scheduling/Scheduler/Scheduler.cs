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
    /// <summary>This class provides an event-driven programming interface for job objects to use.</summary>
    class Scheduler
    {
        public readonly Program PB;
        /// <summary>Creates an instance of the scheduler.</summary>
        /// <param name="program">Program instance to link to.</param>
        public Scheduler(Program program)
        {
            updateFrequency = UpdateFrequency.None;
            PB = program;
            Groups = GroupBlocks;
            Log += PB.Echo;
            RegisterCommand("help", ListCommands);
            RegisterCommand("scangrid", ScanGrid);
            Once += FirstTickRun;
            PB.Runtime.UpdateFrequency = updateFrequency;
            for (int i = StaggerBins.Length - 1; i >= 0; i--)
                StaggerBins[i] = new List<Action<UpdateFrequency>>();
        }
        /// <summary>Main tick method. Call it from Program.Main().</summary>
        public void Update(string argument, UpdateType source)
        {
            try
            {
                if ((source & UpdateType.Once) != 0)
                {
                    Action<UpdateFrequency> once = OnOnce;
                    OnOnce = null;
                    updateFrequency &= ~UpdateFrequency.Once;
                    once?.Invoke(UpdateFrequency.Once);
                }
                if ((source & UpdateType.Update1) != 0) OnTick1?.Invoke(UpdateFrequency.Update1);
                if ((source & UpdateType.Update10) != 0) OnTick10?.Invoke(UpdateFrequency.Update10);
                if ((source & UpdateType.Update100) != 0) OnTick100?.Invoke(UpdateFrequency.Update100);
                if ((source & (UpdateType.Terminal | UpdateType.Script | UpdateType.Trigger)) != 0)
                    ExecuteCommand(argument);
                PB.Runtime.UpdateFrequency = updateFrequency;
            }
            catch (Exception e)
            {
                Fault?.Invoke(e);
                throw;
            }
        }
        /// <summary>
        /// Use this action to write a line to the system log, 
        /// or subscribe to it to handle logging yourself.
        /// By default Echo() is used.
        /// </summary>
        public Action<string> Log;
        /// <summary>
        /// Triggered if an unhandled exception occurs during execution, and PB is about to halt.
        /// Jobs that control blocks should ensure those blocks are left in a safe state (i.e. turned off).
        /// </summary>
        public event Action<Exception> Fault;

        void FirstTickRun(UpdateFrequency freq)
        {   //runs at the first tick (but after Program() constructor finishes)
            LoadState(); //Notify jobs that saved state is available
            ScanGrid(); //Schedule a grid scan
        }
        #region Saving & restoring state
        MyIni SavedState = new MyIni();
        /// <summary>This event is triggered when PB has loaded its saved state.</summary>
        public event Action<MyIni> Loading;
        /// <summary>This event is triggered when PB is saving its state.</summary>
        public event Action<MyIni> Saving;
        /// <summary>
        /// Notifies all subscribed jobs that they need to load their state. 
        /// <para>Done automatically on first tick after recompile/reload, but can be done manually as well.</para>
        /// </summary>
        public void LoadState()
        {
            SavedState.Clear();
            if (string.IsNullOrWhiteSpace(PB.Storage) || !SavedState.TryParse(PB.Storage))
                SavedState.TryParse(PB.Me.CustomData);
            Loading?.Invoke(SavedState);
        }
        /// <summary>
        /// Notifies all subscribed jobs that they need to save their state. Should be called by Program.Save().
        /// <para>Warning: state is saved into a single shared <see cref="MyIniFile"/>.</para>
        /// </summary>
        bool DoSave = true;
        public void SaveState()
        {
            SavedState.Clear();
            if (DoSave) Saving?.Invoke(SavedState);
            PB.SetStorage(SavedState.ToString()); //using mix-in method
        }
        /// <summary>If called, block state won't be saved on next shutdown.</summary>
        public void Reset(MyCommandLine cmd)
        {
            DoSave = false;
            Log("State reset scheduled.");
        }
        #endregion
        #region Blocks & groups
        /// <summary>Triggered every time grid scan returns a block group.</summary>
        public event Action<GridScanArgs<KeyValuePair<IMyBlockGroup, List<IMyTerminalBlock>>>> GroupFound;
        /// <summary>Triggered every time grid scan returns a terminal block.</summary>
        public event Action<GridScanArgs<IMyTerminalBlock>> BlockFound;
        /// <summary>Groups found during last scan and their corresponding blocks.</summary>
        public IReadOnlyDictionary<IMyBlockGroup, IReadOnlyList<IMyTerminalBlock>> Groups;
        /// <summary>
        /// Schedule a grid scan. It will happen over the course of several next ticks, depending on how large the grid is. <para/>
        /// This version can be registered as a command, and will cause a message "Grid scan complete" to be logged.
        /// </summary>
        public void ScanGrid(MyCommandLine args)
        {
            if (GroupFeed == null || BlockFeed == null)
            {
                Log("Initiating grid scan...");
                BlockFound += GridScanComplete;
                ScanGrid();
            }
        }
        void GridScanComplete(GridScanArgs<IMyTerminalBlock> item)
        {
            if (item.Last)
            {
                Log("Grid scan complete.");
                BlockFound -= GridScanComplete;
            }
        }
        /// <summary>Schedule a grid scan. It will happen over the course of several next ticks, depending on how large the grid is.</summary>
        public void ScanGrid()
        {
            if (GroupFeed != null || BlockFeed != null)
                return; //grid scan already in progress, ignore
            GroupList.Clear();
            PB.GridTerminalSystem.GetBlockGroups(GroupList);
            GroupFeed = FeedList(GroupList, GroupScanTick).GetEnumerator();
            Blocks.Clear();
            PB.GridTerminalSystem.GetBlocks(Blocks);
            BlockFeed = FeedList(Blocks, BlockFound).GetEnumerator();
            Tick1 += RescanTick;
        }
        void RescanTick(UpdateFrequency freq)
        {   //Feed next part of group/block lists to consumers.
            if (GroupFeed != null)
                if (!GroupFeed.MoveNext())
                    GroupFeed = null;
            if (GroupFeed == null && BlockFeed != null)
                if (!BlockFeed.MoveNext())
                    BlockFeed = null;
            if (GroupFeed == null && BlockFeed == null)
            {   //once done, we disable ourselves.
                Tick1 -= RescanTick;
                GroupList.Clear();
                Blocks.Clear();
                //we keep the group dictionary, tho
            }
        }
        void GroupScanTick(GridScanArgs<IMyBlockGroup> info)
        {
            if (!GroupBlocks.ContainsKey(info.Item))
                GroupBlocks.Add(info.Item, new List<IMyTerminalBlock>());
            var list = GroupBlocks[info.Item] as List<IMyTerminalBlock>;
            list.Clear();
            info.Item.GetBlocks(list);
            var kv = new KeyValuePair<IMyBlockGroup, List<IMyTerminalBlock>>(info.Item, list);
            var args = new GridScanArgs<KeyValuePair<IMyBlockGroup, List<IMyTerminalBlock>>>(kv, info.First, info.Last);
            GroupFound?.Invoke(args);
        }
        List<IMyBlockGroup> GroupList = new List<IMyBlockGroup>(); //list of available groups
        List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>(); //list of available blocks
        Dictionary<IMyBlockGroup, IReadOnlyList<IMyTerminalBlock>> GroupBlocks = new Dictionary<IMyBlockGroup, IReadOnlyList<IMyTerminalBlock>>();
        IEnumerator GroupFeed = null; //iterator marking current state of group feeding
        IEnumerator BlockFeed = null; //iterator marking current state of block feeding
        IEnumerable FeedList<T>(ICollection<T> list, Action<GridScanArgs<T>> handler, int slice = 1000)
        {   //Feeds list of elements to a consumer delegate, slice by slice
            int i = 0;
            int Target;
            Action<GridScanArgs<T>> handler_copy = handler;
            if (handler_copy != null)
            {
                IEnumerator<T> enumerator = list.GetEnumerator();
                int maxlist = list.Count - 1;
                while (i <= maxlist)
                {
                    Target = Math.Min(i + slice, maxlist + 1);
                    for (; i < Target; i++)
                    {
                        enumerator.MoveNext();
                        handler_copy?.Invoke(new GridScanArgs<T>(enumerator.Current, i == 0, i == maxlist));
                    }
                    yield return null;
                }
            }
        }
        /// <summary>Convenience method for a job to check if this job should use this particular block.</summary>
        /// <param name="policy">Structure describing block usage policy. See <see cref="GridPolicy"/>.</param>
        /// <param name="block">Block to be tested.</param>
        /// <returns>True if block matches the policy.</returns>
        public bool PolicyCheck(GridPolicy policy, IMyTerminalBlock block)
        {
            bool success = true;
            success = success && (((policy.Type & GridPolicy.Types.SameOwner) == 0) || (block.OwnerId == PB.Me.OwnerId));
            success = success && (((policy.Type & GridPolicy.Types.SameConstruct) == 0) || PB.Me.IsSameConstructAs(block));
            success = success && (((policy.Type & GridPolicy.Types.SameGrid) == 0) || (block.CubeGrid == PB.Me.CubeGrid));
            if (success && (policy.Type & GridPolicy.Types.BlockName) != 0)
                success = success && string.Equals(block.CustomName, policy.Name, StringComparison.CurrentCultureIgnoreCase);
            else if (success && (policy.Type & GridPolicy.Types.BlockTag) != 0)
                success = success && (block.CustomName.IndexOf(policy.Name, StringComparison.CurrentCultureIgnoreCase) >= 0);
            else if (success && (policy.Type & GridPolicy.Types.GroupName) != 0)
            {
                success = false;
                foreach (var kv in GroupBlocks)
                    if (string.Equals(policy.Name, kv.Key.Name, StringComparison.CurrentCultureIgnoreCase) && kv.Value.Contains(block))
                    {
                        success = true;
                        break;
                    }
            }
            else if (success && (policy.Type & GridPolicy.Types.GroupTag) != 0)
            {
                success = false;
                foreach (var kv in GroupBlocks)
                    if (kv.Key.Name.IndexOf(policy.Name, StringComparison.CurrentCultureIgnoreCase) >= 0 && kv.Value.Contains(block))
                    {
                        success = true;
                        break;
                    }
            }
            return success;
        }
        /// <summary>Convenience method for a job to check if this job should use this particular block group.</summary>
        /// <param name="policy">Structure describing block usage policy. See <see cref="GridPolicy"/>.</param>
        /// <param name="group">Block group to be tested.</param>
        /// <returns>True if group matches the policy.</returns>
        public bool PolicyCheck(GridPolicy policy, IMyBlockGroup group)
        {
            bool success = true;
            success = success && (((policy.Type & GridPolicy.Types.SameOwner) == 0) || Groups[group].All((b) => PB.Me.OwnerId == b.OwnerId));
            success = success && (((policy.Type & GridPolicy.Types.SameConstruct) == 0) || Groups[group].All((b) => PB.Me.IsSameConstructAs(b)));
            success = success && (((policy.Type & GridPolicy.Types.SameGrid) == 0) || Groups[group].All((b) => PB.Me.CubeGrid == b.CubeGrid));
            if (success && (policy.Type & GridPolicy.Types.GroupName) != 0)
                success = string.Equals(policy.Name, group.Name, StringComparison.CurrentCultureIgnoreCase);
            else if (success && (policy.Type & GridPolicy.Types.GroupTag) != 0)
                success = group.Name.IndexOf(policy.Name, StringComparison.CurrentCultureIgnoreCase) >= 0;
            return success;
        }
        #endregion
        #region Tick handlers
        UpdateFrequency updateFrequency;
        Action<UpdateFrequency> OnOnce;
        /// <summary>
        /// Subscribe to have PB activate on next tick and run your handler.
        /// This event automatically clears out subscribed handlers when they are called.
        /// </summary>
        public event Action<UpdateFrequency> Once
        {
            add { if (OnOnce == null) updateFrequency |= UpdateFrequency.Once; OnOnce += value; }
            remove { if ((OnOnce -= value) == null) updateFrequency &= ~UpdateFrequency.Once; }
        }
        Action<UpdateFrequency> OnTick1;
        /// <summary>Subscribe to have PB activate on every tick and run your handler.</summary>
        public event Action<UpdateFrequency> Tick1
        {
            add { if (OnTick1 == null) updateFrequency |= UpdateFrequency.Update1; OnTick1 += value; }
            remove { if ((OnTick1 -= value) == null) updateFrequency &= ~UpdateFrequency.Update1; }
        }
        Action<UpdateFrequency> OnTick10;
        /// <summary>Subscribe to have PB activate on every 10th tick and run your handler.</summary>
        public event Action<UpdateFrequency> Tick10
        {
            add { if (OnTick10 == null) updateFrequency |= UpdateFrequency.Update10; OnTick10 += value; }
            remove { if ((OnTick10 -= value) == null) updateFrequency &= ~UpdateFrequency.Update10; }
        }
        Action<UpdateFrequency> OnTick100;
        /// <summary>Subscribe to have PB activate on every 100th tick and run your handler.</summary>
        public event Action<UpdateFrequency> Tick100
        {
            add { if (OnTick100 == null) updateFrequency |= UpdateFrequency.Update100; OnTick100 += value; }
            remove { if ((OnTick100 -= value) == null) updateFrequency &= ~UpdateFrequency.Update100; }
        }
        public string Subscribe(Action<UpdateFrequency> handler, string tick, string default_tick = null)
        {
            if (string.Equals(tick, "update1", StringComparison.OrdinalIgnoreCase)) Tick1 += handler;
            else if (string.Equals(tick, "update10", StringComparison.OrdinalIgnoreCase)) Tick10 += handler;
            else if (string.Equals(tick, "update100", StringComparison.OrdinalIgnoreCase)) Tick100 += handler;
            else if (string.Equals(tick, "update100s", StringComparison.OrdinalIgnoreCase)) Tick100S += handler;
            else if (!string.IsNullOrEmpty(default_tick)) return Subscribe(handler, default_tick, null);
            else throw new ArgumentException("Invalid tick setting", "tick");
            return tick;
        }
        public bool Unsubscribe(Action<UpdateFrequency> handler, string tick)
        {
            if (string.Equals(tick, "update1", StringComparison.OrdinalIgnoreCase)) Tick1 -= handler;
            else if (string.Equals(tick, "update10", StringComparison.OrdinalIgnoreCase)) Tick10 -= handler;
            else if (string.Equals(tick, "update100", StringComparison.OrdinalIgnoreCase)) Tick100 -= handler;
            else if (string.Equals(tick, "update100s", StringComparison.OrdinalIgnoreCase)) Tick100S -= handler;
            else return false;
            return true;
        }
        #endregion
        #region Staggered ticks
        int StaggerIndex = 0;
        List<Action<UpdateFrequency>>[] StaggerBins = new List<Action<UpdateFrequency>>[10];
        /// <summary>
        /// Subscribing to this event will cause the handler to run approximately every 100 ticks.
        /// But different handlers will be triggered during different 10-tick intervals.<para/>
        /// Warning: this event uses Update10 frequency.
        /// </summary>
        public event Action<UpdateFrequency> Tick100S
        {
            add
            {
                bool was_empty = StaggerBins.All((b) => b.Count == 0);
                int minidx = 0; //find least used interval
                for (int i = StaggerBins.Length - 1; i > 0; i--)
                    if (StaggerBins[i].Count < StaggerBins[minidx].Count)
                        minidx = i;
                StaggerBins[minidx].Add(value);
                if (was_empty) Tick10 += DoTick100S;
            }
            remove
            {
                for (int i = StaggerBins.Length - 1; i >= 0; i--)
                    if (StaggerBins[i].Remove(value))
                        return;
                if (StaggerBins.All((b) => b.Count == 0)) Tick10 -= DoTick100S;
            }
        }

        void DoTick100S(UpdateFrequency freq)
        {
            foreach (var h in StaggerBins[StaggerIndex])
                h(UpdateFrequency.Update100);
            StaggerIndex = (StaggerIndex + 1) % StaggerBins.Length;
        }
        #endregion
        #region Commands
        MyCommandLine CmdLine = new MyCommandLine();
        SortedDictionary<string, Action<MyCommandLine>> Commands = new SortedDictionary<string, Action<MyCommandLine>>(StringComparer.CurrentCultureIgnoreCase);
        /// <summary>
        /// Registers a command that triggers when PB is activated via terminal, antenna or other block.
        /// First token of the argument string will be treated as command name.
        /// </summary>
        /// <param name="cmd">Command name.</param>
        /// <param name="handler">Command handler.</param>
        public void RegisterCommand(string cmd, Action<MyCommandLine> handler) { Commands.Add(cmd, handler); }
        /// <summary>Remove a previously registered command.</summary>
        /// <param name="cmd">Command name.</param>
        public void UnregisterCommand(string cmd) { Commands.Remove(cmd); }
        /// <summary>Outputs a list of available commands into system log. You can register this as a command of your choosing.</summary>
        public void ListCommands(MyCommandLine args) { Log($"Available commands: {string.Join(", ", Commands.Keys)}"); }
        /// <summary>Executes a command as if it was entered by user.</summary>
        public void ExecuteCommand(string command)
        {
            Log($">{command}");
            Action<MyCommandLine> handler;
            if (CmdLine.TryParse(command) && Commands.TryGetValue(CmdLine.Items[0], out handler))
                handler?.Invoke(CmdLine);
            else
                Log($"Invalid command");
        }
        #endregion
    }
}
