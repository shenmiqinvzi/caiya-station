using System;
using System.Collections.Generic;
using System.Globalization;
using CaiYaStation.Domain;
using UnityEngine;

namespace CaiYaStation
{
    /// <summary>
    /// Draws the delivery receipt and the loss notice from <see cref="ReceiptLayout"/> geometry.
    ///
    /// It talks only to <see cref="IPanelPainter"/> and <see cref="PanelPalette"/> — never to
    /// <see cref="UiTheme"/> and never to GUI.Button — so tools/PanelPreview can replay both
    /// screens offline through <c>RecordPainter</c> and assert the layout on every resolution.
    /// Click handling stays in ShipmentPanel, which tests the rects this class paints.
    /// </summary>
    internal sealed class ReceiptRenderer
    {
        private readonly IPanelPainter _painter;
        private readonly PanelPalette _palette;
        private readonly Func<int, Sprite?> _iconFor;
        private readonly Func<int, string> _nameFor;
        private readonly Action<string>? _log;

        internal ReceiptRenderer(IPanelPainter painter, PanelPalette palette, Func<int, Sprite?> iconFor,
            Func<int, string> nameFor, Action<string>? log = null)
        {
            _painter = painter;
            _palette = palette;
            _iconFor = iconFor;
            _nameFor = nameFor;
            _log = log;
        }

        // ------------------------------------------------------------------ receipt sheet

        /// <summary>
        /// Full receipt. <paramref name="data"/> is null only for legacy text-only receipts, which
        /// are printed verbatim in the body instead of being re-derived from fields they never had.
        /// </summary>
        internal void DrawReceipt(ReceiptLayout layout, float logicalWidth, float logicalHeight, Receipt? data,
            string? legacyText, Sprite? stationIcon, ref float deliveredScroll, ref float lostScroll)
        {
            var panel = layout.Panel;
            _painter.Solid(new Rect(0f, 0f, logicalWidth, logicalHeight), _palette.Backdrop);
            _painter.Rounded(new Rect(panel.x + 2f, panel.y + 6f, panel.width, panel.height), 20f,
                PanelPalette.Alpha(_palette.Text, 0.10f), null, 0f);
            _painter.Rounded(panel, 18f, _palette.Panel, _palette.PanelBorder, 1f);

            DrawHeader(layout, stationIcon, data == null ? "旧版回执" : OutcomeLabel(data.Outcome),
                data == null ? _palette.TextDim : OutcomeColor(data.Outcome), "菜鸭驿站 · 运输回执",
                "DUCK STATION · DELIVERY RECEIPT", _palette.Accent, _palette.Accent);

            if (data == null)
            {
                var body = new Rect(panel.x + ReceiptLayout.Padding, layout.InfoCard.y,
                    panel.width - ReceiptLayout.Padding * 2f, layout.Button.y - layout.InfoCard.y - 14f);
                _painter.Rounded(body, 12f, _palette.Card, _palette.CardBorder, 1f);
                _painter.Text(new Rect(body.x + 16f, body.y + 14f, body.width - 32f, body.height - 28f),
                    legacyText ?? "", PaintText.Body, TextAnchor.UpperLeft, null, true);
                DrawButton(layout.Button, "确认并关闭", true);
                return;
            }

            DrawOrderInfo(layout, data);
            DrawStats(layout, data);
            DrawGoodsTable(layout.Delivered, "送达货物", data.Delivered, false, ref deliveredScroll);
            if (data.LostStacks == 0) DrawNoLossHint(layout.Lost);
            else DrawGoodsTable(layout.Lost, "丢失货物", data.Lost, true, ref lostScroll);

            _painter.Text(layout.FooterHint, "成功寄送的货物已送往地堡马蜂自提点，请前往领取。", PaintText.Small);
            DrawButton(layout.Button, "确认并关闭", true);
        }

