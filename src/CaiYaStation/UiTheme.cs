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
        internal Color Background { get; } = new Color(0.945f, 0.961f, 0.980f, 1f);
        internal Color Panel { get; } = new Color(0.945f, 0.961f, 0.980f, 0.995f);
        internal Color Header { get; } = new Color(0.902f, 0.925f, 0.953f, 1f);
        internal Color Card { get; } = new Color(0.969f, 0.978f, 0.990f, 1f);
        internal Color Accent { get; } = new Color(0.094f, 0.514f, 0.263f, 1f);
        internal Color AccentDeep { get; } = new Color(0.196f, 0.416f, 0.310f, 1f);
        internal Color Text { get; } = new Color(0.145f, 0.192f, 0.251f, 1f);
        internal Color TextDim { get; } = new Color(0.333f, 0.384f, 0.447f, 1f);
        internal Color TextFaint { get; } = new Color(0.478f, 0.525f, 0.584f, 1f);
        internal Color PanelBorder { get; } = new Color(0.741f, 0.784f, 0.839f, 1f);
        internal Color HeaderLine { get; } = new Color(0.788f, 0.824f, 0.867f, 1f);
        internal Color CardBorder { get; } = new Color(0.812f, 0.847f, 0.890f, 1f);
        internal Color CardRaised { get; } = new Color(0.898f, 0.925f, 0.957f, 1f);
        internal Color Good { get; } = new Color(0.086f, 0.478f, 0.286f, 1f);
        internal Color Warning { get; } = new Color(0.639f, 0.447f, 0.075f, 1f);
        internal Color Danger { get; } = new Color(0.722f, 0.239f, 0.243f, 1f);
        internal Color Gold { get; } = new Color(0.702f, 0.510f, 0.098f, 1f);
        internal Color Purple { get; } = new Color(0.463f, 0.322f, 0.694f, 1f);
        internal Color Blue { get; } = new Color(0.145f, 0.400f, 0.702f, 1f);
        internal Color GoodSoft { get; } = new Color(0.878f, 0.945f, 0.910f, 1f);
        internal Color WarningSoft { get; } = new Color(0.984f, 0.941f, 0.867f, 1f);
        internal Color DangerSoft { get; } = new Color(0.961f, 0.906f, 0.910f, 1f);
        internal Color BlueSoft { get; } = new Color(0.906f, 0.933f, 0.976f, 1f);

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
                palette.PanelBorder = PanelBorder;
                palette.Header = Header;
                palette.HeaderLine = HeaderLine;
                palette.Card = Card;
                palette.CardBorder = CardBorder;
                palette.CardRaised = CardRaised;
                palette.Accent = Accent;
                palette.AccentDim = AccentDeep;
                palette.Text = Text;
                palette.TextDim = TextDim;
                palette.TextFaint = TextFaint;
                palette.Good = Good;
                palette.Warning = Warning;
                palette.Danger = Danger;
                palette.Gold = Gold;
                palette.Purple = Purple;
                palette.Blue = Blue;
                palette.GoodSoft = GoodSoft;
                palette.WarningSoft = WarningSoft;
                palette.DangerSoft = DangerSoft;
                palette.BlueSoft = BlueSoft;
                palette.Primary = Accent;
                palette.PrimaryHover = new Color(0.129f, 0.580f, 0.318f, 1f);
                palette.OnPrimary = Color.white;
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
            _dim = Solid(new Color(0.176f, 0.216f, 0.278f, 0.42f), "CaiYaStation.Dim");
            _panel = Rounded(72, 16, Panel, PanelBorder, 2, "CaiYaStation.Panel");
            _header = Rounded(48, 14, Header, HeaderLine, 1, "CaiYaStation.Header");
            _card = Rounded(48, 12, Card, CardBorder, 1, "CaiYaStation.Card");
            _cardSelected = Rounded(48, 12, CardRaised, Accent, 1, "CaiYaStation.CardSelected");
            _rowHover = Rounded(32, 8, new Color(0.145f, 0.192f, 0.251f, 0.05f), null, 0, "CaiYaStation.RowHover");
            _rowSelected = Rounded(32, 8, new Color(0.094f, 0.514f, 0.263f, 0.13f), null, 0, "CaiYaStation.RowSelected");
            _button = Rounded(40, 10, new Color(0.957f, 0.967f, 0.980f, 1f), new Color(0.780f, 0.816f, 0.863f, 1f), 1, "CaiYaStation.Button");
            _buttonHover = Rounded(40, 10, new Color(0.918f, 0.941f, 0.969f, 1f), new Color(0.639f, 0.702f, 0.780f, 1f), 1, "CaiYaStation.ButtonHover");
            _buttonActive = Rounded(40, 10, new Color(0.094f, 0.514f, 0.263f, 1f), new Color(0.059f, 0.404f, 0.204f, 1f), 1, "CaiYaStation.ButtonActive");
            _buttonDisabled = Rounded(40, 10, new Color(0.925f, 0.933f, 0.945f, 1f), new Color(0.851f, 0.871f, 0.890f, 1f), 1, "CaiYaStation.ButtonDisabled");
            _pill = Rounded(36, 16, new Color(0.969f, 0.980f, 0.992f, 0.96f), new Color(0.796f, 0.831f, 0.878f, 0.95f), 1, "CaiYaStation.Pill");
            _check = Rounded(24, 6, Accent, null, 0, "CaiYaStation.Check");
            _riskTrack = Rounded(24, 8, new Color(0.902f, 0.922f, 0.945f, 1f), new Color(0.800f, 0.831f, 0.871f, 1f), 1, "CaiYaStation.RiskTrack");

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
            ButtonPrimary.normal.textColor = new Color(1f, 1f, 1f, 1f);
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
                // The game's own label style carries padding, and a CJK line box is taller than
                // the nominal font size (YaHei at 12px measures ~17px). With Clip plus inherited
                // padding every short rect sliced the top and bottom off the glyphs in game, while
                // the offline preview looked fine because it measures text itself. Zero the
                // padding and let a slightly short rect draw the text outside its own bounds.
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                clipping = TextClipping.Overflow
            };
            style.normal.textColor = color;
            return style;
        }

        /// <summary>
        /// Trims a label to the width it is allowed to occupy. Styles now draw with Overflow so a
        /// short rect can no longer slice a glyph, which also means a long item name would run into
        /// the number column next to it; this keeps every cell inside its own width.
        /// </summary>
        internal static string Fit(GUIStyle style, string text, float width)
        {
            if (string.IsNullOrEmpty(text) || width <= 0f) return text ?? string.Empty;
            if (style.CalcSize(new GUIContent(text)).x <= width) return text;
            const string ellipsis = "…";
            var trimmed = text;
            while (trimmed.Length > 1)
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
                if (style.CalcSize(new GUIContent(trimmed + ellipsis)).x <= width) return trimmed + ellipsis;
            }
            return ellipsis;
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
                GUI.color = new Color(1f, 1f, 1f, 1f);
                GUI.Label(rect, "✓", Badge);
                GUI.color = previous;
                return;
            }
            var previousColor = GUI.color;
            GUI.color = new Color(0.647f, 0.694f, 0.749f, 1f);
            GUI.DrawTexture(rect, _white, ScaleMode.StretchToFill, true);
            GUI.color = new Color(0.969f, 0.980f, 0.992f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), _white);
            GUI.color = previousColor;
        }

        internal static Color QualityColor(int quality)
        {
            if (quality >= 9) return new Color(0.702f, 0.478f, 0.055f, 1f);
            if (quality >= 7) return new Color(0.478f, 0.290f, 0.702f, 1f);
            if (quality >= 5) return new Color(0.129f, 0.380f, 0.722f, 1f);
            if (quality >= 3) return new Color(0.086f, 0.478f, 0.286f, 1f);
            return new Color(0.478f, 0.525f, 0.584f, 1f);
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
