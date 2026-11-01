using UnityEngine;

namespace CaiYaStation
{
    /// <summary>
    /// Pure geometry for the delivery receipt sheet and the loss notice, kept free of IMGUI so it
    /// can be checked offline (tools/PanelPreview) on every supported resolution.
    ///
    /// Layout, top to bottom: a header carrying the station sticker logo, the sheet title and the
    /// three-state outcome badge; an order-info card of six labelled fields in three columns; a
    /// strip of three stat cards (delivered / lost / paid); then the two goods tables, each of
    /// which scrolls on its own; and finally the footer with the hint line and the close button.
    /// Every band is derived from one vertical budget measured from both ends of the sheet, so a
    /// new band cannot push the footer off the panel.
    /// </summary>
    internal readonly struct ReceiptLayout
    {
        internal const float Padding = 20f;
        internal const float HeaderHeight = 96f;
        internal const float HeaderLogoSize = 56f;
        internal const float HeaderLogoLeft = 18f;
        internal const float HeaderLogoTop = 20f;
        internal const float HeaderAccentWidth = 4f;
        internal const float HeaderTitleLeft = 88f;
        internal const float HeaderTitleTop = 16f;
        internal const float HeaderTitleHeight = 34f;
        internal const float HeaderSubtitleTop = 52f;
        internal const float HeaderSubtitleHeight = 20f;
        internal const float HeaderBadgeWidth = 108f;
        internal const float HeaderBadgeHeight = 28f;
        internal const float HeaderBadgeTop = 24f;
        internal const float HeaderCloseWidth = 34f;
        internal const float HeaderCloseHeight = 34f;
        internal const float HeaderCloseTop = 16f;
        internal const float SectionGap = 12f;
        internal const float InfoHeight = 96f;
        internal const float InfoTitleHeight = 30f;
        internal const float InfoRowHeight = 28f;
        internal const float InfoLabelWidth = 72f;
        internal const float InfoColumnGap = 14f;
        internal const float StatHeight = 100f;
        internal const float StatGap = 12f;
        internal const float StatLabelTop = 8f;
        internal const float StatLabelHeight = 20f;
        internal const float StatValueTop = 28f;
        internal const float StatValueHeight = 26f;
        internal const float StatSubTop = 56f;
        internal const float StatSubHeight = 20f;
        internal const float StatSub2Top = 76f;
        internal const float StatSub2Height = 20f;
        internal const float ListGap = 12f;
        /// <summary>Title line plus the column header line, so neither collides with the first row.</summary>
        internal const float ListTitleHeight = 46f;
        internal const float ListTitleTop = 6f;
        internal const float ListTitleTextHeight = 20f;
        internal const float ListHeaderTop = 30f;
        internal const float ListHeaderHeight = 14f;
        internal const float ListFooterHeight = 22f;
        internal const float ListRowHeight = 26f;
        internal const float ListRowTextHeight = 24f;
        internal const float LostHintHeight = 68f;
        internal const float ListIconSize = 20f;
        internal const float ListIconLeft = 10f;
        internal const float ListStatusWidth = 74f;
        internal const float ListTotalWidth = 88f;
        internal const float ListUnitWidth = 76f;
        internal const float ListCountWidth = 52f;
        internal const float ListNameLeft = 38f;
        /// <summary>Breathing room between the last column and the card edge, so status text is not flush.</summary>
        internal const float ListRightPadding = 14f;
        internal const float FooterHintHeight = 22f;
        internal const float ButtonHeight = 40f;
        internal const float ButtonWidth = 168f;
        internal const float BottomMargin = 16f;
        internal const float MaxWidth = 880f;
        internal const float MaxHeight = 660f;
        internal const float ScreenMargin = 20f;

        internal readonly Rect Panel;
        internal readonly Rect Header;
        internal readonly Rect HeaderAccent;
        internal readonly Rect HeaderLogo;
        internal readonly Rect HeaderTitle;
        internal readonly Rect HeaderSubtitle;
        internal readonly Rect HeaderBadge;
        internal readonly Rect HeaderClose;
        internal readonly Rect InfoCard;
        internal readonly Rect StatOne;
        internal readonly Rect StatTwo;
        internal readonly Rect StatThree;
        internal readonly Rect Delivered;
        internal readonly Rect Lost;
        internal readonly Rect FooterHint;
        internal readonly Rect Button;
        /// <summary>Height of the delivered table; the lost table owns the rest of the budget.</summary>
        internal readonly float ListHeight;

        private ReceiptLayout(Rect panel, bool lostEmpty)
        {
            Panel = panel;
            Header = new Rect(panel.x, panel.y, panel.width, HeaderHeight);
            HeaderAccent = new Rect(panel.x, panel.y, HeaderAccentWidth, HeaderHeight);
            HeaderLogo = new Rect(panel.x + HeaderLogoLeft, panel.y + HeaderLogoTop, HeaderLogoSize, HeaderLogoSize);
            HeaderClose = new Rect(panel.xMax - Padding - HeaderCloseWidth, panel.y + HeaderCloseTop,
                HeaderCloseWidth, HeaderCloseHeight);
            HeaderBadge = new Rect(HeaderClose.x - HeaderBadgeWidth - 10f, panel.y + HeaderBadgeTop,
                HeaderBadgeWidth, HeaderBadgeHeight);
            HeaderTitle = new Rect(panel.x + HeaderTitleLeft, panel.y + HeaderTitleTop,
                HeaderBadge.x - (panel.x + HeaderTitleLeft) - 10f, HeaderTitleHeight);
            HeaderSubtitle = new Rect(panel.x + HeaderTitleLeft, panel.y + HeaderSubtitleTop,
                HeaderBadge.x - (panel.x + HeaderTitleLeft) - 10f, HeaderSubtitleHeight);

            var contentWidth = panel.width - Padding * 2f;
            InfoCard = new Rect(panel.x + Padding, panel.y + HeaderHeight + SectionGap, contentWidth, InfoHeight);

            var statTop = InfoCard.yMax + SectionGap;
            var statWidth = (contentWidth - StatGap * 2f) / 3f;
            StatOne = new Rect(panel.x + Padding, statTop, statWidth, StatHeight);
            StatTwo = new Rect(StatOne.xMax + StatGap, statTop, statWidth, StatHeight);
            StatThree = new Rect(StatTwo.xMax + StatGap, statTop, statWidth, StatHeight);

            Button = new Rect(panel.xMax - Padding - ButtonWidth, panel.yMax - BottomMargin - ButtonHeight,
                ButtonWidth, ButtonHeight);
            FooterHint = new Rect(panel.x + Padding, Button.y + (ButtonHeight - FooterHintHeight) * 0.5f,
                contentWidth - ButtonWidth - 16f, FooterHintHeight);

            var listTop = statTop + StatHeight + ListGap;
            var available = Button.y - SectionGap - listTop;
            // Both tables are built from whole rows: title + column header + N rows + footer. Splitting
            // the leftover space as fractions left a sliced half-row at the clip edge, so the budget is
            // spent in rows and the remainder stays as breathing room above the footer.
            var frame = ListTitleHeight + ListFooterHeight;
            var budget = Mathf.Max(0f, available - ListGap);
            var rowBudget = Mathf.Max(0, Mathf.FloorToInt((budget - frame * 2f) / ListRowHeight));
            float deliveredHeight;
            float lostHeight;
            if (lostEmpty)
            {
                lostHeight = LostHintHeight;
                var deliveredBudget = Mathf.Max(0f, budget - lostHeight);
                var rows = Mathf.Max(0, Mathf.FloorToInt((deliveredBudget - frame) / ListRowHeight));
                deliveredHeight = Mathf.Min(deliveredBudget, frame + rows * ListRowHeight);
            }
            else
            {
                var lostRows = rowBudget <= 1 ? rowBudget : Mathf.Clamp(Mathf.RoundToInt(rowBudget * 0.42f), 1,
                    rowBudget - 1);
                var deliveredRows = rowBudget - lostRows;
                lostHeight = frame + lostRows * ListRowHeight;
                deliveredHeight = frame + deliveredRows * ListRowHeight;
            }

            ListHeight = Mathf.Max(0f, deliveredHeight);
            Delivered = new Rect(panel.x + Padding, listTop, contentWidth, ListHeight);
            Lost = new Rect(panel.x + Padding, Delivered.yMax + ListGap, contentWidth, Mathf.Max(0f, lostHeight));
        }

        internal static ReceiptLayout Compute(float logicalWidth, float logicalHeight) =>
            Compute(logicalWidth, logicalHeight, false);

        internal static ReceiptLayout Compute(float logicalWidth, float logicalHeight, bool lostEmpty)
        {
            var width = Mathf.Min(MaxWidth, logicalWidth - ScreenMargin * 2f);
            var height = Mathf.Min(MaxHeight, logicalHeight - ScreenMargin * 2f);
            if (width < 360f) width = 360f;
            if (height < 320f) height = 320f;
            var panel = new Rect((logicalWidth - width) * 0.5f, (logicalHeight - height) * 0.5f, width, height);
            return new ReceiptLayout(panel, lostEmpty);
        }

        /// <summary>Rows that fit in a goods table without touching the truncation footer.</summary>
        internal static int ListCapacity(float height)
        {
            var usable = height - ListTitleHeight - ListFooterHeight;
            if (usable <= 0f) return 0;
            return Mathf.FloorToInt(usable / ListRowHeight);
        }

        /// <summary>
        /// The scrolling window of a goods table: the card minus its title and footer, snapped down
        /// to whole rows so a half-visible row never shows sliced glyphs at the clip edge.
        /// </summary>
        internal static Rect ListView(Rect card)
        {
            var usable = card.height - ListTitleHeight - ListFooterHeight;
            var rows = usable <= 0f ? 0f : Mathf.Floor(usable / ListRowHeight);
            return new Rect(card.x, card.y + ListTitleHeight, card.width, rows * ListRowHeight);
        }

        /// <summary>Row rect for one goods table, in the table's own scroll order.</summary>
        internal static Rect ListRow(Rect card, int index) =>
            new Rect(card.x, card.y + ListTitleHeight + index * ListRowHeight, card.width, ListRowTextHeight);

        /// <summary>Columns of one goods row: icon, name, count, unit, total, status.</summary>
        internal static void ListColumns(Rect row, out Rect icon, out Rect name, out Rect count, out Rect unit,
            out Rect total, out Rect status)
        {
            icon = new Rect(row.x + ListIconLeft, row.y + (row.height - ListIconSize) * 0.5f, ListIconSize,
                ListIconSize);
            status = new Rect(row.xMax - ListRightPadding - ListStatusWidth, row.y, ListStatusWidth, row.height);
            total = new Rect(status.x - ListTotalWidth, row.y, ListTotalWidth, row.height);
            unit = new Rect(total.x - ListUnitWidth, row.y, ListUnitWidth, row.height);
            count = new Rect(unit.x - ListCountWidth, row.y, ListCountWidth, row.height);
            var nameLeft = row.x + ListNameLeft;
            name = new Rect(nameLeft, row.y, Mathf.Max(0f, count.x - nameLeft - 8f), row.height);
        }

        /// <summary>Six info fields in three columns: label rect on the left, value on the right.</summary>
        internal void InfoField(int index, out Rect label, out Rect value)
        {
            var column = index % 3;
            var line = index / 3;
            var columnWidth = (InfoCard.width - Padding * 2f - InfoColumnGap * 2f) / 3f;
            var x = InfoCard.x + Padding + column * (columnWidth + InfoColumnGap);
            var y = InfoCard.y + InfoTitleHeight + 4f + line * InfoRowHeight;
            label = new Rect(x, y, InfoLabelWidth, ListRowTextHeight);
            value = new Rect(x + InfoLabelWidth, y, Mathf.Max(0f, columnWidth - InfoLabelWidth), ListRowTextHeight);
        }
    }
}