        /// <summary>
        /// Loss notice. It is a prologue to the receipt: the only actions are "look at the receipt"
        /// and "acknowledge", and neither of them touches the settlement.
        /// </summary>
        internal void DrawLossNotice(Receipt receipt, float logicalWidth, float logicalHeight,
            ref float lostScroll, out Rect panel, out Rect lostCard, out Rect closeRect, out Rect fullReceiptRect,
            out Rect signRect)
        {
            var culprits = string.IsNullOrWhiteSpace(receipt.LossCulprits)
                ? Receipts.FallbackCulprits
                : receipt.LossCulprits;
            var kaomoji = string.IsNullOrWhiteSpace(receipt.LossKaomoji)
                ? Receipts.FallbackLossKaomoji
                : receipt.LossKaomoji;
            var text = "我们的人被" + culprits + "暴打了，货也弄丢了…" + kaomoji;

            var width = Mathf.Min(600f, logicalWidth - 40f);
            var innerWidth = width - ReceiptLayout.Padding * 2f;
            var textHeight = Mathf.Max(24f, _painter.Measure(text, innerWidth - 28f, PaintText.Label));
            var listHeight = Mathf.Clamp(receipt.Lost.Count * ReceiptLayout.ListRowHeight
                                         + ReceiptLayout.ListTitleHeight + ReceiptLayout.ListFooterHeight,
                96f, 220f);
            var height = 96f + textHeight + 16f + 12f + listHeight + 12f + 52f + 12f + ReceiptLayout.ButtonHeight + 20f;
            if (height > logicalHeight - 24f)
            {
                listHeight = Mathf.Max(72f, listHeight - (height - (logicalHeight - 24f)));
                height = 96f + textHeight + 16f + 12f + listHeight + 12f + 52f + 12f + ReceiptLayout.ButtonHeight + 20f;
            }
            var top = Mathf.Max(12f, (logicalHeight - height) * 0.5f);
            panel = new Rect((logicalWidth - width) * 0.5f, top, width, Mathf.Min(height, logicalHeight - 12f));

            _painter.Solid(new Rect(0f, 0f, logicalWidth, logicalHeight), _palette.Backdrop);
            _painter.Rounded(new Rect(panel.x + 2f, panel.y + 6f, panel.width, panel.height), 20f,
                PanelPalette.Alpha(_palette.Text, 0.10f), null, 0f);
            _painter.Rounded(panel, 18f, _palette.Panel, _palette.PanelBorder, 1f);

            var header = new Rect(panel.x, panel.y, panel.width, 84f);
            _painter.Rounded(new Rect(header.x, header.y, header.width, header.height + 14f), 18f,
                _palette.DangerSoft, null, 0f);
            _painter.Solid(new Rect(header.x, header.y, header.width, 5f), _palette.Danger);
            DrawLogo(new Rect(panel.x + ReceiptLayout.HeaderLogoLeft, panel.y + 14f, 56f, 56f), null,
                _palette.Danger);

            closeRect = new Rect(panel.xMax - ReceiptLayout.Padding - ReceiptLayout.HeaderCloseWidth,
                panel.y + ReceiptLayout.HeaderCloseTop, ReceiptLayout.HeaderCloseWidth,
                ReceiptLayout.HeaderCloseHeight);
            _painter.Text(new Rect(panel.x + ReceiptLayout.HeaderTitleLeft, panel.y + 12f,
                closeRect.x - panel.x - ReceiptLayout.HeaderTitleLeft - 12f, 34f), "运送损失通知单",
                PaintText.Title);
            _painter.Text(new Rect(panel.x + ReceiptLayout.HeaderTitleLeft, panel.y + 46f,
                closeRect.x - panel.x - ReceiptLayout.HeaderTitleLeft - 12f, 22f),
                "菜鸭驿站 · 配送事故备案 · 签收后不再追偿（雾）", PaintText.Subtitle);
            DrawCloseButton(closeRect);

            var cursor = panel.y + 100f;
            _painter.Rounded(new Rect(panel.x + ReceiptLayout.Padding, cursor, innerWidth, textHeight + 16f), 10f,
                _palette.DangerSoft, _palette.Danger, 1f);
            _painter.Text(new Rect(panel.x + ReceiptLayout.Padding + 14f, cursor + 8f, innerWidth - 28f, textHeight),
                text, PaintText.Label, TextAnchor.UpperLeft, _palette.Danger, true);
            cursor += textHeight + 16f + 12f;

            lostCard = new Rect(panel.x + ReceiptLayout.Padding, cursor, innerWidth, listHeight);
            DrawGoodsTable(lostCard, "丢失物品", receipt.Lost, true, ref lostScroll);
            cursor += listHeight + 12f;

            _painter.Text(new Rect(panel.x + ReceiptLayout.Padding, cursor, 200f, 22f), "总计损失价值",
                PaintText.Label);
            _painter.Text(new Rect(panel.xMax - ReceiptLayout.Padding - 200f, cursor, 200f, 24f),
                "-" + receipt.LostValue.ToString("N0", CultureInfo.InvariantCulture), PaintText.BigValue,
                TextAnchor.MiddleRight, _palette.Danger);
            _painter.Text(new Rect(panel.x + ReceiptLayout.Padding, cursor + 26f, innerWidth, 22f),
                "本次送达 " + receipt.DeliveredStacks + " 件 · 价值 "
                + receipt.DeliveredValue.ToString("N0", CultureInfo.InvariantCulture)
                + "　·　已扣运费 " + receipt.Fee.ToString("N0", CultureInfo.InvariantCulture), PaintText.Small);

            var buttonsTop = panel.yMax - 20f - ReceiptLayout.ButtonHeight;
            fullReceiptRect = new Rect(panel.x + ReceiptLayout.Padding, buttonsTop, 168f,
                ReceiptLayout.ButtonHeight);
            signRect = new Rect(panel.xMax - ReceiptLayout.Padding - 200f, buttonsTop, 200f,
                ReceiptLayout.ButtonHeight);
            DrawButton(fullReceiptRect, "查看完整回执", false);
            DrawButton(signRect, "签收（确认损失）", true);
        }

