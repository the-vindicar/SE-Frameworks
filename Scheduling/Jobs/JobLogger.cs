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
    class JobLogger : IHasOutput
    {
        public string ID { get; set; } = "Log";
        public Color FontColor = Color.Transparent;
        public Color BgColor = Color.Transparent;
        Scheduler Owner;
        ScreenManager Manager;
        public JobLogger(Scheduler owner, ScreenManager manager, int maxlines = 50)
        {
            Owner = owner; Manager = manager;
            MaxLines = maxlines;
            Owner.Log += AddLine;
            Owner.Loading += Load;
            Owner.Saving += Save;
            Owner.Fault += Fault;
            Owner.RegisterCommand(ID, Command);
        }
        void Fault(Exception e)
        {
            AddLine($"*** FAULT ***\n{e.GetType().Name}: {e.Message}\n{e.StackTrace}\n");
            FontColor = Color.White;
            BgColor = Color.Red;
            try { Manager.ForceUpdateFor(this); }
            catch { }
            var s = Owner.PB.Me.GetSurface(0);
            s.ContentType = ContentType.TEXT_AND_IMAGE;
            s.BackgroundColor = BgColor;
            s.WriteText(Buffer, false);
        }
        const string HELP =
@"Available subcommands:
  help  - show this message
  clear - clear the log
  write - log a message
";
        void Command(MyCommandLine args)
        {
            switch (args.Argument(1))
            {
                case "write": AddLine(args.Argument(2)); break;
                case "clear": Buffer.Clear(); break;
                case "help": Owner.Log(HELP); break;
                default: Owner.Log($"Unknown command: {args.ToString()}"); break;
            }
        }
        #region Text control
        StringBuilder Buffer = new StringBuilder();
        int MaxLines;

        int FindLastLines(StringBuilder sb, int lines)
        {
            int idx;
            int counter = lines;
            for (idx = sb.Length - 1; (idx >= 0) && (counter > 0); idx--)
                if (sb[idx] == '\n')
                    counter--;
            return idx + 1;
        }

        void AddLine(string line)
        {
            Buffer.Append(line);
            Buffer.Append('\n');
            int idx = FindLastLines(Buffer, MaxLines);
            if (idx >= 0)
                Buffer.Remove(0, idx);
        }

        public string LastLines(int N = -1)
        {
            if (N < 0)
                return Buffer.ToString();
            else
            {
                int idx = FindLastLines(Buffer, N);
                return Buffer.ToString(idx, Buffer.Length - idx);
            }
        }

        #endregion
        #region State save/load
        void Load(MyIni state) { Buffer.Append(state.Get(ID, "Buffer").ToString()).Append('\n'); }
        void Save(MyIni state) { state.Set(ID, "Buffer", Buffer.ToString()); }

        public bool TryParseMode(string mode, out object data)
        {
            data = null;
            return string.IsNullOrEmpty(mode);
        }

        public string SerializeMode(object data)
        {
            return string.Empty;
        }

        public void Render(Window window, StringBuilder text, ref MySpriteDrawFrame frame)
        {
            text.Append('\n').Append(Buffer).Append('\n');
            if (BgColor != Color.Transparent)
                frame.Add(window.Surface.FitSprite("SquareSimple", window.Area, BgColor));
            Color textcolor = FontColor == Color.Transparent ? window.Surface.ScriptForegroundColor : FontColor;
            frame.Add(window.Surface.FitText(Buffer.ToString(), window.Area, "Monospace", textcolor, TextAlignment.LEFT));
        }
        #endregion
    }
}
