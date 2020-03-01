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
    /// <summary>Any object that implements this interface can be used by the screen manager as an output provider.</summary>
    interface IHasOutput
    {
        /// <summary>Human-readable string identifying the output provider.</summary>
        string ID { get; }
        /// <summary>Tries to convert a string into a provider-defined data object describing the output (settings, sprite cache, etc).</summary>
        /// <param name="mode">Input string</param>
        /// <param name="data">Output data object. Using structs is not recommended due to boxing.</param>
        /// <returns>True if parsing succeeded.</returns>
        bool TryParseMode(string mode, out object data);
        /// <summary>
        /// Converts a provider-defined data object to a string representation that can be used by TryParseMode().
        /// </summary>
        /// <param name="data">Data object to serialize.</param>
        /// <returns>String representation.</returns>
        string SerializeMode(object data);
        /// <summary>Renders sprites for the specified window and uses given DrawFrame to display them.</summary>
        /// <param name="window">Window object defining surface, area of interest, data object, etc.</param>
        /// <param name="text">Text representation of the content can be added here for ease of copying.</param>
        /// <param name="frame">DrawFrame object to display the sprites with.</param>
        void Render(Window window, StringBuilder text, ref MySpriteDrawFrame frame);
    }
    /// <summary>Defines an output area on an IMyTextSurface that can be used by an output provider.</summary>
    class Window
    {
        /// <summary>Surface to be used for output.</summary>
        public readonly IMyTextSurface Surface;
        /// <summary>Area of the surface that should be used by this window.</summary>
        public RectangleF Area { get { return Location.Area; } }
        /// <summary>Location of this window - block, surface id, area.</summary>
        public WindowLocation Location;
        /// <summary>Provider-defined data object describing the contents of the window.</summary>
        public object Data;
        /// <summary>Output provider associated with this window.</summary>
        public readonly IHasOutput Job;
        /// <summary>Constructor - only to be used by screen manager.</summary>
        public Window(WindowLocation loc, IMyTextSurface surface, object data, IHasOutput job)
        {
            Location = loc; Surface = surface; Data = data; Job = job;
        }
        public T GetData<T>() where T : class { return Data as T; }
    }
    /// <summary>Describes a location of a window: block, surface id, area.</summary>
    struct WindowLocation
    {
        public long BlockId;
        public int SurfaceId;
        public RectangleF Area;
        public WindowLocation(long block, int surf, RectangleF area)
        { BlockId = block; SurfaceId = surf; Area = area; }
        public WindowLocation(IMyTextSurfaceProvider source, int id, RectangleF area)
        { BlockId = (source as IMyEntity).EntityId; SurfaceId = id; Area = area; }
        public static bool TryParse(string s, out WindowLocation loc)
        {
            string[] parts = s.Split('/');
            loc = new WindowLocation();
            return (parts.Length == 6)
                && long.TryParse(parts[0], out loc.BlockId)
                && int.TryParse(parts[1], out loc.SurfaceId)
                && float.TryParse(parts[2], out loc.Area.Position.X)
                && float.TryParse(parts[3], out loc.Area.Position.Y)
                && float.TryParse(parts[4], out loc.Area.Size.X)
                && float.TryParse(parts[5], out loc.Area.Size.Y);
        }
        public override string ToString()
        {
            return $"{BlockId}/{SurfaceId}/{Area.Position.X}/{Area.Position.Y}/{Area.Size.X}/{Area.Size.Y}";
        }
    }
    /// <summary>
    /// Manages output providers and their associated windows.
    /// </summary>
    class ScreenManager
    {
        public string ID = "Screen";
        Scheduler Owner;
        public ScreenManager(Scheduler owner)
        {
            Owner = owner;
            Owner.BlockFound += TryAttachScreen;
            Owner.Loading += Load;
            Owner.Saving += Save;
            Owner.RegisterCommand(ID, Command);
            Updater = Screens.GetEnumerator();
        }
        const string Help =
@"Control screen output. Subcommands:
  help - show this message;
  add SurfaceProvider/0 0;0;1;1 JobID [mode] - add output window on screen area.
  set SurfaceProvider/0 JobID [mode] - replace content of the screen with output.
  clear SurfaceProvider/0 [0;0;1;1] - remove all output windows intersecting area.
  jobs - list available job IDs.
";
        public static ScreenManager operator+(ScreenManager manager, IHasOutput job)
        {   //Just a shorthand to associate a provider with a manager instance.
            manager.Jobs.Add(job.ID, job);
            return manager;
        }
        public IDictionary<string, IHasOutput> Jobs = new SortedDictionary<string, IHasOutput>(StringComparer.CurrentCultureIgnoreCase);
        void Command(MyCommandLine cmd)
        {
            switch (cmd.Items[1])
            {
                case "help": Owner.Log(Help); break;
                case "add":
                    {   //add command lets you put a window onto a screen
                        string surfaddr = cmd.Items[2];
                        string areacoords = cmd.Items[3];
                        string jobid = cmd.Items[4];
                        string mode = cmd.Items[5];
                        IMyTerminalBlock block;
                        int surfidx;
                        RectangleF area;
                        if (!Jobs.ContainsKey(jobid))
                            Owner.Log($"Unknown job '{jobid}'.");
                        else if (!LookupSurface(surfaddr, Owner.PB.GridTerminalSystem, out block, out surfidx))
                            Owner.Log($"Failed to find surface '{surfaddr}'.");
                        else if (!ReadArea(areacoords, out area))
                            Owner.Log($"'{areacoords}' is not a valid area description.");
                        else
                            AddWindow(block as IMyTextSurfaceProvider, surfidx, area, Jobs[jobid], mode);
                    }; break;
                case "set":
                    {   //set command replaces content of the screen with a window
                        string surfaddr = cmd.Items[2];
                        string jobid = cmd.Items[3];
                        string mode = cmd.Items[4];
                        IMyTerminalBlock block;
                        int surfidx;
                        if (!Jobs.ContainsKey(jobid))
                            Owner.Log($"Unknown job '{jobid}'.");
                        else if (!LookupSurface(surfaddr, Owner.PB.GridTerminalSystem, out block, out surfidx))
                            Owner.Log($"Failed to find surface '{surfaddr}'.");
                        else
                            SetScreen(block as IMyTextSurfaceProvider, surfidx, Jobs[jobid], mode);
                    }; break;
                case "clear":
                    {   //clear command removes windows in certain area (or clears entire screen)
                        string surfaddr = cmd.Items[2];
                        string areacoords = cmd.Items[3];
                        IMyTerminalBlock block;
                        int surfidx;
                        RectangleF area;
                        if (!LookupSurface(surfaddr, Owner.PB.GridTerminalSystem, out block, out surfidx))
                            Owner.Log($"Failed to find surface '{surfaddr}'.");
                        else if (ReadArea(areacoords, out area))
                            RemoveWindowsIn(block as IMyTextSurfaceProvider, surfidx, area);
                        else
                            ClearScreen(block as IMyTextSurfaceProvider, surfidx);
                    }; break;
                case "jobs":
                    Owner.Log(string.Join(", ", Jobs.Keys)); break;
                default:
                    Owner.Log($"Unknown command: '{cmd.Items[1]}'"); break;
            }
        }
        #region Save/Load state
        List<MyTuple<WindowLocation, string, string>> PendingScreens = new List<MyTuple<WindowLocation, string, string>>();
        void Save(MyIni state)
        {   //serialize each window, storing its location, provider ID and data object representation
            foreach (var ws in Screens.Values)
                foreach (Window w in ws)
                    state.Set(ID, w.Location.ToString(), w.Job.ID + "\n" + w.Job.SerializeMode(w.Data));
        }
        void Load(MyIni state)
        {
            WindowLocation loc;
            List<MyIniKey> keys = new List<MyIniKey>();
            state.GetKeys(ID, keys);
            PendingScreens.Clear();
            foreach (var key in keys)
                if (WindowLocation.TryParse(key.Name, out loc))
                {   //we store a would-be window into a buffer - we will create it once we find that text surface again.
                    string value = state.Get(key).ToString();
                    int idx = value.IndexOf('\n');
                    if (idx < 0)
                        PendingScreens.Add(new MyTuple<WindowLocation, string, string>(loc, value, string.Empty));
                    else
                        PendingScreens.Add(new MyTuple<WindowLocation, string, string>(loc, value.Substring(0, idx), value.Substring(idx + 1)));
                }
        }
        void TryAttachScreen(GridScanArgs<IMyTerminalBlock> item)
        {   //Check if we found a surface provider block that was used by one of our windows.
            if (item.First)
            {
                Owner.Tick10 -= ScreenUpdateTick;
            }
            var p = item.Item as IMyTextSurfaceProvider;
            if (p != null)
                for (int i = PendingScreens.Count - 1; i >= 0; i--)
                    if (PendingScreens[i].Item1.BlockId == item.Item.EntityId)
                    {   //found a matching block!
                        object data;
                        IHasOutput job;
                        if ((PendingScreens[i].Item1.SurfaceId <= p.SurfaceCount) //found a matching surface
                            && Jobs.TryGetValue(PendingScreens[i].Item2, out job) //found an output provider
                            && job.TryParseMode(PendingScreens[i].Item3, out data)) //managed to restore its data object
                        {
                            var surface = p.GetSurface(PendingScreens[i].Item1.SurfaceId);
                            if (!Screens.ContainsKey(surface)) //add to active screens if necessary
                                Screens[surface] = new List<Window>();
                            Screens[surface].Add(new Window(PendingScreens[i].Item1, surface, data, job));
                        }
                        PendingScreens.RemoveAt(i);
                    }
            if (item.Last)
            {
                PendingScreens.Clear(); //we forget any windows we couldn't restore
                Updater.Dispose();
                Updater = Screens.GetEnumerator();
                Owner.BlockFound -= TryAttachScreen; //as such, we won't need scanning the grid again
                Owner.Tick10 += ScreenUpdateTick;
            }
        }
        #endregion
        #region Screen/window control
        Dictionary<IMyTextSurface, List<Window>> Screens = new Dictionary<IMyTextSurface, List<Window>>();
        Dictionary<IMyTextSurface, List<Window>>.Enumerator Updater;
        /// <summary>Adds an output window to a specific screen surface. Does not affect other windows.</summary>
        /// <param name="block">Block that provides the surface</param>
        /// <param name="surf">Surface number</param>
        /// <param name="area">Area for the window to take up.</param>
        /// <param name="job">Output provider that will use this window.</param>
        /// <param name="mode">String describing the contents of the window (provider-defined).</param>
        /// <returns>Window object.</returns>
        public Window AddWindow(IMyTextSurfaceProvider block, int surf, RectangleF area, IHasOutput job, string mode)
        {
            object data;
            if (surf >= block.SurfaceCount)
                Owner.Log($"Surface #{surf} does not exist on '{(block as IMyTerminalBlock).CustomName}'");
            else if (!job.TryParseMode(mode, out data))
                Owner.Log($"'{mode}' is not a valid mode string for '{job.ID}'.");
            else
            {
                var surface = block.GetSurface(surf);
                if (!Screens.ContainsKey(surface))
                {
                    Screens[surface] = new List<Window>();
                    Updater.Dispose();
                    Updater = Screens.GetEnumerator();
                }
                var w = new Window(new WindowLocation(block, surf, area), surface, data, job);
                Screens[surface].Add(w);
                UpdateScreen(surface, Screens[surface]);
                return w;
            }
            return null;
        }
        /// <summary>Replaces contents of a specific screen with an output window.</summary>
        /// <param name="block">Block that provides the surface</param>
        /// <param name="surf">Surface number</param>
        /// <param name="job">Output provider that will use this window.</param>
        /// <param name="mode">String describing the contents of the window (provider-defined).</param>
        /// <returns>Window object.</returns>
        public Window SetScreen(IMyTextSurfaceProvider block, int surf, IHasOutput job, string mode)
        {
            RectangleF area = new RectangleF(0, 0, 1, 1);
            ClearScreen(block as IMyTextSurfaceProvider, surf);
            return AddWindow(block as IMyTextSurfaceProvider, surf, area, job, mode);
        }
        /// <summary>Removes specific window from whatever screen it's at the moment.</summary>
        /// <param name="w">Window to close.</param>
        public void RemoveWindow(Window w)
        {
            foreach (var kv in Screens)
                if (kv.Value.Contains(w))
                {
                    kv.Value.Remove(w);
                    if (kv.Value.Count == 0)
                        ClearScreen(kv.Key);
                    break;
                }
        }
        /// <summary>Removes all windows that intersect specific area on the given surface.</summary>
        /// <param name="block">Block that provides the surface</param>
        /// <param name="surf">Surface number</param>
        /// <param name="area">Any window that intersects this area will be removed.</param>
        public void RemoveWindowsIn(IMyTextSurfaceProvider block, int surf, RectangleF area)
        {
            RectangleF intersect;
            if (surf >= block.SurfaceCount)
                Owner.Log($"Surface #{surf} does not exist on '{(block as IMyTerminalBlock).CustomName}'");
            else
            {
                var surface = block.GetSurface(surf);
                if (Screens.ContainsKey(surface))
                {
                    var ws = Screens[surface];
                    if (ws.Count == ws.RemoveAll((w) => RectangleF.Intersect(ref area, ref w.Location.Area, out intersect)))
                        ClearScreen(block, surf);
                    else
                        UpdateScreen(surface, ws);
                }
            }
        }
        /// <summary>Removes all windows that belong to a specific output provider.</summary>
        /// <param name="job"></param>
        public void RemoveWindowsFor(IHasOutput job)
        {
            var keys = Screens.Keys.ToArray();
            foreach (var key in keys)
            {
                var ws = Screens[key];
                ws.RemoveAll((w) => ReferenceEquals(job, w.Job));
                if (ws.Count == 0) ClearScreen(key);
            }
        }
        /// <summary>
        /// Clears specified surface if it exists.
        /// </summary>
        /// <param name="block">Block that provides the surface</param>
        /// <param name="surf">Surface ID</param>
        public void ClearScreen(IMyTextSurfaceProvider block, int surf)
        {
            if (surf >= block.SurfaceCount)
                Owner.Log($"Surface #{surf} does not exist on '{(block as IMyTerminalBlock).CustomName}'");
            else
                ClearScreen(block.GetSurface(surf));
        }
        void ClearScreen(IMyTextSurface surface)
        {
            surface.ContentType = ContentType.NONE;
            surface.WriteText(string.Empty, false);
            if (Screens.Remove(surface))
            {
                Updater.Dispose();
                Updater = Screens.GetEnumerator();
            }
        }
        #endregion
        #region Screen updates
        /// <summary>
        /// Immediately redraws all screens that have windows belonging to the given output provider.<para/>
        /// Warning: not only this provider's windows will be redrawn!
        /// </summary>
        /// <param name="job"></param>
        public void ForceUpdateFor(IHasOutput job)
        {
            foreach (var kv in Screens)
                if (kv.Value.Any((w) => ReferenceEquals(w.Job, job)))
                    UpdateScreen(kv.Key, kv.Value);
        }

        StringBuilder TextBuffer = new StringBuilder();
        int UpdTick = 0;
        void ScreenUpdateTick(UpdateFrequency freq)
        {   //one screen is updated every 10 ticks, but any screen is updated not faster than every 100 ticks
            if (Updater.MoveNext())
                UpdateScreen(Updater.Current.Key, Updater.Current.Value);
            else if (UpdTick >= 10)
            {
                Updater.Dispose();
                Updater = Screens.GetEnumerator();
                UpdTick = 0;
            }
            UpdTick++;
        }
        void UpdateScreen(IMyTextSurface surface, List<Window> windows)
        {
            surface.ContentType = ContentType.SCRIPT;
            surface.Script = string.Empty;
            var df = surface.DrawFrame();
            foreach (var w in windows)
                try
                { w.Job.Render(w, TextBuffer, ref df); }
                catch
                {
                    df.Add(w.Surface.FitSprite("Cross", w.Area));
                    df.Add(w.Surface.FitText(w.Job.ID, w.Area, "Debug", Color.Yellow));
                }
            df.Dispose();
            surface.WriteText(TextBuffer, false);
            TextBuffer.Clear();
        }
        #endregion
        #region Screen/window description and location
        static bool LookupSurface(string addr, IMyGridTerminalSystem gts, out IMyTerminalBlock block, out int surfid)
        {   //finds a block and a surface that correspond to the given address string
            long blockid;
            block = null;
            surfid = 0;
            int sepidx = addr.LastIndexOf('/');
            if (sepidx < 0) sepidx = addr.Length;
            if (addr[0] != '@')
                block = gts.GetBlockWithName(addr.Substring(0, sepidx));
            else if (long.TryParse(addr.Substring(0, sepidx), out blockid))
                block = gts.GetBlockWithId(blockid);
            var p = block as IMyTextSurfaceProvider;
            if (p == null)
                return false;
            return (sepidx == addr.Length) || int.TryParse(addr.Substring(sepidx + 1), out surfid);
        }
        static bool ReadArea(string s, out RectangleF area)
        {   //parses an area definition string into a RectangleF
            if (string.IsNullOrWhiteSpace(s))
            {
                area = new RectangleF(0, 0, 1, 1);
                return true;
            }
            area = new RectangleF();
            string[] parts = s.Split(';');
            return (parts.Length == 4)
                && float.TryParse(parts[0], out area.Position.X)
                && float.TryParse(parts[1], out area.Position.Y)
                && float.TryParse(parts[2], out area.Size.X)
                && float.TryParse(parts[3], out area.Size.Y);
        }
        #endregion
    }
}