        // ------------------------------------------------------------------ bands

        private void DrawHeader(ReceiptLayout layout, Sprite? stationIcon, string badge, Color badgeColor,
            string title, string subtitle, Color accent, Color logoBorder)
        {
            var header = layout.Header;
            _painter.Rounded(new Rect(header.x, header.y, header.width, header.height + 14f), 18f,
                _palette.Header, null, 0f);
            _painter.Solid(new Rect(header.x, header.y, ReceiptLayout.HeaderAccentWidth, header.height), accent);
            DrawLogo(layout.HeaderLogo, stationIcon, logoBorder);
            _painter.Text(layout.HeaderTitle, title, PaintText.Title);
            _painter.Text(layout.HeaderSubtitle, subtitle, PaintText.Subtitle);

            var badgeRect = layout.HeaderBadge;
            _painter.Rounded(badgeRect, 14f, PanelPalette.Alpha(badgeColor, 0.14f),
                PanelPalette.Alpha(badgeColor, 0.45f), 1f);
            _painter.Text(badgeRect, badge, PaintText.Badge, TextAnchor.MiddleCenter, badgeColor);
            DrawCloseButton(layout.HeaderClose);
        }

        private void DrawLogo(Rect rect, Sprite? stationIcon, Color border)
        {
            _painter.Rounded(rect, 12f, _palette.CardRaised, PanelPalette.Alpha(border, 0.55f), 1f);
            _painter.Icon(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f), stationIcon,
                "菜", PaintText.MetricValue, border);
        }

        private void DrawCloseButton(Rect rect)
        {
            _painter.Rounded(rect, 9f, _palette.Card, _palette.CardBorder, 1f);
            _painter.Text(rect, "×", PaintText.Label, TextAnchor.MiddleCenter, _palette.TextDim);
        }

        private void DrawButton(Rect rect, string label, bool primary)
        {
            var fill = primary ? _palette.Primary : _palette.Button;
            var border = primary ? _palette.Primary : _palette.ButtonBorder;
            var textColor = primary ? _palette.OnPrimary : _palette.Text;
            _painter.Rounded(rect, 9f, fill, border, 1f);
            _painter.Text(rect, label, primary ? PaintText.ButtonPrimary : PaintText.Button,
                TextAnchor.MiddleCenter, textColor);
        }

