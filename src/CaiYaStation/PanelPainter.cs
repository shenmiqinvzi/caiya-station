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
        internal Color Backdrop = new Color(0f, 0f, 0f, 0.60f);
        internal Color Shadow = new Color(0f, 0f, 0f, 0.45f);
        internal Color Panel = new Color(0.051f, 0.082f, 0.069f, 0.995f);
        internal Color PanelBorder = new Color(0.153f, 0.271f, 0.196f, 1f);
        internal Color Header = new Color(0.067f, 0.114f, 0.094f, 1f);
        internal Color HeaderLine = new Color(0.165f, 0.302f, 0.224f, 1f);
        internal Color Card = new Color(0.063f, 0.106f, 0.086f, 1f);
        internal Color CardBorder = new Color(0.122f, 0.204f, 0.169f, 1f);
        internal Color CardRaised = new Color(0.078f, 0.129f, 0.106f, 1f);
        internal Color Accent = new Color(0.373f, 0.890f, 0.631f, 1f);
        internal Color AccentDim = new Color(0.184f, 0.498f, 0.357f, 1f);
        internal Color Text = new Color(0.925f, 0.965f, 0.941f, 1f);
        internal Color TextDim = new Color(0.663f, 0.741f, 0.694f, 1f);
        internal Color TextFaint = new Color(0.478f, 0.565f, 0.522f, 1f);
        internal Color Good = new Color(0.388f, 0.847f, 0.580f, 1f);
        internal Color Warning = new Color(0.941f, 0.706f, 0.361f, 1f);
        internal Color Danger = new Color(0.949f, 0.439f, 0.373f, 1f);
        internal Color Gold = new Color(1f, 0.820f, 0.400f, 1f);
        internal Color Purple = new Color(0.753f, 0.545f, 1f, 1f);
        internal Color Blue = new Color(0.455f, 0.663f, 1f, 1f);
        internal Color TrackBackground = new Color(0.039f, 0.067f, 0.055f, 1f);
        internal Color TrackBorder = new Color(0.114f, 0.188f, 0.157f, 1f);
        internal Color RowHover = new Color(1f, 1f, 1f, 0.045f);
        internal Color RowSelected = new Color(0.373f, 0.890f, 0.631f, 0.15f);
        internal Color Button = new Color(0.086f, 0.141f, 0.114f, 1f);
        internal Color ButtonBorder = new Color(0.149f, 0.239f, 0.188f, 1f);
        internal Color ButtonHover = new Color(0.110f, 0.184f, 0.149f, 1f);
        internal Color ButtonHoverBorder = new Color(0.200f, 0.345f, 0.271f, 1f);
        internal Color ButtonDisabled = new Color(0.055f, 0.090f, 0.075f, 1f);
        internal Color ButtonDisabledBorder = new Color(0.110f, 0.165f, 0.137f, 1f);
        internal Color Primary = new Color(0.373f, 0.890f, 0.631f, 1f);
        internal Color PrimaryHover = new Color(0.463f, 0.941f, 0.694f, 1f);
        internal Color OnPrimary = new Color(0.024f, 0.098f, 0.063f, 1f);
        internal Font? Font;

        internal static PanelPalette Default => new PanelPalette();

        internal static Color Quality(int quality)
        {
            if (quality >= 9) return new Color(1f, 0.82f, 0.40f, 1f);
            if (quality >= 7) return new Color(0.75f, 0.55f, 1f, 1f);
            if (quality >= 5) return new Color(0.45f, 0.66f, 1f, 1f);
            if (quality >= 3) return new Color(0.39f, 0.85f, 0.58f, 1f);
            return new Color(0.48f, 0.57f, 0.53f, 1f);
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
    }

    internal static class PaintGeometry
    {
        /// <summary>Nine-slice draw so a rounded texture keeps its corner radius at any size.</summary>
        internal static void DrawSliced(Rect rect, Texture2D texture, int border)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f) return;
            var width = texture.width;
            var height = texture.height;
            var slice = Mathf.Clamp(border, 0, Mathf.Min(width, height) / 2 - 1);
            var left = Mathf.Min(slice, rect.width * 0.5f);
            var right = Mathf.Min(slice, rect.width * 0.5f);
            var top = Mathf.Min(slice, rect.height * 0.5f);
            var bottom = Mathf.Min(slice, rect.height * 0.5f);
            var u1 = slice / (float)width;
            var u2 = 1f - u1;
            var v1 = slice / (float)height;
            var v2 = 1f - v1;

            GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y, left, top), texture, new Rect(0f, v2, u1, v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax - right, rect.y, right, top), texture, new Rect(u2, v2, u1, v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.yMax - bottom, left, bottom), texture, new Rect(0f, 0f, u1, v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax - right, rect.yMax - bottom, right, bottom), texture, new Rect(u2, 0f, u1, v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x + left, rect.y, rect.width - left - right, top), texture, new Rect(u1, v2, u2 - u1, v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x + left, rect.yMax - bottom, rect.width - left - right, bottom), texture, new Rect(u1, 0f, u2 - u1, v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y + top, left, rect.height - top - bottom), texture, new Rect(0f, v1, u1, v2 - v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax - right, rect.y + top, right, rect.height - top - bottom), texture, new Rect(u2, v1, u1, v2 - v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x + left, rect.y + top, rect.width - left - right, rect.height - top - bottom), texture, new Rect(u1, v1, u2 - u1, v2 - v1));
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
                        GUI.DrawTextureWithTexCoords(Offset(rect), texture,
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
            var local = new Rect(rect.x - _offset.x, rect.y - _offset.y, Mathf.Max(0f, rect.width), Mathf.Max(0f, rect.height));
            GUI.BeginGroup(local);
            _offset += rect.position;
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
                clipping = wrap ? TextClipping.Clip : TextClipping.Clip,
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
