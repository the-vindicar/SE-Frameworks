using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;
using VRage.Game.GUI.TextPanel;

namespace IngameScript
{
    static class RectangleF_Extension
    {
        public static RectangleF Unit = new RectangleF(0, 0, 1, 1);
        public static RectangleF SubRect(this RectangleF rect, float x, float y, float w, float h)
        {
            return new RectangleF(
                rect.X + x * rect.Width, rect.Y + y * rect.Height, 
                w * rect.Width, h * rect.Height);
        }
        public static RectangleF SubRectCentered(this RectangleF rect, float x, float y, float w, float h)
        {
            return new RectangleF(
                rect.X + x * rect.Width - 0.5f * w * rect.Width, 
                rect.Y + y * rect.Height - 0.5f * h * rect.Height, 
                w * rect.Width, h * rect.Height);
        }
        public static RectangleF Inflate(this RectangleF area, float x, float y)
        {
            return new RectangleF(area.X - area.Width * x, area.Y - area.Height * y, area.Width * (1+2*x), area.Height * (1+2*y));
        }
        public static float Ratio(this RectangleF area) { return area.Width / area.Height; }
        public static IEnumerable<RectangleF> Table(this RectangleF rect, int rows, int cols, Vector2? padding = null, Vector2? spacing = null)
        {
            if (rows <= 0 || cols <= 0) yield break;
            if (!padding.HasValue) padding = Vector2.Zero;
            if (!spacing.HasValue) spacing = Vector2.Zero;
            Vector2 cellSize;
            cellSize.X = (1.0f - 2 * padding.Value.X - (cols - 1) * spacing.Value.X) / cols;
            cellSize.Y = (1.0f - 2 * padding.Value.Y - (rows - 1) * spacing.Value.Y) / rows;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    yield return rect.SubRect(
                        c * cellSize.X + c * spacing.Value.X + padding.Value.X,
                        r * cellSize.Y + r * spacing.Value.Y + padding.Value.Y,
                        cellSize.X, cellSize.Y);
        }
        public static IEnumerable<RectangleF> FlowTable(this RectangleF area, 
           int Count, float w2hratio = 1.0f,
           Vector2? padding = null,
           Vector2? spacing = null
           )
        {
            if (Count <= 0) yield break;
            int Cols = Math.Max(1, (int)(Math.Sqrt(Count) / w2hratio * area.Ratio()));
            int Rows = Count / Cols + ((Count % Cols > 0) ? 1 : 0);
            int i = 0;
            foreach (RectangleF a in area.Table(Rows, Cols, padding, spacing))
                if (i < Count)
                {
                    yield return a;
                    i++;
                }
                else
                    yield break;
        }
    }

    static class TextSurfaceExtension_Size
    {
        private static StringBuilder TextSample = new StringBuilder("M");
        public static Vector2I GetSizeInCharacters(this IMyTextSurface surface)
        {
            Vector2 CharSize = surface.MeasureStringInPixels(TextSample, surface.Font, surface.FontSize);
            return new Vector2I(
                    (int)(surface.SurfaceSize.X * (1.0f - 0.02f * surface.TextPadding) / CharSize.X),
                    (int)(surface.SurfaceSize.Y * (1.0f - 0.01f * surface.TextPadding) / CharSize.Y)
                );
        }
        public static void ScaleFontToFit(this IMyTextSurface surface, int cols, int rows)
        {
            Vector2 CharSize = surface.MeasureStringInPixels(TextSample, surface.Font, 1);
            float width = surface.SurfaceSize.X * (1.0f - 0.02f * surface.TextPadding);
            float height = surface.SurfaceSize.Y * (1.0f - 0.01f * surface.TextPadding);
            if (cols == 0 || rows == 0)
                surface.FontSize = 1;
            else
                surface.FontSize = Math.Min(width / (cols * CharSize.X), height / (rows * CharSize.Y));
        }
    }
    static class TextSurfaceExtension_Positioning
    {
        public static MySprite FitSprite(this IMyTextSurface surface, string id, RectangleF roi, Color? color = null, float rotation = 0.0f)
        {
            Vector2 offset = (surface.TextureSize - surface.SurfaceSize) / 2;
            Vector2 pos = new Vector2(
                surface.SurfaceSize.X * (roi.X + roi.Size.X / 2) + offset.X, 
                surface.SurfaceSize.Y * (roi.Y + roi.Size.Y / 2) + offset.Y);
            Vector2 size = new Vector2(surface.SurfaceSize.X * roi.Size.X, surface.SurfaceSize.Y * roi.Size.Y);
            return new MySprite(SpriteType.TEXTURE, id, pos, size, color, null, TextAlignment.CENTER, rotation);
        }
        public static MySprite FitText(this IMyTextSurface surface, string text, RectangleF roi, string font, Color color, TextAlignment align = TextAlignment.CENTER)
        {
            Vector2 offset = (surface.TextureSize - surface.SurfaceSize) / 2;
            Vector2 pos = new Vector2(0, surface.SurfaceSize.Y * (roi.Y + roi.Size.Y / 2) + offset.Y);
            switch (align)
            {
                case TextAlignment.LEFT: pos.X = surface.SurfaceSize.X * roi.X + offset.X; break;
                case TextAlignment.CENTER: pos.X = surface.SurfaceSize.X * (roi.X + roi.Size.X / 2) + offset.X; break;
                case TextAlignment.RIGHT: pos.X = surface.SurfaceSize.X * (roi.X + roi.Size.X) + offset.X; break;
            }
            Vector2 size = new Vector2(surface.SurfaceSize.X * roi.Size.X, surface.SurfaceSize.Y * roi.Size.Y);
            Vector2 size1 = surface.MeasureStringInPixels(new StringBuilder(text), font, 1);
            float scale = Math.Min(size.X / size1.X, size.Y / size1.Y);
            pos.Y -= size1.Y * scale / 2;
            return new MySprite(SpriteType.TEXT, text, pos, null, color, font, align, scale);
        }
        public static void FitProgressBar(this IMyTextSurface surface, ref MySprite sprite, float ratio, RectangleF roi, TextAlignment align)
        {
            Vector2 offset = (surface.TextureSize - surface.SurfaceSize) / 2;
            Vector2 pos = new Vector2(
                surface.SurfaceSize.X * (roi.X + roi.Size.X / 2) + offset.X,
                surface.SurfaceSize.Y * (roi.Y + roi.Size.Y / 2) + offset.Y);
            Vector2 size = new Vector2(surface.SurfaceSize.X * roi.Size.X, surface.SurfaceSize.Y * roi.Size.Y);
            Vector2 size2 = new Vector2(size.X * ratio, size.Y);
            switch (align)
            {
                case TextAlignment.LEFT: pos.X -= (size.X - size2.X) / 2; break;
                case TextAlignment.RIGHT: pos.X += (size.X - size2.X) / 2; break;
                case TextAlignment.CENTER: break;
            }
            sprite.Size = size2;
            sprite.Position = pos;
        }

