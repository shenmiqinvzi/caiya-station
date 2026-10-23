using System;
using UnityEngine;

namespace CaiYaStation
{
    /// <summary>
    /// Small runtime theme: generated rounded 9-slice textures and cached styles.
    /// Kept in one place so the panel, labels and receipts share the same look, and so
    /// every generated texture is destroyed when the mod is disabled.
    /// </summary>
    internal sealed class UiTheme : IDisposable
    {
        internal Color Background { get; } = new Color(0.031f, 0.051f, 0.039f, 1f);
        internal Color Panel { get; } = new Color(0.051f, 0.082f, 0.069f, 0.995f);
        internal Color Header { get; } = new Color(0.067f, 0.114f, 0.094f, 1f);
        internal Color Card { get; } = new Color(0.063f, 0.106f, 0.086f, 1f);
        internal Color Accent { get; } = new Color(0.373f, 0.890f, 0.631f, 1f);
        internal Color AccentDeep { get; } = new Color(0.184f, 0.498f, 0.357f, 1f);
        internal Color Text { get; } = new Color(0.925f, 0.965f, 0.941f, 1f);
        internal Color TextDim { get; } = new Color(0.663f, 0.741f, 0.694f, 1f);
        internal Color Good { get; } = new Color(0.388f, 0.847f, 0.580f, 1f);
        internal Color Warning { get; } = new Color(0.941f, 0.706f, 0.361f, 1f);
        internal Color Danger { get; } = new Color(0.949f, 0.439f, 0.373f, 1f);
        internal Color Gold { get; } = new Color(1f, 0.820f, 0.400f, 1f);
        internal Color Purple { get; } = new Color(0.753f, 0.545f, 1f, 1f);
        internal Color Blue { get; } = new Color(0.455f, 0.663f, 1f, 1f);

        private PanelPalette? _palette;

        /// <summary>Palette handed to the panel renderer so both UI layers share one colour set.</summary>
        internal PanelPalette Palette
        {
            get
            {
                if (_palette != null) return _palette;
                var palette = PanelPalette.Default;
                palette.Font = _font;
                palette.Panel = Panel;
                palette.Header = Header;
                palette.Card = Card;
                palette.Accent = Accent;
                palette.AccentDim = AccentDeep;
                palette.Text = Text;
                palette.TextDim = TextDim;
                palette.Good = Good;
                palette.Warning = Warning;
                palette.Danger = Danger;
                palette.Gold = Gold;
                palette.Purple = Purple;
                palette.Blue = Blue;
                palette.Primary = Accent;
                _palette = palette;
                return palette;
            }
        }

        internal GUIStyle Title { get; }
        internal GUIStyle Subtitle { get; }
        internal GUIStyle Label { get; }
        internal GUIStyle BodyText { get; }
        internal GUIStyle Small { get; }
        internal GUIStyle SmallRight { get; }
        internal GUIStyle RowName { get; }
        internal GUIStyle RowValue { get; }
        internal GUIStyle RowValueBold { get; }
        internal GUIStyle Button { get; }
        internal GUIStyle ButtonSmall { get; }
        internal GUIStyle ButtonPrimary { get; }
        internal GUIStyle CardStyle { get; }
        internal GUIStyle CardSelected { get; }
        internal GUIStyle CloseButton { get; }
        internal GUIStyle RowButton { get; }
        internal GUIStyle Badge { get; }
        internal GUIStyle PillLabel { get; }
        internal GUIStyle WarningText { get; }
        internal GUIStyle Tooltip { get; }
        internal GUIStyle BigValue { get; }
        internal GUIStyle Section { get; }
        internal GUIStyle NoticeTitle { get; }
        internal GUIStyle NoticeBody { get; }
        internal GUIStyle NoticeHint { get; }

        private readonly Texture2D _white;
        private readonly Texture2D _dim;
        private readonly Texture2D _panel;
        private readonly Texture2D _header;
        private readonly Texture2D _card;
        private readonly Texture2D _cardSelected;
        private readonly Texture2D _rowHover;
        private readonly Texture2D _rowSelected;
        private readonly Texture2D _button;
        private readonly Texture2D _buttonHover;
        private readonly Texture2D _buttonActive;
        private readonly Texture2D _buttonDisabled;
        private readonly Texture2D _pill;
        private readonly Texture2D _check;
        private readonly Texture2D _riskTrack;
        private readonly Font? _font;
        private bool _disposed;