        private void DrawOrderInfo(ReceiptLayout layout, Receipt receipt)
        {
            var card = layout.InfoCard;
            _painter.Rounded(card, 12f, _palette.Card, _palette.CardBorder, 1f);
            _painter.Solid(new Rect(card.x, card.y + 8f, 3f, 18f), _palette.Accent);
            _painter.Text(new Rect(card.x + 14f, card.y + 6f, card.width - 28f, 20f), "订单信息", PaintText.Section);

            var fields = new[]
            {
                ("运单编号", string.IsNullOrWhiteSpace(receipt.ShipmentId) ? "未记录" : receipt.ShipmentId),
                ("发货站点", StationLine(receipt)),
                ("收货地点", "地堡马蜂自提点"),
                ("寄送时间", LocalTime(receipt.CreatedUtc)),
                ("护送方案", string.IsNullOrWhiteSpace(receipt.EscortName) ? "无" : receipt.EscortName),
                ("单件丢件率", Percent(receipt.LossRate))
            };
            for (var index = 0; index < fields.Length; index++)
            {
                layout.InfoField(index, out var label, out var value);
                _painter.Text(label, fields[index].Item1, PaintText.Small);
                _painter.Text(value, PanelText.Fit(_painter, fields[index].Item2, value.width, PaintText.RowName),
                    PaintText.RowName);
            }
        }

        private void DrawStats(ReceiptLayout layout, Receipt receipt)
        {
            var lost = receipt.LostStacks > 0;
            DrawStatCard(layout.StatOne, "成功送达", receipt.DeliveredStacks + " 件", _palette.Good,
                _palette.GoodSoft, _palette.Good,
                "价值 " + receipt.DeliveredValue.ToString("N0", CultureInfo.InvariantCulture), null);
            DrawStatCard(layout.StatTwo, "运输丢失", receipt.LostStacks + " 件",
                lost ? _palette.Danger : _palette.TextDim,
                lost ? _palette.DangerSoft : _palette.Card, lost ? _palette.Danger : _palette.CardBorder,
                lost ? "价值 " + receipt.LostValue.ToString("N0", CultureInfo.InvariantCulture) : "本次没有丢件",
                null);
            DrawStatCard(layout.StatThree, "支付费用",
                receipt.TotalUpfront.ToString("N0", CultureInfo.InvariantCulture), _palette.Blue,
                _palette.BlueSoft, _palette.Blue,
                "运费 " + receipt.Fee.ToString("N0", CultureInfo.InvariantCulture) + " · 护送 "
                + receipt.EscortMoney.ToString("N0", CultureInfo.InvariantCulture), MaterialLine(receipt));
        }

        private void DrawStatCard(Rect rect, string label, string value, Color valueColor, Color fill,
            Color border, string sub, string? sub2)
        {
            _painter.Rounded(rect, 12f, fill, PanelPalette.Alpha(border, 0.55f), 1f);
            _painter.Text(new Rect(rect.x + 14f, rect.y + ReceiptLayout.StatLabelTop, rect.width - 28f,
                ReceiptLayout.StatLabelHeight), label, PaintText.Small, TextAnchor.MiddleLeft, _palette.TextDim);
            _painter.Text(new Rect(rect.x + 14f, rect.y + ReceiptLayout.StatValueTop, rect.width - 28f,
                    ReceiptLayout.StatValueHeight),
                PanelText.Fit(_painter, value, rect.width - 28f, PaintText.RowStrong), PaintText.RowStrong,
                TextAnchor.MiddleLeft, valueColor);
            _painter.Text(new Rect(rect.x + 14f, rect.y + ReceiptLayout.StatSubTop, rect.width - 28f,
                    ReceiptLayout.StatSubHeight),
                PanelText.Fit(_painter, sub, rect.width - 28f, PaintText.Small), PaintText.Small);
            if (string.IsNullOrEmpty(sub2)) return;
            _painter.Text(new Rect(rect.x + 14f, rect.y + ReceiptLayout.StatSub2Top, rect.width - 28f,
                    ReceiptLayout.StatSub2Height),
                PanelText.Fit(_painter, sub2!, rect.width - 28f, PaintText.Small), PaintText.Small,
                TextAnchor.MiddleLeft, _palette.TextFaint);
        }

