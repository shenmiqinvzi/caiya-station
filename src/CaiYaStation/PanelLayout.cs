using System.Collections.Generic;
using UnityEngine;

namespace CaiYaStation
{
    /// <summary>
    /// Pure geometry for the shipment panel. Everything the panel draws comes from here, so the
    /// layout can be checked offline (see tools/PanelPreview) before it ever runs in game.
    /// No GUI, textures or game APIs are touched: only Rect/Mathf, which run headless.
    ///
    /// The old panel positioned the right column with hard-coded Y offsets (manifest 150, summary
    /// 168, alerts at right.yMax - 88). The cost copy is taller than 168 and the alert block sat on
    /// top of the third escort card, which is exactly the pile-up visible in game. Here every
    /// section is allocated from a single vertical budget, so two sections cannot claim one band.
    /// </summary>
    internal readonly struct PanelLayout
    {
        internal const float HeaderHeight = 76f;
        internal const float BodyTopGap = 14f;
        internal const float FooterHeight = 64f;
        internal const float FooterMargin = 12f;
        internal const float FooterRuleGap = 10f;
        internal const float SidePadding = 20f;
        internal const float RailWidth = 340f;
        internal const float ColumnGap = 16f;
        internal const float ListRowHeight = 40f;
        internal const float ManifestRowHeight = 30f;
        internal const float EscortCardHeight = 44f;
        internal const float EscortCardGap = 5f;
        internal const float EscortCardTop = 24f;
        internal const int EscortCardCount = 3;

        /// <summary>Derived from the cards, so adding a fourth option can never clip the last one.</summary>
        internal const float EscortHeight = EscortCardTop + EscortCardHeight * EscortCardCount
                                            + EscortCardGap * (EscortCardCount - 1) + 2f;
        internal const float CostHeight = 160f;
        internal const float RailPadding = 14f;
        internal const float RailGap = 10f;
        internal const float MinManifestHeight = 96f;

        internal readonly Rect Panel;
        internal readonly Rect Header;
        internal readonly Rect HeaderIcon;
        internal readonly Rect HeaderTitle;
        internal readonly Rect HeaderBadge;
        internal readonly Rect HeaderSubtitle;
        internal readonly Rect HeaderClose;
        internal readonly Rect HeaderRule;
        internal readonly Rect Body;
        internal readonly Rect Left;
        internal readonly Rect Right;
        internal readonly Rect ListTitle;
        internal readonly Rect ListCapacity;
        internal readonly Rect ListCard;
        internal readonly Rect ListColumns;
        internal readonly Rect ListView;
        internal readonly Rect ListHint;
        internal readonly float ListContentWidth;
        internal readonly Rect RailCard;
        internal readonly Rect Manifest;
        internal readonly Rect ManifestTitle;
        internal readonly Rect ManifestCount;
        internal readonly Rect ManifestView;
        internal readonly Rect ManifestHint;
        internal readonly Rect Cost;
        internal readonly Rect CostTitle;
        internal readonly Rect RiskCap;
        internal readonly Rect MetricRowOne;
        internal readonly Rect MetricRowTwo;
        internal readonly Rect CostRule;
        internal readonly Rect TotalLabel;
        internal readonly Rect TotalValue;
        internal readonly Rect RiskTrack;
        internal readonly Rect RiskLegend;
        internal readonly Rect Escort;
        internal readonly Rect EscortTitle;
        internal readonly Rect EscortHint;
        internal readonly Rect Footer;
        internal readonly Rect FooterRule;
        internal readonly Rect SelectAllButton;
        internal readonly Rect ClearButton;
        internal readonly Rect ConfirmButton;
        internal readonly Rect CloseButton;
        internal readonly Rect BalanceLabel;
        internal readonly Rect BalanceValue;
        internal readonly Rect CashLabel;
        internal readonly Rect CashValue;
        internal readonly Rect FooterSummary;
        internal readonly Rect FooterStatus;

        private PanelLayout(Rect panel)
        {
            Panel = panel;
            var x = panel.x;
            var y = panel.y;
            var w = panel.width;
            var h = panel.height;

            Header = new Rect(x, y, w, HeaderHeight);
            HeaderIcon = new Rect(x + 20, y + 18, 40, 40);
            HeaderTitle = new Rect(x + 72, y + 6, 106, 30);
            HeaderBadge = new Rect(x + 186, y + 10, 64, 22);
            HeaderSubtitle = new Rect(x + 72, y + 42, w - 150, 17);
            HeaderClose = new Rect(x + w - 54, y + 18, 34, 38);
            HeaderRule = new Rect(x + SidePadding, y + HeaderHeight - 1, w - SidePadding * 2, 1);

            Footer = new Rect(x + SidePadding, y + h - FooterMargin - FooterHeight,
                w - SidePadding * 2, FooterHeight);
            FooterRule = new Rect(Footer.x, Footer.y - FooterRuleGap, Footer.width, 1);
            var bodyTop = y + HeaderHeight + BodyTopGap;
            var bodyBottom = FooterRule.y - 8f;
            Body = new Rect(x + SidePadding, bodyTop, w - SidePadding * 2,
                Mathf.Max(0f, bodyBottom - bodyTop));

            var leftWidth = Mathf.Max(240f, Body.width - RailWidth - ColumnGap);
            Left = new Rect(Body.x, Body.y, leftWidth, Body.height);
            Right = new Rect(Body.x + leftWidth + ColumnGap, Body.y, RailWidth, Body.height);

            ListTitle = new Rect(Left.x, Left.y, Mathf.Max(80f, Left.width - 274), 22);
            ListCapacity = new Rect(Left.x + Left.width - 260, Left.y + 3, 260, 17);
            ListCard = new Rect(Left.x, Left.y + 26, Left.width, Mathf.Max(160f, Left.height - 26));
            ListView = new Rect(ListCard.x + 6, ListCard.y + 27, ListCard.width - 12,
                Mathf.Max(60f, ListCard.height - 27 - 24));
            ListHint = new Rect(ListCard.x + 14, ListCard.yMax - 19, ListCard.width - 28, 13);
            ListContentWidth = ListView.width - 14;
            ListColumns = new Rect(ListView.x + 2, ListCard.y + 8, ListContentWidth - 4, 16);

            RailCard = Right;
            var railX = RailCard.x + RailPadding;
            var railW = RailCard.width - RailPadding * 2;
            var manifestHeight = Mathf.Clamp(
                RailCard.height - (RailPadding * 2 + RailGap * 2 + CostHeight + EscortHeight),
                MinManifestHeight, 168f);

            Manifest = new Rect(railX, RailCard.y + RailPadding, railW, manifestHeight);
            ManifestTitle = new Rect(Manifest.x, Manifest.y + 7, Manifest.width - 100, 16);
            ManifestCount = new Rect(Manifest.x + Manifest.width - 96, Manifest.y + 7, 88, 16);
            ManifestView = new Rect(Manifest.x + 6, Manifest.y + 26, Manifest.width - 12,
                Mathf.Max(34f, manifestHeight - 26 - 20));
            ManifestHint = new Rect(Manifest.x + 8, Manifest.yMax - 17, Manifest.width - 16, 13);

            Cost = new Rect(railX, Manifest.yMax + RailGap, railW, CostHeight);
            CostTitle = new Rect(Cost.x + 14, Cost.y + 10, 120, 16);
            RiskCap = new Rect(Cost.x + Cost.width - 176, Cost.y + 139, 162, 14);
            MetricRowOne = new Rect(Cost.x + 14, Cost.y + 32, Cost.width - 28, 20);
            MetricRowTwo = new Rect(Cost.x + 14, Cost.y + 54, Cost.width - 28, 20);
            CostRule = new Rect(Cost.x + 14, Cost.y + 80, Cost.width - 28, 1);
            TotalLabel = new Rect(Cost.x + 14, Cost.y + 88, 128, 30);
            TotalValue = new Rect(Cost.x + Cost.width - 160, Cost.y + 84, 146, 34);
            RiskTrack = new Rect(Cost.x + 14, Cost.y + 126, Cost.width - 28, 8);
            RiskLegend = new Rect(Cost.x + 14, Cost.y + 139, Cost.width - 28, 14);

            Escort = new Rect(railX, Cost.yMax + RailGap, railW, EscortHeight);
            EscortTitle = new Rect(Escort.x, Escort.y + 1, 140, 17);
            EscortHint = new Rect(Escort.x + Escort.width - 160, Escort.y + 2, 160, 16);

            var buttonY = Footer.y + 15;
            CloseButton = new Rect(Footer.xMax - 76, buttonY, 76, 34);
            ConfirmButton = new Rect(CloseButton.x - 8 - 152, buttonY, 152, 34);
            ClearButton = new Rect(ConfirmButton.x - 8 - 76, buttonY, 76, 34);
            SelectAllButton = new Rect(ClearButton.x - 8 - 104, buttonY, 104, 34);

            BalanceLabel = new Rect(Footer.x, Footer.y + 7, 34, 14);
            BalanceValue = new Rect(Footer.x + 40, Footer.y + 3, 96, 20);
            CashLabel = new Rect(Footer.x + 146, Footer.y + 7, 34, 14);
            CashValue = new Rect(Footer.x + 186, Footer.y + 3, 116, 20);
            var textWidth = Mathf.Max(120f, SelectAllButton.x - Footer.x - 16);
            FooterSummary = new Rect(Footer.x, Footer.y + 27, textWidth, 16);
            FooterStatus = new Rect(Footer.x, Footer.y + 45, textWidth, 14);
        }

        internal static PanelLayout Compute(Rect panel) => new PanelLayout(panel);

        /// <summary>Panel rect for a given logical screen size, clamped into the screen.</summary>
        internal static Rect PanelRect(float logicalWidth, float logicalHeight, float designWidth, float designHeight)
        {
            var width = Mathf.Min(designWidth, logicalWidth - 32f);
            var height = Mathf.Min(designHeight, logicalHeight - 32f);
            return new Rect((logicalWidth - width) * 0.5f, (logicalHeight - height) * 0.5f, width, height);
        }

        internal Rect ListRowRect(int index, float scroll)
        {
            return new Rect(ListView.x + 2, ListView.y + index * ListRowHeight - scroll + 2,
                ListContentWidth - 4, ListRowHeight - 4);
        }

        internal Rect ManifestRowRect(int index, float scroll)
        {
            return new Rect(ManifestView.x + 2, ManifestView.y + index * ManifestRowHeight - scroll + 1,
                ManifestView.width - 4, ManifestRowHeight - 2);
        }

        internal Rect EscortCardRect(int index)
        {
            return new Rect(Escort.x, Escort.y + EscortCardTop + index * (EscortCardHeight + EscortCardGap),
                Escort.width, EscortCardHeight);
        }

        internal float ListContentHeight(int count) =>
            Mathf.Max(ListView.height, count * ListRowHeight + 6f);

        internal float ManifestContentHeight(int count) =>
            Mathf.Max(ManifestView.height, count * ManifestRowHeight + 4f);

        internal float MaxListScroll(int count) =>
            Mathf.Max(0f, ListContentHeight(count) - ListView.height);

        internal float MaxManifestScroll(int count) =>
            Mathf.Max(0f, ManifestContentHeight(count) - ManifestView.height);

        /// <summary>Named rects for the offline checks and the preview renderer.</summary>
        internal void CollectRects(List<KeyValuePair<string, Rect>> into)
        {
            into.Add(new KeyValuePair<string, Rect>("Panel", Panel));
            into.Add(new KeyValuePair<string, Rect>("Header", Header));
            into.Add(new KeyValuePair<string, Rect>("HeaderIcon", HeaderIcon));
            into.Add(new KeyValuePair<string, Rect>("HeaderTitle", HeaderTitle));
            into.Add(new KeyValuePair<string, Rect>("HeaderBadge", HeaderBadge));
            into.Add(new KeyValuePair<string, Rect>("HeaderSubtitle", HeaderSubtitle));
            into.Add(new KeyValuePair<string, Rect>("HeaderClose", HeaderClose));
            into.Add(new KeyValuePair<string, Rect>("Body", Body));
            into.Add(new KeyValuePair<string, Rect>("Left", Left));
            into.Add(new KeyValuePair<string, Rect>("Right", Right));
            into.Add(new KeyValuePair<string, Rect>("ListTitle", ListTitle));
            into.Add(new KeyValuePair<string, Rect>("ListCapacity", ListCapacity));
            into.Add(new KeyValuePair<string, Rect>("ListCard", ListCard));
            into.Add(new KeyValuePair<string, Rect>("ListColumns", ListColumns));
            into.Add(new KeyValuePair<string, Rect>("ListView", ListView));
            into.Add(new KeyValuePair<string, Rect>("ListHint", ListHint));
            into.Add(new KeyValuePair<string, Rect>("RailCard", RailCard));
            into.Add(new KeyValuePair<string, Rect>("Manifest", Manifest));
            into.Add(new KeyValuePair<string, Rect>("ManifestTitle", ManifestTitle));
            into.Add(new KeyValuePair<string, Rect>("ManifestCount", ManifestCount));
            into.Add(new KeyValuePair<string, Rect>("ManifestView", ManifestView));
            into.Add(new KeyValuePair<string, Rect>("ManifestHint", ManifestHint));
            into.Add(new KeyValuePair<string, Rect>("Cost", Cost));
            into.Add(new KeyValuePair<string, Rect>("CostTitle", CostTitle));
            into.Add(new KeyValuePair<string, Rect>("RiskCap", RiskCap));
            into.Add(new KeyValuePair<string, Rect>("MetricRowOne", MetricRowOne));
            into.Add(new KeyValuePair<string, Rect>("MetricRowTwo", MetricRowTwo));
            into.Add(new KeyValuePair<string, Rect>("CostRule", CostRule));
            into.Add(new KeyValuePair<string, Rect>("TotalLabel", TotalLabel));
            into.Add(new KeyValuePair<string, Rect>("TotalValue", TotalValue));
            into.Add(new KeyValuePair<string, Rect>("RiskTrack", RiskTrack));
            into.Add(new KeyValuePair<string, Rect>("RiskLegend", RiskLegend));
            into.Add(new KeyValuePair<string, Rect>("Escort", Escort));
            into.Add(new KeyValuePair<string, Rect>("EscortTitle", EscortTitle));
            into.Add(new KeyValuePair<string, Rect>("EscortHint", EscortHint));
            into.Add(new KeyValuePair<string, Rect>("Footer", Footer));
            into.Add(new KeyValuePair<string, Rect>("SelectAllButton", SelectAllButton));
            into.Add(new KeyValuePair<string, Rect>("ClearButton", ClearButton));
            into.Add(new KeyValuePair<string, Rect>("ConfirmButton", ConfirmButton));
            into.Add(new KeyValuePair<string, Rect>("CloseButton", CloseButton));
            into.Add(new KeyValuePair<string, Rect>("BalanceLabel", BalanceLabel));
            into.Add(new KeyValuePair<string, Rect>("BalanceValue", BalanceValue));
            into.Add(new KeyValuePair<string, Rect>("CashLabel", CashLabel));
            into.Add(new KeyValuePair<string, Rect>("CashValue", CashValue));
            into.Add(new KeyValuePair<string, Rect>("FooterSummary", FooterSummary));
            into.Add(new KeyValuePair<string, Rect>("FooterStatus", FooterStatus));
        }
    }
}