        internal UiTheme(Action<string> log)
        {
            try
            {
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 16);
            }
            catch (Exception exception)
            {
                log("UI font fallback: " + exception.Message);
            }

            _white = Solid(Color.white, "CaiYaStation.White");
            _dim = Solid(new Color(0f, 0f, 0f, 0.55f), "CaiYaStation.Dim");
            _panel = Rounded(72, 16, Panel, new Color(0.153f, 0.271f, 0.196f, 1f), 2, "CaiYaStation.Panel");
            _header = Rounded(48, 14, Header, new Color(0.165f, 0.302f, 0.224f, 0.8f), 1, "CaiYaStation.Header");
            _card = Rounded(48, 12, Card, new Color(0.122f, 0.204f, 0.169f, 1f), 1, "CaiYaStation.Card");
            _cardSelected = Rounded(48, 12, new Color(0.078f, 0.129f, 0.106f, 1f), Accent, 1, "CaiYaStation.CardSelected");
            _rowHover = Rounded(32, 8, new Color(1f, 1f, 1f, 0.055f), null, 0, "CaiYaStation.RowHover");
            _rowSelected = Rounded(32, 8, new Color(0.32f, 0.90f, 0.58f, 0.14f), null, 0, "CaiYaStation.RowSelected");
            _button = Rounded(40, 10, new Color(0.10f, 0.15f, 0.13f, 1f), new Color(0.18f, 0.30f, 0.24f, 1f), 1, "CaiYaStation.Button");
            _buttonHover = Rounded(40, 10, new Color(0.14f, 0.22f, 0.18f, 1f), new Color(0.30f, 0.55f, 0.40f, 1f), 1, "CaiYaStation.ButtonHover");
            _buttonActive = Rounded(40, 10, new Color(0.30f, 0.86f, 0.55f, 1f), new Color(0.55f, 1f, 0.72f, 1f), 1, "CaiYaStation.ButtonActive");
            _buttonDisabled = Rounded(40, 10, new Color(0.08f, 0.10f, 0.09f, 1f), new Color(0.14f, 0.18f, 0.16f, 1f), 1, "CaiYaStation.ButtonDisabled");
            _pill = Rounded(36, 16, new Color(0.04f, 0.07f, 0.06f, 0.92f), new Color(0.14f, 0.30f, 0.22f, 0.9f), 1, "CaiYaStation.Pill");
            _check = Rounded(24, 6, Accent, null, 0, "CaiYaStation.Check");
            _riskTrack = Rounded(24, 8, new Color(0.05f, 0.07f, 0.065f, 1f), new Color(0.12f, 0.18f, 0.15f, 1f), 1, "CaiYaStation.RiskTrack");