        private void DrawNoLossHint(Rect card)
        {
            if (card.height <= 0f) return;
            _painter.Rounded(card, 12f, _palette.GoodSoft, PanelPalette.Alpha(_palette.Good, 0.55f), 1f);
            _painter.Solid(new Rect(card.x, card.y + 10f, 3f, 20f), _palette.Good);
            _painter.Text(new Rect(card.x + 16f, card.y + 8f, card.width - 32f, 22f), "丢失货物　0 件",
                PaintText.Section);
            _painter.Text(new Rect(card.x + 16f, card.y + 34f, card.width - 32f, 22f),
                "本次寄送全部送达，没有货物丢失。", PaintText.RowName);
        }

        /// <summary>
        /// One scrollable goods table. Every row carries the game's own item icon; a missing icon
        /// falls back to the shared letter tile and is logged once per type id by the caller's
        /// resolver, never replaced with invented art.
        /// </summary>
        private void DrawGoodsTable(Rect card, string title, List<ReceiptLine> lines, bool lost,
            ref float scroll)
        {
            if (card.height <= 0f) return;
            var accent = lost ? _palette.Danger : _palette.Good;
            _painter.Rounded(card, 12f, _palette.Card, _palette.CardBorder, 1f);
            _painter.Solid(new Rect(card.x, card.y + 10f, 3f, 20f), accent);
            _painter.Text(new Rect(card.x + 16f, card.y + ReceiptLayout.ListTitleTop, card.width - 32f,
                ReceiptLayout.ListTitleTextHeight), title + "　" + lines.Count + " 件", PaintText.Section,
                TextAnchor.MiddleLeft, accent);

            var header = new Rect(card.x, card.y + ReceiptLayout.ListHeaderTop, card.width,
                ReceiptLayout.ListHeaderHeight);
            ReceiptLayout.ListColumns(header, out _, out var nameHead, out var countHead, out var unitHead,
                out var totalHead, out var statusHead);
            _painter.Text(nameHead, "物品", PaintText.Tiny);
            _painter.Text(countHead, "数量", PaintText.Tiny, TextAnchor.MiddleRight);
            _painter.Text(unitHead, "单价", PaintText.Tiny, TextAnchor.MiddleRight);
            _painter.Text(totalHead, "总价值", PaintText.Tiny, TextAnchor.MiddleRight);
            _painter.Text(statusHead, "状态", PaintText.Tiny, TextAnchor.MiddleRight);

            var view = ReceiptLayout.ListView(card);
            var contentHeight = lines.Count * ReceiptLayout.ListRowHeight;
            var maxScroll = Mathf.Max(0f, contentHeight - view.height);
            scroll = Mathf.Clamp(scroll, 0f, maxScroll);

            if (lines.Count == 0)
            {
                _painter.Text(new Rect(view.x + 16f, view.y, view.width - 32f, 22f),
                    lost ? "没有货物丢失。" : "没有货物送达。", PaintText.Small);
                return;
            }
            if (view.height <= 0f) return;

            var first = Mathf.FloorToInt(scroll / ReceiptLayout.ListRowHeight);
            var shown = Mathf.Min(lines.Count - first,
                Mathf.CeilToInt(view.height / ReceiptLayout.ListRowHeight) + 1);
            _painter.BeginClip(view);
            for (var offset = 0; offset < shown; offset++)
            {
                var index = first + offset;
                var row = new Rect(view.x, view.y + index * ReceiptLayout.ListRowHeight - scroll, view.width,
                    ReceiptLayout.ListRowTextHeight);
                if (row.yMax < view.y - 0.5f || row.y > view.yMax + 0.5f) continue;
                DrawGoodsRow(row, lines[index], lost);
            }
            _painter.EndClip();

            if (maxScroll > 0f)
            {
                var bar = new Rect(view.xMax - 4f, view.y, 4f, view.height);
                _painter.Rounded(bar, 2f, PanelPalette.Alpha(_palette.TextFaint, 0.10f), null, 0f);
                var thumbHeight = Mathf.Max(18f, view.height * (view.height / contentHeight));
                var thumbY = view.y + (view.height - thumbHeight) * (scroll / maxScroll);
                _painter.Rounded(new Rect(bar.x, thumbY, bar.width, thumbHeight), 2f,
                    PanelPalette.Alpha(_palette.TextFaint, 0.45f), null, 0f);
            }
        }

