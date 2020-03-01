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
        /// <summary>Creates a sub-rectangle using relative coordinates (0 to 1).</summary>
        /// <param name="rect"></param>
        /// <param name="x">Offset of the left side.</param>
        /// <param name="y">Offset of the top side.</param>
        /// <param name="w">Part of parent rectangle's width.</param>
        /// <param name="h">Part of parent rectangle's height.</param>
        /// <returns></returns>
        public static RectangleF SubRect(this RectangleF rect, float x, float y, float w, float h)
        {
            return new RectangleF(
                rect.X + x * rect.Width, rect.Y + y * rect.Height, 
                w * rect.Width, h * rect.Height);
        }
        /// <summary>Creates a centered sub-rectangle using relative coordinates (0 to 1).</summary>
        /// <param name="rect"></param>
        /// <param name="x">Offset of the sub-rectangle's center from the left side.</param>
        /// <param name="y">Offset of the sub-rectangle's center from the top side.</param>
        /// <param name="w">Part of parent rectangle's width.</param>
        /// <param name="h">Part of parent rectangle's height.</param>
        /// <returns></returns>
        public static RectangleF SubRectCentered(this RectangleF rect, float x, float y, float w, float h)
        {
            return new RectangleF(
                rect.X + x * rect.Width - 0.5f * w * rect.Width, 
                rect.Y + y * rect.Height - 0.5f * h * rect.Height, 
                w * rect.Width, h * rect.Height);
        }
        /// <summary>
        /// Returns a copy of the rectangle increased on all sides by specified amount. 
        /// Use negative amount to decrease the rectangle instead.
        /// </summary>
        /// <param name="area"></param>
        /// <param name="x">Increase of width</param>
        /// <param name="y">Increase of height</param>
        /// <returns></returns>
        public static RectangleF Inflate(this RectangleF area, float x, float y)
        {
            return new RectangleF(area.X - area.Width * x, area.Y - area.Height * y, area.Width * (1+2*x), area.Height * (1+2*y));
        }
        /// <summary>
        /// Calculates aspect ratio of the rectangle.
        /// </summary>
        /// <param name="area"></param>
        /// <returns></returns>
        public static float Ratio(this RectangleF area) { return area.Width / area.Height; }
        /// <summary>
        /// Generates a table of equal cells and yields a rectangle corresponding to each cell.
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="rows">How many rows should the table have.</param>
        /// <param name="cols">How many columns should the table have.</param>
        /// <param name="padding">Padding of the table.</param>
        /// <param name="spacing">Spacing between the cells.</param>
        /// <returns></returns>
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
        /// <summary>
        /// Generates a table that can fit a set number of items with set aspect ratio (approx).
        /// Yields a rectangle corresponding to each item.
        /// </summary>
        /// <param name="area"></param>
        /// <param name="Count">How many items to plan for.</param>
        /// <param name="w2hratio">Desired aspect ration of the items.</param>
        /// <param name="padding">Padding for the table.</param>
        /// <param name="spacing">Spacing between the cells.</param>
        /// <returns></returns>
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
        /// <summary>Returns surface size in characters for current font settings.</summary>
        /// <param name="surface"></param>
        /// <returns></returns>
        public static Vector2I GetSizeInCharacters(this IMyTextSurface surface)
        {
            Vector2 CharSize = surface.MeasureStringInPixels(TextSample, surface.Font, surface.FontSize);
            return new Vector2I(
                    (int)(surface.SurfaceSize.X * (1.0f - 0.02f * surface.TextPadding) / CharSize.X),
                    (int)(surface.SurfaceSize.Y * (1.0f - 0.01f * surface.TextPadding) / CharSize.Y)
                );
        }
        /// <summary>Attempts to change font size to fit specified number of columns and rows.</summary>
        /// <param name="surface"></param>
        /// <param name="cols"></param>
        /// <param name="rows"></param>
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
        /// <summary>Creates a sprite stretched to fit into specified area.</summary>
        /// <param name="surface"></param>
        /// <param name="id">ID of the sprite to use.</param>
        /// <param name="roi">Area to fit the sprite into.</param>
        /// <param name="color">Color of the sprite.</param>
        /// <param name="rotation">How to rotate the sprite.</param>
        /// <returns></returns>
        public static MySprite FitSprite(this IMyTextSurface surface, string id, RectangleF roi, Color? color = null, float rotation = 0.0f)
        {
            Vector2 offset = (surface.TextureSize - surface.SurfaceSize) / 2;
            Vector2 pos = new Vector2(
                surface.SurfaceSize.X * (roi.X + roi.Size.X / 2) + offset.X, 
                surface.SurfaceSize.Y * (roi.Y + roi.Size.Y / 2) + offset.Y);
            Vector2 size = new Vector2(surface.SurfaceSize.X * roi.Size.X, surface.SurfaceSize.Y * roi.Size.Y);
            return new MySprite(SpriteType.TEXTURE, id, pos, size, color, null, TextAlignment.CENTER, rotation);
        }
        /// <summary>Creates a text sprite scaled to fit into specified area.</summary>
        /// <param name="surface"></param>
        /// <param name="text">Text to display</param>
        /// <param name="roi">Area to fit the text into</param>
        /// <param name="font">Font to use</param>
        /// <param name="color">Color of the text</param>
        /// <param name="align">Text alignment within the area</param>
        /// <returns></returns>
        public static MySprite FitText(this IMyTextSurface surface, StringBuilder text, RectangleF roi, string font, Color color, TextAlignment align = TextAlignment.CENTER)
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
            Vector2 size1 = surface.MeasureStringInPixels(text, font, 1);
            float scale = Math.Min(size.X / size1.X, size.Y / size1.Y);
            pos.Y -= size1.Y * scale / 2;
            return new MySprite(SpriteType.TEXT, text, pos, null, color, font, align, scale);
        }
        /// <summary>Creates a text sprite scaled to fit into specified area.</summary>
        /// <param name="surface"></param>
        /// <param name="text">Text to display</param>
        /// <param name="roi">Area to fit the text into</param>
        /// <param name="font">Font to use</param>
        /// <param name="color">Color of the text</param>
        /// <param name="align">Text alignment within the area</param>
        /// <returns></returns>
        public static MySprite FitText(this IMyTextSurface surface, string text, RectangleF roi, string font, Color color, TextAlignment align = TextAlignment.CENTER)
        {
            return FitText(surface, new StringBuilder(text), roi, font, color, align);
        }
        /// <summary>
        /// Adjusts a sprite to act as a progress bar.
        /// </summary>
        /// <param name="surface"></param>
        /// <param name="sprite">Sprite to adjust.</param>
        /// <param name="ratio">The progress bar fill ratio (0 - empty, 1 - full).</param>
        /// <param name="roi">Area to fit the progress bar into.</param>
        /// <param name="align">Progress bar horizontal alignment within the area.</param>
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
        /// <summary>Computes aspect ratio of the surface.</summary>
        /// <param name="surface"></param>
        /// <returns></returns>
        public static float Ratio(this IMyTextSurface surface) { return surface.SurfaceSize.X / surface.SurfaceSize.Y; }
        /// <summary>
        /// Generates a table that can fit a set number of items with set aspect ratio (approx),
        /// while taking into account surface's own aspect ratio.
        /// Yields a rectangle corresponding to each item.
        /// </summary>
        /// <param name="surface"></param>
        /// <param name="Count">How many items to generate</param>
        /// <param name="w2hratio">Desired aspect ratio for the items</param>
        /// <param name="padding">Padding for the table</param>
        /// <param name="spacing">Spacing between the cells</param>
        /// <param name="area">Area to generate the table in (whole surface if not set)</param>
        /// <returns></returns>
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
    /// <summary>
    /// A long-lived progress-bar made of a couple square/box sprites.
    /// </summary>
    class ProgressBar : IEnumerable<MySprite>
    {
        RectangleF area;
        float _Value;
        MySprite Background;
        MySprite Border;
        MySprite Foreground;
        MySprite Text;
        IMyTextSurface Surface;
        /// <summary>Format used to display the progress value, i.e. "{0:P1}".</summary>
        public string Format;
        /// <summary>Progress bar alignment - whether it goes left-to-right, right-to-left or grows from the center.</summary>
        public TextAlignment Alignment;
        /// <summary>
        /// Current progress value. Should be in 0...1 range.
        /// Negatives or NaNs will display "N/A" as the value instead.
        /// </summary>
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
        /// <summary>Color of the unfilled portion of the progress bar. Use transparent color to hide.</summary>
        public Color BackgroundColor
        {
            get { return Background.Color.Value; }
            set { Background.Color = value; }
        }
        /// <summary>Color of the border and the filled portion of the progress bar. Use transparent color to hide.</summary>
        public Color ForegroundColor
        {
            get { return Foreground.Color.Value; }
            set { Foreground.Color = value; Border.Color = value; }
        }
        /// <summary>Color of the text. Use transparent color to hide.</summary>
        public Color TextColor
        {
            get { return Text.Color.Value; }
            set { Text.Color = value; }
        }
        /// <summary>
        /// Creates an instance of progress bar linked to a particular surface.
        /// </summary>
        /// <param name="surface">Surface to use.</param>
        /// <param name="roi">Area for the progress bar to fit into.</param>
        /// <param name="fg">Color of the filled portion and the border.</param>
        /// <param name="bg">Color of the unfilled portion.</param>
        /// <param name="txt">Color of the text.</param>
        /// <param name="fmt">Format of the displayed value.</param>
        /// <param name="font">Font to diplay the value with.</param>
        /// <param name="align">Where the progress bar should start (left, right, start in the middle).</param>
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
        /// <summary>
        /// Sets surface's script background color and script foreground color as background and text colors respectively.
        /// </summary>
        public void PollColors()
        {
            BackgroundColor = Surface.ScriptBackgroundColor;
            TextColor = Surface.ScriptForegroundColor;
        }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        public IEnumerator<MySprite> GetEnumerator()
        {   //this is used so you can add all sprites via MyDrawFrame.AddRange() call.
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