            Title = Style(26, FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            Subtitle = Style(13, FontStyle.Normal, TextDim, TextAnchor.MiddleLeft);
            Label = Style(15, FontStyle.Normal, Text, TextAnchor.MiddleLeft);
            BodyText = Style(13, FontStyle.Normal, Text, TextAnchor.UpperLeft);
            BodyText.wordWrap = true;
            Small = Style(12, FontStyle.Normal, TextDim, TextAnchor.MiddleLeft);
            SmallRight = Style(12, FontStyle.Normal, TextDim, TextAnchor.MiddleRight);
            RowName = Style(15, FontStyle.Normal, Text, TextAnchor.MiddleLeft);
            RowValue = Style(13, FontStyle.Normal, TextDim, TextAnchor.MiddleRight);
            RowValueBold = Style(16, FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            BigValue = Style(24, FontStyle.Bold, Accent, TextAnchor.MiddleRight);
            Section = Style(13, FontStyle.Bold, Accent, TextAnchor.MiddleLeft);
            NoticeTitle = Style(23, FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            NoticeBody = Style(16, FontStyle.Normal, Text, TextAnchor.MiddleLeft);
            NoticeHint = Style(14, FontStyle.Bold, Accent, TextAnchor.MiddleLeft);

            Button = BlockStyle(_button, _buttonHover, _buttonActive, Text, 14);
            ButtonSmall = BlockStyle(_button, _buttonHover, _buttonActive, Text, 13);
            ButtonSmall.padding = new RectOffset(8, 8, 3, 3);
            Button.normal.background = _button;
            Button.hover.background = _buttonHover;
            Button.active.background = _buttonActive;
            Button.focused.background = _buttonHover;
            Button.onNormal.background = _buttonActive;
            Button.onHover.background = _buttonActive;
            Button.onActive.background = _buttonActive;
            ButtonPrimary = new GUIStyle(Button);
            ButtonPrimary.normal.background = _buttonActive;
            ButtonPrimary.hover.background = _buttonActive;
            ButtonPrimary.active.background = _buttonActive;
            ButtonPrimary.normal.textColor = new Color(0.02f, 0.10f, 0.06f, 1f);
            ButtonPrimary.hover.textColor = ButtonPrimary.normal.textColor;
            ButtonPrimary.active.textColor = ButtonPrimary.normal.textColor;
            ButtonPrimary.fontStyle = FontStyle.Bold;

            CardStyle = BlockStyle(_card, _card, _card, Text, 14);
            CardSelected = BlockStyle(_cardSelected, _cardSelected, _cardSelected, Text, 14);
            CloseButton = BlockStyle(_button, _buttonHover, _buttonActive, TextDim, 18);
            RowButton = new GUIStyle(GUIStyle.none)
            {
                border = new RectOffset(10, 10, 10, 10),
                normal = { background = null },
                hover = { background = _rowHover },
                active = { background = _rowSelected },
                focused = { background = _rowHover }
            };
            Badge = new GUIStyle(Small) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            Badge.normal.textColor = Accent;
            PillLabel = new GUIStyle(Small) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            PillLabel.normal.textColor = Text;
            WarningText = new GUIStyle(Small) { wordWrap = true, fontSize = 12 };
            WarningText.normal.textColor = Danger;
            Tooltip = new GUIStyle(Small) { wordWrap = true, fontSize = 12 };
            Tooltip.normal.textColor = Text;
            Tooltip.padding = new RectOffset(12, 12, 10, 10);
            Tooltip.border = new RectOffset(18, 18, 18, 18);
            Tooltip.normal.background = _panel;

            if (_font != null)
            {
                foreach (var style in new[] { Title, Subtitle, Label, BodyText, Small, SmallRight, RowName, RowValue, RowValueBold, Button, ButtonSmall, ButtonPrimary, CardStyle, CardSelected, CloseButton, RowButton, Badge, PillLabel, WarningText, Tooltip, BigValue, Section, NoticeTitle, NoticeBody, NoticeHint })
                    style.font = _font;
            }
        }

        private GUIStyle Style(int size, FontStyle weight, Color color, TextAnchor anchor)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = weight,
                alignment = anchor,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = color;
            return style;
        }

        private static GUIStyle BlockStyle(Texture2D normal, Texture2D hover, Texture2D active, Color text, int size)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = size,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(12, 12, 12, 12),
                padding = new RectOffset(10, 10, 5, 5)
            };
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = active;
            style.focused.background = hover;
            style.normal.textColor = text;
            style.hover.textColor = text;
            style.active.textColor = text;
            return style;
        }