        public static float Ratio(this IMyTextSurface surface) { return surface.SurfaceSize.X / surface.SurfaceSize.Y; }

        public static IEnumerable<RectangleF> MakeTable(this IMyTextSurface surface,
           int Count, float w2hratio = 1.0f,
           Vector2? padding = null,
           Vector2? spacing = null,
           RectangleF? area = null
           )
        {
            if (!area.HasValue) area = new RectangleF(0, 0, 1, 1);
            return area.Value.FlowTable(Count, w2hratio / surface.Ratio(), padding, spacing);
        }
    }
    class ProgressBar : IEnumerable<MySprite>
    {
        RectangleF area;
        float _Value;
        MySprite Background;
        MySprite Border;
        MySprite Foreground;
        MySprite Text;
        IMyTextSurface Surface;

        public string Format;
        public TextAlignment Alignment;
        public float Value
        {
            get { return _Value; }
            set
            {
                if (_Value == value) return;
                _Value = value;
                if (float.IsNaN(_Value) || _Value < 0)
                    Text.Data = "N/A";
                else
                {
                    Surface.FitProgressBar(ref Foreground, Math.Min(1.0f, Math.Max(0f, _Value)), area, Alignment);
                    Text.Data = string.Format(Format, _Value);
                }
            }
        }

        public Color BackgroundColor
        {
            get { return Background.Color.Value; }
            set { Background.Color = value; }
        }

        public Color ForegroundColor
        {
            get { return Foreground.Color.Value; }
            set { Foreground.Color = value; Border.Color = value; }
        }

        public Color TextColor
        {
            get { return Text.Color.Value; }
            set { Text.Color = value; }
        }

        public ProgressBar(IMyTextSurface surface, RectangleF roi, Color fg, Color bg, Color txt, string fmt = null, string font = "Debug", TextAlignment align = TextAlignment.LEFT)
        {
            Surface = surface;
            area = roi;
            Format = fmt ?? "{0:P1}";
            Alignment = align;
            _Value = 0.0f;
            Background = Surface.FitSprite("SquareSimple", area, bg);
            Border = Surface.FitSprite("SquareHollow", area, fg);
            Foreground = Surface.FitSprite("SquareSimple", area, fg);
            Text = Surface.FitText(string.Format(Format, 1.0), area, font, txt);
        }
        public void PollColors()
        {
            BackgroundColor = Surface.ScriptBackgroundColor;
            TextColor = Surface.ScriptForegroundColor;
        }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        public IEnumerator<MySprite> GetEnumerator()
        {
            if (Background.Color.GetValueOrDefault(Color.Transparent) != Color.Transparent)
                yield return Background;
            if (!float.IsNaN(_Value) && (_Value > 0) &&
                (Foreground.Color.GetValueOrDefault(Color.Transparent) != Color.Transparent))
                yield return Foreground;
            if (Border.Color.GetValueOrDefault(Color.Transparent) != Color.Transparent)
                yield return Border;
            if (Text.Color.GetValueOrDefault(Color.Transparent) != Color.Transparent && !string.IsNullOrEmpty(Format))
                yield return Text;
        }
    }

}
