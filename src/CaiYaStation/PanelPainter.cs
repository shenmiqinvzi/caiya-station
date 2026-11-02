using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaiYaStation
{
    /// <summary>Text roles used by the shipment panel. Sizes and colours live in PaintStyles.</summary>
    internal enum PaintText
    {
        Title,
        Subtitle,
        Section,
        Label,
        Body,
        Small,
        RowName,
        RowValue,
        RowStrong,
        BigValue,
        Badge,
        Pill,
        MetricLabel,
        MetricValue,
        Tiny,
        Button,
        ButtonPrimary,
        Hint,
        TooltipTitle,
        TooltipBody,
        TooltipLine
    }

    /// <summary>
    /// Palette + font shared by the panel renderer. Deliberately free of GUI state so the same
    /// renderer can run headless in tools/PanelPreview against recorded paint commands.
    /// </summary>
    internal sealed class PanelPalette
    {
        // Light "cold white / mint" theme: pale blue-grey surfaces, deep grey-blue ink and a
        // deep-green primary so one palette drives the shipping panel, the receipt and the
        // loss notice instead of a dark panel next to a light sheet.
        internal Color Backdrop = new Color(0.478f, 0.541f, 0.616f, 0.55f);
        internal Color Shadow = new Color(0.290f, 0.353f, 0.435f, 0.30f);
        internal Color Panel = new Color(0.945f, 0.961f, 0.980f, 0.995f);
        internal Color PanelBorder = new Color(0.741f, 0.784f, 0.839f, 1f);
        internal Color Header = new Color(0.902f, 0.925f, 0.953f, 1f);
        internal Color HeaderLine = new Color(0.788f, 0.824f, 0.867f, 1f);
        internal Color Card = new Color(0.969f, 0.978f, 0.990f, 1f);
        internal Color CardBorder = new Color(0.812f, 0.847f, 0.890f, 1f);
        internal Color CardRaised = new Color(0.898f, 0.925f, 0.957f, 1f);
        internal Color Accent = new Color(0.094f, 0.514f, 0.263f, 1f);
        internal Color AccentDim = new Color(0.196f, 0.416f, 0.310f, 1f);
        internal Color Text = new Color(0.145f, 0.192f, 0.251f, 1f);
        internal Color TextDim = new Color(0.333f, 0.384f, 0.447f, 1f);
        internal Color TextFaint = new Color(0.478f, 0.525f, 0.584f, 1f);
        internal Color Good = new Color(0.086f, 0.478f, 0.286f, 1f);
        internal Color Warning = new Color(0.639f, 0.447f, 0.075f, 1f);
        internal Color Danger = new Color(0.722f, 0.239f, 0.243f, 1f);
        internal Color Gold = new Color(0.702f, 0.510f, 0.098f, 1f);
        internal Color Purple = new Color(0.463f, 0.322f, 0.694f, 1f);
        internal Color Blue = new Color(0.145f, 0.400f, 0.702f, 1f);
        internal Color TrackBackground = new Color(0.902f, 0.922f, 0.945f, 1f);
        internal Color TrackBorder = new Color(0.800f, 0.831f, 0.871f, 1f);
        internal Color RowHover = new Color(0.145f, 0.192f, 0.251f, 0.05f);
        internal Color RowSelected = new Color(0.094f, 0.514f, 0.263f, 0.14f);
        internal Color Button = new Color(0.957f, 0.967f, 0.980f, 1f);
        internal Color ButtonBorder = new Color(0.780f, 0.816f, 0.863f, 1f);
        internal Color ButtonHover = new Color(0.918f, 0.941f, 0.969f, 1f);
        internal Color ButtonHoverBorder = new Color(0.639f, 0.702f, 0.780f, 1f);
        internal Color ButtonDisabled = new Color(0.925f, 0.933f, 0.945f, 1f);
        internal Color ButtonDisabledBorder = new Color(0.851f, 0.871f, 0.890f, 1f);
        internal Color Primary = new Color(0.094f, 0.514f, 0.263f, 1f);
        internal Color PrimaryHover = new Color(0.129f, 0.580f, 0.318f, 1f);
        internal Color OnPrimary = new Color(1f, 1f, 1f, 1f);
        internal Color GoodSoft = new Color(0.878f, 0.945f, 0.910f, 1f);
        internal Color WarningSoft = new Color(0.984f, 0.941f, 0.867f, 1f);
        internal Color DangerSoft = new Color(0.961f, 0.906f, 0.910f, 1f);
        internal Color BlueSoft = new Color(0.906f, 0.933f, 0.976f, 1f);
        internal Font? Font;

        internal static PanelPalette Default => new PanelPalette();

        internal static Color Quality(int quality)
        {
            if (quality >= 9) return new Color(0.702f, 0.478f, 0.055f, 1f);
            if (quality >= 7) return new Color(0.478f, 0.290f, 0.702f, 1f);
            if (quality >= 5) return new Color(0.129f, 0.380f, 0.722f, 1f);
            if (quality >= 3) return new Color(0.086f, 0.478f, 0.286f, 1f);
            return new Color(0.478f, 0.525f, 0.584f, 1f);
        }

        internal static Color Alpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        internal Color QualityColor(int quality) => Quality(quality);
    }

    internal readonly struct TextStyle
    {
        internal readonly int Size;
        internal readonly bool Bold;
        internal readonly Color Color;
        internal readonly bool Wrap;

        internal TextStyle(int size, bool bold, Color color, bool wrap = false)
        {
            Size = size;
            Bold = bold;
            Color = color;
            Wrap = wrap;
        }
    }

    internal static class PaintStyles
    {
        internal static TextStyle Resolve(PaintText role, PanelPalette p)
        {
            switch (role)
            {
                case PaintText.Title: return new TextStyle(24, true, p.Text);
                case PaintText.Subtitle: return new TextStyle(13, false, p.TextDim);
                case PaintText.Section: return new TextStyle(14, true, p.Accent);
                case PaintText.Label: return new TextStyle(15, false, p.Text);
                case PaintText.Body: return new TextStyle(13, false, p.Text, true);
                case PaintText.Small: return new TextStyle(13, false, p.TextDim);
                case PaintText.RowName: return new TextStyle(16, false, p.Text);
                case PaintText.RowValue: return new TextStyle(14, false, p.TextDim);
                case PaintText.RowStrong: return new TextStyle(14, false, p.Text);
                case PaintText.BigValue: return new TextStyle(26, true, p.Accent);
                case PaintText.Badge: return new TextStyle(12, true, p.Accent);
                case PaintText.Pill: return new TextStyle(13, false, p.Text);
                case PaintText.MetricLabel: return new TextStyle(12, false, p.TextFaint);
                case PaintText.MetricValue: return new TextStyle(15, false, p.Text);
                case PaintText.Tiny: return new TextStyle(12, false, p.TextFaint);
                case PaintText.Button: return new TextStyle(14, false, p.Text);
                case PaintText.ButtonPrimary: return new TextStyle(14, true, p.OnPrimary);
                case PaintText.Hint: return new TextStyle(13, false, p.TextFaint);
                case PaintText.TooltipTitle: return new TextStyle(16, true, p.Text);
                case PaintText.TooltipBody: return new TextStyle(13, false, p.TextDim, true);
                case PaintText.TooltipLine: return new TextStyle(13, false, p.Text);
                default: return new TextStyle(14, false, p.Text);
            }
        }
    }

    /// <summary>
    /// Minimal painting surface: filled/rounded rects, text, icons and clipping. Two
    /// implementations exist — the live GUI painter and a recorder used by the offline preview.
    /// </summary>
    internal interface IPanelPainter
    {
        void Solid(Rect rect, Color color);
        void Rounded(Rect rect, float radius, Color fill, Color? border, float borderWidth);
        void Text(Rect rect, string text, PaintText role, TextAnchor anchor = TextAnchor.MiddleLeft,
            Color? color = null, bool wrap = false);
        void Icon(Rect rect, Sprite? sprite, string fallback, PaintText role, Color fallbackColor);
        void BeginClip(Rect rect);
        void EndClip();
        float Measure(string text, float width, PaintText role);
        /// <summary>Width the text needs on one line, for trimming it into a fixed cell.</summary>
        float Width(string text, PaintText role);
    }

    /// <summary>
    /// Text that has to live inside a fixed cell. A style can no longer clip vertically, so a
    /// long item name would otherwise run straight over the number column beside it.
    /// </summary>
    internal static class PanelText
    {
        internal static string Fit(IPanelPainter painter, string text, float width, PaintText role)
        {
            if (string.IsNullOrEmpty(text) || width <= 0f) return text ?? string.Empty;
            if (painter.Width(text, role) <= width) return text;
            const string ellipsis = "…";
            var trimmed = text;
            while (trimmed.Length > 1)
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
                if (painter.Width(trimmed + ellipsis, role) <= width) return trimmed + ellipsis;
            }
            return ellipsis;
        }
    }

    internal static class PaintGeometry
    {
        /// <summary>
        /// Device pixels per logical pixel, as set by ModBehaviour.OnGUI (Screen.height / 1080,
        /// clamped to 0.8–1.5).
        /// </summary>
        internal static float DeviceScale
        {
            get
            {
                var scale = GUI.matrix.m00;
                return scale > 0.0001f ? scale : 1f;
            }
        }

        /// <summary>
        /// Where the nine quads of a slice draw start and end, in <em>device</em> pixels.
        ///
        /// A nine-slice is nine quads that have to meet exactly. They only meet exactly when
        /// their shared edges land on whole screen pixels, and they usually do not: the panel is
        /// drawn under GUI.matrix scaled by Screen.height / 1080, so on a 1073px window one
        /// logical pixel is 0.9934 screen pixels and a 9px corner ends at x + 8.94. IMGUI rounds
        /// each quad on its own, so the two quads that share that edge come out a fraction of a
        /// pixel apart; what shows through the gap is the panel behind the shape. That is the
        /// pale line straight across 确认发货 and across the × close button — a seam, not a
        /// decoration. (When the rounding goes the other way the two quads overlap instead and
        /// the seam reads as a darker line.) Rounding the outer rect and both cut lines to whole
        /// pixels removes the seam: every edge is shared exactly, so there is no gap to see
        /// through and nothing to blend twice.
        /// </summary>
        internal readonly struct SliceCuts
        {
            /// <summary>Left/top/right/bottom edge of the whole shape, in device pixels.</summary>
            internal readonly float X0, Y0, X1, Y1;

            /// <summary>Thickness of the corner band, in device pixels.</summary>
            internal readonly float CutX, CutY;

            /// <summary>Corner band of the texture, in texels.</summary>
            internal readonly int Slice;

            internal SliceCuts(Rect rect, int textureWidth, int textureHeight, int border, float scale)
            {
                Slice = Mathf.Clamp(border, 0, Mathf.Min(textureWidth, textureHeight) / 2 - 1);
                var cut = Mathf.Round(Slice * scale);
                X0 = Mathf.Round(rect.x * scale);
                Y0 = Mathf.Round(rect.y * scale);
                X1 = Mathf.Round(rect.xMax * scale);
                Y1 = Mathf.Round(rect.yMax * scale);
                // A shape narrower than two corner bands keeps the bands equal and lets the
                // middle strip collapse, which is what a 8px tall pill needs.
                CutX = Mathf.Max(0f, Mathf.Min(cut, Mathf.Floor((X1 - X0) * 0.5f)));
                CutY = Mathf.Max(0f, Mathf.Min(cut, Mathf.Floor((Y1 - Y0) * 0.5f)));
            }
        }

        private static readonly Rect[] SliceParts = new Rect[9];
        private static readonly Rect[] SliceUvs = new Rect[9];

        /// <summary>
        /// The nine quads of a slice draw, in draw order — four corners, four edges, centre —
        /// with the texture area each one shows. Pure geometry, so the offline preview can check
        /// that the cuts line up without a GUI.
        /// </summary>
        internal static void Slice(Rect rect, int textureWidth, int textureHeight, int border, float scale,
            Rect[] parts, Rect[] uvs)
        {
            var cuts = new SliceCuts(rect, textureWidth, textureHeight, border, scale);
            var u1 = cuts.Slice / (float)textureWidth;
            var u2 = 1f - u1;
            var v1 = cuts.Slice / (float)textureHeight;
            var v2 = 1f - v1;

            // Back to logical space for the draw calls; dividing a whole device pixel by the
            // scale puts the edge exactly where the matrix will map it back to.
            var x0 = cuts.X0 / scale;
            var y0 = cuts.Y0 / scale;
            var x1 = cuts.X1 / scale;
            var y1 = cuts.Y1 / scale;
            var cornerW = cuts.CutX / scale;
            var cornerH = cuts.CutY / scale;
            var middleW = x1 - x0 - cornerW * 2f;
            var middleH = y1 - y0 - cornerH * 2f;

            parts[0] = new Rect(x0, y0, cornerW, cornerH);
            parts[1] = new Rect(x1 - cornerW, y0, cornerW, cornerH);
            parts[2] = new Rect(x0, y1 - cornerH, cornerW, cornerH);
            parts[3] = new Rect(x1 - cornerW, y1 - cornerH, cornerW, cornerH);
            parts[4] = new Rect(x0 + cornerW, y0, middleW, cornerH);
            parts[5] = new Rect(x0 + cornerW, y1 - cornerH, middleW, cornerH);
            parts[6] = new Rect(x0, y0 + cornerH, cornerW, middleH);
            parts[7] = new Rect(x1 - cornerW, y0 + cornerH, cornerW, middleH);
            parts[8] = new Rect(x0 + cornerW, y0 + cornerH, middleW, middleH);

            uvs[0] = new Rect(0f, v2, u1, v1);
            uvs[1] = new Rect(u2, v2, u1, v1);
            uvs[2] = new Rect(0f, 0f, u1, v1);
            uvs[3] = new Rect(u2, 0f, u1, v1);
            uvs[4] = new Rect(u1, v2, u2 - u1, v1);
            uvs[5] = new Rect(u1, 0f, u2 - u1, v1);
            uvs[6] = new Rect(0f, v1, u1, v2 - v1);
            uvs[7] = new Rect(u2, v1, u1, v2 - v1);
            uvs[8] = new Rect(u1, v1, u2 - u1, v2 - v1);
        }

        /// <summary>Nine-slice draw so a rounded texture keeps its corner radius at any size.</summary>
        internal static void DrawSliced(Rect rect, Texture2D texture, int border)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f) return;
            Slice(rect, texture.width, texture.height, border, DeviceScale, SliceParts, SliceUvs);
            for (var i = 0; i < SliceParts.Length; i++)
            {
                var part = SliceParts[i];
                // A collapsed strip (a rect narrower than its two corners) has nothing to draw.
                if (part.width <= 0f || part.height <= 0f) continue;
                GUI.DrawTextureWithTexCoords(part, texture, SliceUvs[i]);
            }
        }
    }

    /// <summary>Live IMGUI painter. Owns the generated textures and the per-role GUIStyles.</summary>
    internal sealed class GuiPanelPainter : IPanelPainter, IDisposable
    {
        private readonly PanelPalette _palette;
        private readonly Dictionary<StyleKey, GUIStyle> _styles = new Dictionary<StyleKey, GUIStyle>();
        private readonly Dictionary<TextureKey, Texture2D> _textures = new Dictionary<TextureKey, Texture2D>();
        private readonly List<Vector2> _offsets = new List<Vector2>();
        private Texture2D? _white;
        private Vector2 _offset;
        private bool _disposed;

        internal GuiPanelPainter(PanelPalette palette)
        {
            _palette = palette;
        }

        private Texture2D White
        {
            get
            {
                if (_white != null) return _white;
                _white = MakeSolid(Color.white, "CaiYaStation.PaintWhite");
                return _white;
            }
        }

        public void Solid(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(Offset(rect), White);
            GUI.color = previous;
        }

        public void Rounded(Rect rect, float radius, Color fill, Color? border, float borderWidth)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            var key = new TextureKey(Mathf.RoundToInt(radius), fill, border ?? Color.clear, border.HasValue, Mathf.RoundToInt(borderWidth));
            if (!_textures.TryGetValue(key, out var texture) || texture == null)
            {
                texture = MakeRounded(Mathf.Clamp(key.Radius * 4, 24, 96), key.Radius, fill, border, key.BorderWidth,
                    "CaiYaStation.Paint" + _textures.Count);
                _textures[key] = texture;
            }
            PaintGeometry.DrawSliced(Offset(rect), texture, Mathf.Clamp(key.Radius, 1, 48));
        }

        public void Text(Rect rect, string text, PaintText role, TextAnchor anchor = TextAnchor.MiddleLeft,
            Color? color = null, bool wrap = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            var style = StyleFor(role, anchor, color, wrap);
            GUI.Label(Offset(rect), text, style);
        }

        public void Icon(Rect rect, Sprite? sprite, string fallback, PaintText role, Color fallbackColor)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            if (sprite != null)
            {
                try
                {
                    var texture = sprite.texture;
                    if (texture != null)
                    {
                        var source = sprite.textureRect;
                        // Fit inside the slot instead of stretching into it: the station marker is
                        // a square sticker today, but item icons and any future art are not.
                        var fit = FitInside(rect, source.width, source.height);
                        GUI.DrawTextureWithTexCoords(Offset(fit), texture,
                            new Rect(source.x / texture.width, source.y / texture.height,
                                source.width / texture.width, source.height / texture.height), true);
                        return;
                    }
                }
                catch
                {
                    // Fall through to the letter tile: a broken sprite must never take the panel down.
                }
            }
            Rounded(rect, 6f, PanelPalette.Alpha(fallbackColor, 0.10f), PanelPalette.Alpha(fallbackColor, 0.22f), 1f);
            Text(rect, string.IsNullOrEmpty(fallback) ? "?" : fallback.Substring(0, 1), role,
                TextAnchor.MiddleCenter, fallbackColor);
        }

        public void BeginClip(Rect rect)
        {
            _offsets.Add(_offset);
            // rect is always an absolute screen rect, so the group origin is rect itself and
            // everything drawn inside must subtract rect.position — not the accumulated offset.
            // Adding instead of assigning here made a nested clip (rail card -> manifest view)
            // subtract the parent's position twice, which pushed every manifest row out of the
            // group and left the "待运送货物" list looking empty on screen.
            var local = new Rect(rect.x - _offset.x, rect.y - _offset.y, Mathf.Max(0f, rect.width), Mathf.Max(0f, rect.height));
            GUI.BeginGroup(local);
            _offset = rect.position;
        }

        public void EndClip()
        {
            if (_offsets.Count == 0) return;
            _offset = _offsets[_offsets.Count - 1];
            _offsets.RemoveAt(_offsets.Count - 1);
            GUI.EndGroup();
        }

        public float Measure(string text, float width, PaintText role)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            var style = StyleFor(role, TextAnchor.UpperLeft, null, true);
            return style.CalcHeight(new GUIContent(text), Mathf.Max(16f, width));
        }

        public float Width(string text, PaintText role)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return StyleFor(role, TextAnchor.MiddleLeft, null, false).CalcSize(new GUIContent(text)).x;
        }

        private GUIStyle StyleFor(PaintText role, TextAnchor anchor, Color? color, bool wrap)
        {
            var key = new StyleKey(role, anchor, color ?? Color.clear, color.HasValue, wrap);
            if (_styles.TryGetValue(key, out var cached)) return cached;
            var spec = PaintStyles.Resolve(role, _palette);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = spec.Size,
                fontStyle = spec.Bold ? FontStyle.Bold : FontStyle.Normal,
                alignment = anchor,
                wordWrap = wrap,
                richText = false,
                // Wrapped rects are sized by CalcHeight, so Clip is safe there. Single-line rects
                // are laid out on a nominal height while a CJK line box is taller than that
                // (YaHei at 12px measures ~17px), and Clip sliced the top and bottom of the glyphs
                // in game. Overflow lets the glyphs finish drawing; PanelText.Fit keeps them inside
                // their column horizontally.
                clipping = wrap ? TextClipping.Clip : TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            style.normal.textColor = color ?? spec.Color;
            var font = _palette.Font;
            if (font != null) style.font = font;
            _styles[key] = style;
            return style;
        }

        private Rect Offset(Rect rect) => new Rect(rect.x - _offset.x, rect.y - _offset.y, rect.width, rect.height);

        /// <summary>
        /// Largest rect with the sprite's aspect ratio that fits inside <paramref name="slot"/>,
        /// centred. Keeps non-square art from being squashed into a square header tile.
        /// </summary>
        private static Rect FitInside(Rect slot, float width, float height)
        {
            if (width <= 0f || height <= 0f) return slot;
            var scale = Mathf.Min(slot.width / width, slot.height / height);
            var drawWidth = width * scale;
            var drawHeight = height * scale;
            return new Rect(slot.x + (slot.width - drawWidth) * 0.5f,
                slot.y + (slot.height - drawHeight) * 0.5f, drawWidth, drawHeight);
        }

        private static Texture2D MakeSolid(Color color, string name)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = name, hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply(false);
            return texture;
        }

        private static Texture2D MakeRounded(int size, int radius, Color fill, Color? border, int borderWidth, string name)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var half = size * 0.5f;
            var r = Mathf.Min(radius, size * 0.5f - 1f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = x + 0.5f - half;
                    var py = y + 0.5f - half;
                    var qx = Mathf.Abs(px) - (half - r);
                    var qy = Mathf.Abs(py) - (half - r);
                    var outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    var distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                    var alpha = Mathf.Clamp01(0.5f - distance);
                    if (alpha <= 0.001f)
                    {
                        texture.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                        continue;
                    }
                    var colour = fill;
                    if (border.HasValue && borderWidth > 0 && distance > -borderWidth)
                    {
                        var blend = Mathf.Clamp01(-distance / borderWidth);
                        colour = Color.Lerp(border.Value, fill, blend);
                    }
                    colour.a *= alpha;
                    texture.SetPixel(x, y, colour);
                }
            }
            texture.Apply(false);
            return texture;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var texture in _textures.Values)
                if (texture != null) UnityEngine.Object.Destroy(texture);
            _textures.Clear();
            _styles.Clear();
            if (_white != null) UnityEngine.Object.Destroy(_white);
            _white = null;
        }

        private readonly struct StyleKey : IEquatable<StyleKey>
        {
            private readonly PaintText _role;
            private readonly TextAnchor _anchor;
            private readonly Color _color;
            private readonly bool _hasColor;
            private readonly bool _wrap;

            internal StyleKey(PaintText role, TextAnchor anchor, Color color, bool hasColor, bool wrap)
            {
                _role = role;
                _anchor = anchor;
                _color = color;
                _hasColor = hasColor;
                _wrap = wrap;
            }

            public bool Equals(StyleKey other) =>
                _role == other._role && _anchor == other._anchor && _hasColor == other._hasColor
                && _wrap == other._wrap && (!_hasColor || _color == other._color);

            public override bool Equals(object? obj) => obj is StyleKey other && Equals(other);

            public override int GetHashCode() =>
                ((int)_role * 397) ^ ((int)_anchor * 31) ^ (_wrap ? 17 : 0) ^ (_hasColor ? _color.GetHashCode() : 0);
        }

        private readonly struct TextureKey : IEquatable<TextureKey>
        {
            internal readonly int Radius;
            internal readonly Color Fill;
            internal readonly Color Border;
            private readonly bool _hasBorder;
            internal readonly int BorderWidth;

            internal TextureKey(int radius, Color fill, Color border, bool hasBorder, int borderWidth)
            {
                Radius = radius;
                Fill = fill;
                Border = border;
                _hasBorder = hasBorder;
                BorderWidth = borderWidth;
            }

            public bool Equals(TextureKey other) =>
                Radius == other.Radius && BorderWidth == other.BorderWidth && _hasBorder == other._hasBorder
                && Fill == other.Fill && (!_hasBorder || Border == other.Border);

            public override bool Equals(object? obj) => obj is TextureKey other && Equals(other);

            public override int GetHashCode() =>
                (Radius * 397) ^ BorderWidth ^ (_hasBorder ? 1 : 0) ^ Fill.GetHashCode() ^ (_hasBorder ? Border.GetHashCode() : 0);
        }
    }
}