        internal void DrawSolid(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _white);
            GUI.color = previous;
        }

        internal void DrawDim(float width, float height) => GUI.DrawTexture(new Rect(0, 0, width, height), _dim);
        internal void DrawPanel(Rect rect) => DrawSliced(rect, _panel, 22);
        internal void DrawHeader(Rect rect) => DrawSliced(rect, _header, 18);
        internal void DrawCard(Rect rect, bool selected) => DrawSliced(rect, selected ? _cardSelected : _card, 16);
        internal void DrawRowHover(Rect rect) => DrawSliced(rect, _rowHover, 10);
        internal void DrawRowSelected(Rect rect) => DrawSliced(rect, _rowSelected, 10);
        internal void DrawPill(Rect rect) => DrawSliced(rect, _pill, 8);
        internal void DrawRiskTrack(Rect rect) => DrawSliced(rect, _riskTrack, 8);

        /// <summary>Nine-slice draw so a 72px rounded texture keeps its corner radius at panel size.</summary>
        private static void DrawSliced(Rect rect, Texture2D texture, int border)
        {
            if (texture == null) return;
            var width = texture.width;
            var height = texture.height;
            var slice = Mathf.Clamp(border, 0, Mathf.Min(width, height) / 2 - 1);
            var left = Mathf.Min(slice, rect.width * 0.5f);
            var right = Mathf.Min(slice, rect.width * 0.5f);
            var top = Mathf.Min(slice, rect.height * 0.5f);
            var bottom = Mathf.Min(slice, rect.height * 0.5f);
            var u0 = 0f;
            var u1 = slice / (float)width;
            var u2 = 1f - u1;
            var v0 = 0f;
            var v1 = slice / (float)height;
            var v2 = 1f - v1;

            // Corners.
            GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y, left, top), texture, new Rect(u0, v2, u1, v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax - right, rect.y, right, top), texture, new Rect(u2, v2, u1, v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.yMax - bottom, left, bottom), texture, new Rect(u0, v0, u1, v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax - right, rect.yMax - bottom, right, bottom), texture, new Rect(u2, v0, u1, v1));
            // Edges.
            GUI.DrawTextureWithTexCoords(new Rect(rect.x + left, rect.y, rect.width - left - right, top), texture, new Rect(u1, v2, u2 - u1, v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x + left, rect.yMax - bottom, rect.width - left - right, bottom), texture, new Rect(u1, v0, u2 - u1, v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y + top, left, rect.height - top - bottom), texture, new Rect(u0, v1, u1, v2 - v1));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax - right, rect.y + top, right, rect.height - top - bottom), texture, new Rect(u2, v1, u1, v2 - v1));
            // Centre.
            GUI.DrawTextureWithTexCoords(new Rect(rect.x + left, rect.y + top, rect.width - left - right, rect.height - top - bottom), texture, new Rect(u1, v1, u2 - u1, v2 - v1));
        }

        internal void DrawCheck(Rect rect, bool selected)
        {
            if (selected)
            {
                GUI.DrawTexture(rect, _check, ScaleMode.StretchToFill, true);
                var previous = GUI.color;
                GUI.color = new Color(0.02f, 0.12f, 0.07f, 1f);
                GUI.Label(rect, "✓", Badge);
                GUI.color = previous;
                return;
            }
            var previousColor = GUI.color;
            GUI.color = new Color(0.45f, 0.55f, 0.50f, 1f);
            GUI.DrawTexture(rect, _white, ScaleMode.StretchToFill, true);
            GUI.color = new Color(0.06f, 0.09f, 0.08f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), _white);
            GUI.color = previousColor;
        }

        internal static Color QualityColor(int quality)
        {
            if (quality >= 9) return new Color(1f, 0.80f, 0.30f, 1f);
            if (quality >= 7) return new Color(0.78f, 0.56f, 1f, 1f);
            if (quality >= 5) return new Color(0.48f, 0.72f, 1f, 1f);
            if (quality >= 3) return new Color(0.42f, 0.90f, 0.62f, 1f);
            return new Color(0.66f, 0.76f, 0.70f, 1f);
        }

        internal Texture2D? BackgroundForDisabled => _buttonDisabled;

        private static Texture2D Solid(Color color, string name)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = name, hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply(false);
            return texture;
        }

        private static Texture2D Rounded(int size, int radius, Color fill, Color? border, int borderWidth, string name)
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
                    var outside = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0));
                    var distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
                    var alpha = Mathf.Clamp01(0.5f - distance);
                    if (alpha <= 0.001f)
                    {
                        texture.SetPixel(x, y, new Color(0, 0, 0, 0));
                        continue;
                    }
                    var colour = fill;
                    if (border.HasValue && borderWidth > 0 && distance > -borderWidth)
                    {
                        var blend = Mathf.Clamp01((-distance) / borderWidth);
                        colour = Color.Lerp(border.Value, fill, blend * 0.75f);
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
            foreach (var texture in new[]
                     {
                         _white, _dim, _panel, _header, _card, _cardSelected, _rowHover, _rowSelected,
                         _button, _buttonHover, _buttonActive, _buttonDisabled, _pill, _check, _riskTrack
                     })
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
            if (_font != null) UnityEngine.Object.Destroy(_font);
        }
    }
}