        private void DrawGoodsRow(Rect row, ReceiptLine line, bool lost)
        {
            ReceiptLayout.ListColumns(row, out var icon, out var name, out var count, out var unit, out var total,
                out var status);
            _painter.Icon(icon, _iconFor(line.TypeId), line.Name, PaintText.MetricLabel, _palette.TextDim);
            _painter.Text(name, PanelText.Fit(_painter, line.Name, name.width, PaintText.RowName), PaintText.RowName);
            _painter.Text(count, "×" + line.StackCount, PaintText.RowValue, TextAnchor.MiddleRight);
            _painter.Text(unit, line.UnitValue.ToString("N0", CultureInfo.InvariantCulture), PaintText.RowValue,
                TextAnchor.MiddleRight);
            _painter.Text(total, line.GoodsValue.ToString("N0", CultureInfo.InvariantCulture), PaintText.RowStrong,
                TextAnchor.MiddleRight);
            _painter.Text(status, lost ? "丢失" : "已送达", PaintText.RowValue, TextAnchor.MiddleRight,
                lost ? _palette.Danger : _palette.Good);
        }

        // ------------------------------------------------------------------ text helpers

        internal static string OutcomeLabel(ReceiptOutcome outcome) => outcome switch
        {
            ReceiptOutcome.Delivered => "全部送达",
            ReceiptOutcome.Partial => "部分送达",
            _ => "全部丢失"
        };

        private Color OutcomeColor(ReceiptOutcome outcome) => outcome switch
        {
            ReceiptOutcome.Delivered => _palette.Good,
            ReceiptOutcome.Partial => _palette.Warning,
            _ => _palette.Danger
        };

        /// <summary>
        /// Escort material. Zero means the receipt predates the field, so say "未记录" rather than
        /// back-filling from the current escort table, which may since have changed.
        /// </summary>
        private string MaterialLine(Receipt receipt)
        {
            if (receipt.EscortItemAmount <= 0) return "材料消耗未记录";
            return "材料 " + receipt.EscortItemAmount.ToString("N0", CultureInfo.InvariantCulture) + " "
                   + ItemName(receipt.EscortItemId);
        }

        private string ItemName(int typeId)
        {
            if (typeId <= 0) return "物品";
            try
            {
                var name = _nameFor(typeId);
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            catch
            {
                _log?.Invoke("Receipt escort material name unavailable for typeId=" + typeId);
            }
            return "物品 " + typeId;
        }

        internal static string StationLine(Receipt receipt)
        {
            var station = string.IsNullOrWhiteSpace(receipt.StationName) ? "未知站点" : receipt.StationName;
            return station + "（" + MapName(receipt.MapId) + "）";
        }

        internal static string MapName(string mapId)
        {
            switch (mapId)
            {
                case "Level_HiddenWarehouse_Main": return "仓库区";
                case "Level_Farm_Main": return "农场镇";
            }
            var name = mapId.StartsWith("Level_", StringComparison.Ordinal) ? mapId.Substring(6) : mapId;
            if (name.EndsWith("_Main", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 5);
            name = name.Replace('_', ' ').Trim();
            return string.IsNullOrEmpty(name) ? "未知区域" : name;
        }

        internal static string LocalTime(string createdUtc)
        {
            if (string.IsNullOrWhiteSpace(createdUtc)) return "未记录";
            if (DateTimeOffset.TryParse(createdUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                    out var stamp))
            {
                return stamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }
            return createdUtc;
        }

        internal static string Percent(double value) =>
            (value * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }
}
