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
    class JobPowerMonitor : IHasOutput
    {
        public string ID { get; set; } = "Power";
        Scheduler Owner;
        ScreenManager Manager;
        JobBattery Monitor;
        public JobPowerMonitor(Scheduler owner, ScreenManager manager, JobBattery monitor) 
        {
            Owner = owner;
            Manager = manager;
            Monitor = monitor;
        }

        public void Render(Window window, StringBuilder text, ref MySpriteDrawFrame frame)
        {
            ScreenContent content = window.GetData<ScreenContent>();
            if (Monitor.Updating || content == null) return;
            if (content.Total)
                RenderTotal(window, content);
            else
                RenderAll(window, content);
            frame.AddRange(content.StaticSprites);
            foreach (var pb in content.ProgressBars)
                frame.AddRange(pb);
        }

        void RenderTotal(Window window, ScreenContent content)
        {
            if (content.StaticSprites.Length != 3 || content.ProgressBars.Length != 3)
            {
                content.StaticSprites = new MySprite[3];
                content.ProgressBars = new ProgressBar[3];
                content.StaticSprites[0] = window.Surface.FitText("Discharged in 00h 00m",
                    window.Area.SubRect(0.05f, 0.04f, 0.9f, 0.2f), 
                    "Debug", window.Surface.ScriptForegroundColor);
                content.ProgressBars[0] = new ProgressBar(window.Surface,
                    window.Area.SubRect(0.05f, 0.28f, 0.9f, 0.2f),
                    Color.Blue, window.Surface.ScriptBackgroundColor, window.Surface.ScriptForegroundColor,
                    "{0:P1}", "Debug");
                content.StaticSprites[1] = window.Surface.FitText("Consumption: 000000MWh",
                    window.Area.SubRect(0.05f, 0.52f, 0.4f, 0.2f), "Debug", window.Surface.ScriptForegroundColor);
                content.StaticSprites[2] = window.Surface.FitText("Production: 000000MWh",
                    window.Area.SubRect(0.55f, 0.52f, 0.4f, 0.2f), "Debug", window.Surface.ScriptForegroundColor);
                content.ProgressBars[1] = new ProgressBar(window.Surface,
                    window.Area.SubRect(0.05f, 0.76f, 0.45f, 0.2f),
                    Color.Red, window.Surface.ScriptBackgroundColor, window.Surface.ScriptForegroundColor,
                    null, "Debug", TextAlignment.RIGHT);
                content.ProgressBars[2] = new ProgressBar(window.Surface,
                    window.Area.SubRect(0.5f, 0.76f, 0.45f, 0.2f),
                    Color.Green, window.Surface.ScriptBackgroundColor, window.Surface.ScriptForegroundColor,
                    null, "Debug", TextAlignment.LEFT);
            }
            float delta = Monitor.Input.Current - Monitor.Output.Current;
            string fmt;
            float time;
            if (Math.Abs(delta) > 0.1)
            {
                float capacity = delta < 0 ? Monitor.Charge.Current : Monitor.Charge.Unused;
                time = capacity / Math.Abs(delta);
                fmt = delta > 0 ? "'Recharged in '%h'h '%m'm'" : "'Discharged in '%h'h '%m'm'";
                content.ProgressBars[0].ForegroundColor = delta > 0 ? Color.Green : Color.Red;
            }
            else
            {
                fmt = "'Charge stable'";
                time = 0;
                content.ProgressBars[0].ForegroundColor = Color.Blue;
            }
            content.ProgressBars[0].Value = Monitor.ChargeRatio;
            content.StaticSprites[0].Data = TimeSpan.FromHours(time).ToString(fmt);
            content.ProgressBars[1].Value = Monitor.Output.GetRatio(1.0f);
            content.StaticSprites[1].Data = $"Consumption: {Monitor.Output.Current:F1}MWh";
            content.ProgressBars[2].Value = Monitor.Input.GetRatio(1.0f);
            content.StaticSprites[2].Data = $"Production: {Monitor.Input.Current:F1}MWh";
            foreach (var pb in content.ProgressBars) pb.PollColors();
        }

        void RenderAll(Window window, ScreenContent content)
        {
            int count = Monitor.Batteries.Count;
            if (content.StaticSprites.Length != 1 || content.ProgressBars.Length != count)
            {
                content.StaticSprites = new MySprite[1];
                content.StaticSprites[0] = window.Surface.FitText("Battery Status", window.Area.SubRect(0.1f, 0.0f, 0.8f, 0.15f), "Debug", window.Surface.ScriptForegroundColor);
                content.ProgressBars = new ProgressBar[count];
                int i = 0;
                foreach (var a in window.Surface.MakeTable(count, 2.0f, new Vector2(0.01f, 0.01f), new Vector2(0.01f, 0.01f), window.Area.SubRect(0.0f, 0.2f, 1.0f, 0.8f)))
                {
                    string fmt = Monitor.Batteries[i].Battery.IsSameConstructAs(Owner.PB.Me)
                        ? " {0:P1} " : "[{0:P1}]";
                    content.ProgressBars[i] = new ProgressBar(window.Surface, a,
                        window.Surface.ScriptForegroundColor, Color.Black, window.Surface.ScriptForegroundColor,
                        fmt);
                    i++;
                }
            }
            for (int i = 0; i < count; i++)
            {
                content.StaticSprites[0].Color = window.Surface.ScriptForegroundColor;
                float delta = Monitor.Batteries[i].Input.Current - Monitor.Batteries[i].Output.Current;
                if (Math.Abs(delta) > 0.1)
                    content.ProgressBars[i].ForegroundColor = (delta > 0) ? Color.Green : Color.Red;
                else
                    content.ProgressBars[i].ForegroundColor = Color.Blue;
                content.ProgressBars[i].PollColors();
                content.ProgressBars[i].Value = Monitor.Batteries[i].Charge.GetRatio();
            }
        }

        public bool TryParseMode(string mode, out object data)
        {
            if (string.Equals(mode, "total", StringComparison.CurrentCultureIgnoreCase))
                data = new ScreenContent(true);
            else if (string.Equals(mode, "each", StringComparison.CurrentCultureIgnoreCase))
                data = new ScreenContent(false);
            else
                data = null;
            return data != null;
        }

        public string SerializeMode(object data)
        {
            return data.ToString();
        }

        class ScreenContent
        {
            public ScreenContent(bool total) { Total = total; }
            public override string ToString() { return Total ? "total" : "each"; }
            public bool Total;
            public MySprite[] StaticSprites = new MySprite[0];
            public ProgressBar[] ProgressBars = new ProgressBar[0];
        }
    }
}
