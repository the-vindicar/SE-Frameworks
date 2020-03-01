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
    /// <summary>Maintains registry of items stored in specified inventories and displays it on screens.</summary>
    class JobInventoryMonitor : IHasOutput
    {
        public string ID { get; set; } = "Inventory";
        public Color BgColor = Color.Transparent;
        ScreenManager Manager;
        JobInventory Tracker;
        /// <summary>This constructor uses provided paramaters to configure the job.</summary>
        /// <param name="manager">Screen manager instance to use.</param>
        /// <param name="tracker">Inventory tracker instance to use.</param>
        public JobInventoryMonitor(ScreenManager manager, JobInventory tracker)
        {
            Manager = manager;
            Tracker = tracker;
        }
        #region Screen updates
        StringBuilder Buffer = new StringBuilder();
        IDictionary<string, string> ValueStrings = new SortedDictionary<string, string>(StringComparer.CurrentCulture);
        public void Render(Window window, StringBuilder text, ref MySpriteDrawFrame frame)
        {
            if (Tracker.Updating) return;
            var categories = window.GetData<CategoryStorage>();
            if (categories == null) return;
            JobInventory.ItemTypeDescriptor.ItemCategory cat = categories.Categories;
            Buffer.Clear();
            ValueStrings.Clear();
            int name_w = 0;
            int val_w = 0;
            string val;
            foreach (var item in Tracker.Stock)
                if ((cat & item.Value.Category) == item.Value.Category)
                {
                    val = item.Value.FormatAmount();
                    ValueStrings[item.Value.Name] = val;
                    name_w = Math.Max(name_w, item.Value.Name.Length);
                    val_w = Math.Max(val_w, val.Length);
                }
            string catname = cat.ToString();
            int titlepad = name_w + 1 + val_w - catname.Length;
            if (titlepad < 0)
            {
                name_w += -titlepad;
                titlepad = 0;
            }
            Buffer.Append('-', titlepad / 2);
            Buffer.Append(catname);
            Buffer.Append('-', titlepad - titlepad / 2);
            Buffer.Append('\n');
            foreach (var item in ValueStrings)
            {
                Buffer.Append(item.Key);
                Buffer.Append(' ', (name_w - item.Key.Length) + 1 + (val_w - item.Value.Length));
                Buffer.Append(item.Value);
                Buffer.Append('\n');
            }
            if (BgColor != Color.Transparent)
                frame.Add(window.Surface.FitSprite("SquareSimple", window.Area, BgColor));
            frame.Add(window.Surface.FitText(Buffer.ToString(), window.Area, "Monospace", window.Surface.ScriptForegroundColor));
            Buffer.Clear();
            ValueStrings.Clear();
        }
        #endregion
        #region Save/Load state
        class CategoryStorage
        {
            public JobInventory.ItemTypeDescriptor.ItemCategory Categories;
        }
        public bool TryParseMode(string mode, out object data)
        {
            CategoryStorage cs = new CategoryStorage();
            data = Enum.TryParse(mode, true, out cs.Categories) ? cs : null;
            return data != null;
        }
        public string SerializeMode(object data) { return (data as CategoryStorage).Categories.ToString(); }
        #endregion
    }
}
