using UnityEngine;

namespace CaiYaStation
{
    /// <summary>
    /// Pure geometry for the delivery receipt sheet, kept free of IMGUI so it can be checked
    /// offline (tools/PanelPreview) on every supported resolution.
    ///
    /// The first version hard-coded the chip strip at y+84 with height 52 while each chip drew
    /// three lines up to y+52, so the sub line was clipped by the card edge; the four chips and
    /// the close button were also placed with absolute x offsets that ran off a narrow panel.
    /// Here every band is derived from one vertical budget measured from both ends of the sheet,
    /// and the chip strip is split evenly from the content width.
    /// </summary>
    internal readonly struct ReceiptLayout
    {
        internal const float Padding = 24f;
        internal const float HeaderHeight = 68f;
        internal const float ChipTopGap = 16f;
        internal const float ChipHeight = 66f;
        internal const float ChipGap = 12f;
        internal const float ChipLabelTop = 5f;
        internal const float ChipValueTop = 21f;
        internal const float ChipSubTop = 48f;
        /// <summary>Bottom of the lowest chip line relative to the chip top.</summary>
        internal const float ChipContentBottom = 64f;
        internal const float ListGap = 14f;
        internal const float ColumnGap = 12f;
        internal const float ListTitleHeight = 40f;
        internal const float ListFooterHeight = 22f;
        internal const float ListRowHeight = 24f;
        internal const float ListMinHeight = 72f;
        internal const float NarrativeHeight = 44f;
        internal const float NarrativeGap = 12f;
        internal const float HeadlineHeight = 22f;
        internal const float FlavorHeight = 20f;
        internal const float ButtonHeight = 32f;
        internal const float ButtonWidth = 100f;
        internal const float BottomMargin = 14f;
        internal const float MaxWidth = 760f;
        internal const float MaxHeight = 570f;
        internal const float ScreenMargin = 20f;
        internal const float HeaderCloseWidth = 36f;
        internal const float HeaderCloseHeight = 34f;
        internal const float HeaderCloseTop = 16f;
        internal const float HeaderSubtitleTop = 42f;
        internal const float HeaderBadgeLeft = 150f;
        internal const float HeaderBadgeTop = 17f;
        internal const float HeaderBadgeWidth = 92f;
        internal const float HeaderBadgeHeight = 24f;

        internal readonly Rect Panel;
        internal readonly Rect Header;
        internal readonly Rect HeaderAccent;
        internal readonly Rect HeaderTitle;
        internal readonly Rect HeaderBadge;
        internal readonly Rect HeaderSubtitle;
        internal readonly Rect HeaderClose;
        internal readonly Rect ChipOne;
        internal readonly Rect ChipTwo;
        internal readonly Rect ChipThree;
        internal readonly Rect ChipFour;
        internal readonly Rect Delivered;
        internal readonly Rect Lost;
        internal readonly Rect Headline;
        internal readonly Rect Flavor;
        internal readonly Rect Button;
        internal readonly float ListHeight;

        private ReceiptLayout(Rect panel)
        {
            Panel = panel;
            Header = new Rect(panel.x, panel.y, panel.width, HeaderHeight);
            HeaderAccent = new Rect(panel.x, panel.y, 4f, HeaderHeight);
            HeaderTitle = new Rect(panel.x + 20f, panel.y + 10f, HeaderBadgeLeft - 28f, 32f);
            HeaderBadge = new Rect(HeaderTitle.xMax + 8f, panel.y + HeaderBadgeTop, HeaderBadgeWidth, HeaderBadgeHeight);
            HeaderSubtitle = new Rect(panel.x + 20f, panel.y + HeaderSubtitleTop, panel.width - 88f, 18f);
            HeaderClose = new Rect(panel.xMax - HeaderCloseWidth - 18f, panel.y + HeaderCloseTop, HeaderCloseWidth, HeaderCloseHeight);

            var contentWidth = panel.width - Padding * 2f;
            var chipWidth = (contentWidth - ChipGap * 3f) / 4f;
            var chipTop = panel.y + HeaderHeight + ChipTopGap;
            ChipOne = new Rect(panel.x + Padding, chipTop, chipWidth, ChipHeight);
            ChipTwo = new Rect(ChipOne.xMax + ChipGap, chipTop, chipWidth, ChipHeight);
            ChipThree = new Rect(ChipTwo.xMax + ChipGap, chipTop, chipWidth, ChipHeight);
            ChipFour = new Rect(ChipThree.xMax + ChipGap, chipTop, chipWidth, ChipHeight);

            Button = new Rect(panel.xMax - Padding - ButtonWidth, panel.yMax - BottomMargin - ButtonHeight,
                ButtonWidth, ButtonHeight);
            var narrativeTop = Button.y - NarrativeGap - NarrativeHeight;
            Headline = new Rect(panel.x + Padding, narrativeTop, contentWidth, HeadlineHeight);
            Flavor = new Rect(panel.x + Padding, narrativeTop + HeadlineHeight + 2f, contentWidth, FlavorHeight);

            var listTop = chipTop + ChipHeight + ListGap;
            var available = narrativeTop - NarrativeGap - listTop;
            ListHeight = Mathf.Max(ListMinHeight, available);
            var listWidth = (contentWidth - ColumnGap) * 0.5f;
            Delivered = new Rect(panel.x + Padding, listTop, listWidth, ListHeight);
            Lost = new Rect(Delivered.xMax + ColumnGap, listTop, listWidth, ListHeight);
        }

        internal static ReceiptLayout Compute(float logicalWidth, float logicalHeight)
        {
            var width = Mathf.Min(MaxWidth, logicalWidth - ScreenMargin * 2f);
            var height = Mathf.Min(MaxHeight, logicalHeight - ScreenMargin * 2f);
            if (width < 320f) width = 320f;
            if (height < 300f) height = 300f;
            var panel = new Rect((logicalWidth - width) * 0.5f, (logicalHeight - height) * 0.5f, width, height);
            return new ReceiptLayout(panel);
        }

        /// <summary>Rows that fit in a list card without touching the truncation footer.</summary>
        internal static int ListCapacity(float height)
        {
            var usable = height - ListTitleHeight - ListFooterHeight;
            if (usable <= 0f) return 0;
            return Mathf.FloorToInt(usable / ListRowHeight);
        }
    }
}
