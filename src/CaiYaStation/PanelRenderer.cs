using System.Collections.Generic;
using CaiYaStation.Domain;
using UnityEngine;

namespace CaiYaStation
{
    /// <summary>
    /// Draws the shipment panel from a <see cref="ShipmentView"/>. Stateless with respect to input:
    /// hover is derived from the mouse position and the same rects that get painted, so what the
    /// player sees highlighted is exactly what a click will hit. The old code asked IMGUI for hover
    /// state inside a scroll view and remapped the mouse by hand, which is what made the tooltip
    /// blink and land on the wrong row.
    /// </summary>
    internal sealed class PanelRenderer
    {
        private readonly PanelPalette _c;
        private readonly IPanelPainter _p;
        private readonly Dictionary<int, float> _measuredTooltips = new Dictionary<int, float>();

        internal PanelRenderer(PanelPalette palette, IPanelPainter painter)
        {
            _c = palette;
            _p = painter;
        }

        internal void ResetMeasurements() => _measuredTooltips.Clear();

        internal PanelRenderResult Draw(ShipmentView v, PanelLayout l, Vector2 mouse, float screenWidth, float screenHeight)
        {
            var result = new PanelRenderResult();
            _p.Solid(new Rect(0f, 0f, screenWidth, screenHeight), _c.Backdrop);
            _p.Rounded(new Rect(l.Panel.x + 2f, l.Panel.y + 7f, l.Panel.width, l.Panel.height), 20f, _c.Shadow, null, 0f);
            _p.Rounded(l.Panel, 18f, _c.Panel, _c.PanelBorder, 1f);

            DrawHeader(v, l, mouse, result);
            DrawList(v, l, mouse, result);
            DrawRail(v, l, mouse, result);
            DrawFooter(v, l, mouse, result);

            if (result.TooltipRow != null)
                DrawTooltip(result.TooltipRow, result.HoveredRowRect, l, result);
            return result;
        }

        // ------------------------------------------------------------------ header

        private void DrawHeader(ShipmentView v, PanelLayout l, Vector2 mouse, PanelRenderResult r)
        {
            var head = new Rect(l.Panel.x + 1f, l.Panel.y + 1f, l.Panel.width - 2f, PanelLayout.HeaderHeight - 2f);
            _p.Rounded(head, 17f, _c.Header, null, 0f);
            // Square off the bottom of the header until it meets its own rule.
            _p.Solid(new Rect(head.x, head.y + 40f, head.width, head.height - 40f), _c.Header);
            _p.Solid(new Rect(l.Panel.x + 20f, l.Panel.y + PanelLayout.HeaderHeight - 1f, l.Panel.width - 40f, 1f), _c.HeaderLine);
            _p.Solid(new Rect(l.Panel.x + 1f, l.Panel.y + 20f, 3f, 36f), _c.Accent);

            _p.Rounded(l.HeaderIcon, 10f, PanelPalette.Alpha(_c.Accent, 0.10f), PanelPalette.Alpha(_c.Accent, 0.30f), 1f);
            _p.Icon(new Rect(l.HeaderIcon.x + 4f, l.HeaderIcon.y + 4f, 32f, 32f), v.StationIcon, "菜",
                PaintText.MetricValue, _c.Accent);

            _p.Text(l.HeaderTitle, v.Title, PaintText.Title);

            var badgeColor = v.StationOpen ? _c.Accent : _c.Danger;
            _p.Rounded(l.HeaderBadge, 11f, PanelPalette.Alpha(_c.CardRaised, 0.95f),
                PanelPalette.Alpha(badgeColor, 0.35f), 1f);
            _p.Text(l.HeaderBadge, v.StatusLabel, PaintText.Badge, TextAnchor.MiddleCenter, badgeColor);

            _p.Text(l.HeaderSubtitle, v.Subtitle, PaintText.Subtitle);

            var closeHover = l.HeaderClose.Contains(mouse);
            _p.Rounded(l.HeaderClose, 9f, closeHover ? _c.ButtonHover : _c.Card,
                closeHover ? _c.ButtonHoverBorder : _c.CardBorder, 1f);
            _p.Text(l.HeaderClose, "×", PaintText.Label, TextAnchor.MiddleCenter,
                closeHover ? _c.Text : _c.TextDim);
            AddHit(r, PanelHitKind.Close, "close", l.HeaderClose, 0);
        }

        // ------------------------------------------------------------------ backpack list

        private void DrawList(ShipmentView v, PanelLayout l, Vector2 mouse, PanelRenderResult r)
        {
            _p.Text(l.ListTitle, "背包物品", PaintText.Section);
            _p.Text(l.ListCapacity, v.CapacityText, PaintText.Small, TextAnchor.MiddleRight);

            _p.Rounded(l.ListCard, 14f, _c.Card, _c.CardBorder, 1f);
            DrawColumnHeader(l);

            _p.BeginClip(l.ListView);
            try
            {
                if (v.Rows.Count == 0)
                {
                    var empty = new Rect(l.ListView.x, l.ListView.y + l.ListView.height * 0.5f - 28f, l.ListView.width, 22f);
                    _p.Text(empty, "背包里没有可寄的东西", PaintText.Label, TextAnchor.MiddleCenter, _c.TextDim);
                    _p.Text(new Rect(empty.x, empty.y + 24f, empty.width, 18f), "先去搜刮，再回来寄",
                        PaintText.Tiny, TextAnchor.MiddleCenter, _c.TextFaint);
                }
                else
                {
                    for (var i = 0; i < v.Rows.Count; i++)
                    {
                        var row = l.ListRowRect(i, v.ListScroll);
                        if (row.yMax < l.ListView.y - 4f) continue;
                        if (row.y > l.ListView.yMax + 4f) continue;
                        DrawRow(v.Rows[i], row, mouse, l.ListView, r);
                    }
                    DrawScrollbar(l.ListView, v.ListScroll, l.ListContentHeight(v.Rows.Count));
                }
            }
            finally
            {
                _p.EndClip();
            }

            _p.Text(l.ListHint, "单击勾选 / 取消　·　悬停查看详情　·　灰色不可寄", PaintText.Tiny);
        }

        private void DrawColumnHeader(PanelLayout l)
        {
            var cells = Cells(l.ListColumns);
            _p.Text(cells.Name, "名称", PaintText.Tiny);
            _p.Text(cells.Count, "数量", PaintText.Tiny, TextAnchor.MiddleRight);
            _p.Text(cells.Unit, "单件", PaintText.Tiny, TextAnchor.MiddleRight);
            _p.Text(cells.Total, "总价", PaintText.Tiny, TextAnchor.MiddleRight);
            _p.Text(cells.Weight, "重量", PaintText.Tiny, TextAnchor.MiddleRight);
            _p.Solid(new Rect(l.ListColumns.x, l.ListColumns.yMax + 2f, l.ListColumns.width, 1f), _c.CardBorder);
        }

        private void DrawRow(ShipmentRowView row, Rect rect, Vector2 mouse, Rect view, PanelRenderResult r)
        {
            var hovered = view.Contains(mouse) && rect.Contains(mouse);
            if (row.Selected)
            {
                _p.Rounded(rect, 8f, _c.RowSelected, null, 0f);
                _p.Solid(new Rect(rect.x, rect.y + 8f, 2f, rect.height - 16f), _c.Accent);
            }
            else if (hovered)
            {
                _p.Rounded(rect, 8f, _c.RowHover, null, 0f);
            }
            AddHit(r, PanelHitKind.Row, "row:" + row.InstanceId, Clip(rect, view), row.InstanceId);

            _p.Solid(new Rect(rect.x + 4f, rect.y + 9f, 3f, rect.height - 18f), PanelPalette.Quality(row.Quality));
            DrawCheck(new Rect(rect.x + 13f, rect.y + (rect.height - 18f) * 0.5f, 18f, 18f), row.Selected);

            var icon = new Rect(rect.x + 39f, rect.y + 4f, 28f, 28f);
            _p.Icon(icon, row.Icon, row.Name, PaintText.MetricLabel, _c.TextDim);

            var cells = Cells(rect);
            var nameColor = row.Eligible ? _c.Text : _c.TextFaint;
            if (row.Eligible)
            {
                _p.Text(new Rect(cells.Name.x, rect.y + 8f, cells.Name.width, 20f), row.Name, PaintText.RowName,
                    TextAnchor.MiddleLeft, nameColor);
            }
            else
            {
                _p.Text(new Rect(cells.Name.x, rect.y + 5f, cells.Name.width, 18f), row.Name, PaintText.RowName,
                    TextAnchor.MiddleLeft, nameColor);
                _p.Text(new Rect(cells.Name.x, rect.y + 22f, cells.Name.width, 13f), "不可寄 · " + row.BlockedReason,
                    PaintText.Tiny, TextAnchor.MiddleLeft, _c.Danger);
            }

            _p.Text(new Rect(cells.Count.x, rect.y + 8f, cells.Count.width, 20f), row.CountText, PaintText.RowValue,
                TextAnchor.MiddleRight, row.Eligible ? _c.TextDim : _c.TextFaint);
            _p.Text(new Rect(cells.Unit.x, rect.y + 8f, cells.Unit.width, 20f), row.UnitValue.ToString("N0"),
                PaintText.RowValue, TextAnchor.MiddleRight, row.Eligible ? _c.TextDim : _c.TextFaint);
            _p.Text(new Rect(cells.Total.x, rect.y + 8f, cells.Total.width, 20f), row.GoodsValue.ToString("N0"),
                PaintText.RowStrong, TextAnchor.MiddleRight, row.Eligible ? _c.Text : _c.TextFaint);
            _p.Text(new Rect(cells.Weight.x, rect.y + 8f, cells.Weight.width, 20f), row.WeightKg.ToString("0.##") + "kg",
                PaintText.RowValue, TextAnchor.MiddleRight, row.Eligible ? _c.TextFaint : _c.TextFaint);

            if (hovered)
            {
                r.TooltipRow = row;
                r.HoveredRowRect = rect;
            }
        }

        private void DrawCheck(Rect rect, bool selected)
        {
            if (selected)
            {
                _p.Rounded(rect, 5f, _c.Accent, null, 0f);
                _p.Text(rect, "√", PaintText.Badge, TextAnchor.MiddleCenter, _c.OnPrimary);
                return;
            }
            _p.Rounded(rect, 5f, PanelPalette.Alpha(_c.TrackBackground, 0.9f), _c.CardBorder, 1f);
        }

        private void DrawScrollbar(Rect view, float scroll, float contentHeight)
        {
            if (contentHeight <= view.height + 0.5f) return;
            var track = new Rect(view.xMax - 9f, view.y + 2f, 4f, view.height - 4f);
            var thumbHeight = Mathf.Max(24f, track.height * view.height / contentHeight);
            var progress = Mathf.Clamp01(scroll / (contentHeight - view.height));
            var thumbY = track.y + (track.height - thumbHeight) * progress;
            _p.Rounded(track, 2f, PanelPalette.Alpha(_c.TextFaint, 0.10f), null, 0f);
            _p.Rounded(new Rect(track.x, thumbY, track.width, thumbHeight), 2f,
                PanelPalette.Alpha(_c.TextFaint, 0.45f), null, 0f);
        }

        // ------------------------------------------------------------------ right rail

        private void DrawRail(ShipmentView v, PanelLayout l, Vector2 mouse, PanelRenderResult r)
        {
            _p.Rounded(l.RailCard, 14f, _c.Card, _c.CardBorder, 1f);
            // Hard guarantee: even if a config shrinks the panel below the rail's natural height,
            // the last escort card is cut off at the card edge instead of painting over the footer.
            _p.BeginClip(new Rect(l.RailCard.x + 1f, l.RailCard.y + 1f, l.RailCard.width - 2f, l.RailCard.height - 2f));
            try
            {
                DrawManifest(v, l, mouse, r);
                DrawCost(v, l);
                DrawEscorts(v, l, mouse, r);
            }
            finally
            {
                _p.EndClip();
            }
        }

        private void DrawManifest(ShipmentView v, PanelLayout l, Vector2 mouse, PanelRenderResult r)
        {
            _p.Text(l.ManifestTitle, "待运送货物", PaintText.Section);
            _p.Text(l.ManifestCount, v.Manifest.Count + " 件", PaintText.Small, TextAnchor.MiddleRight);

            _p.BeginClip(l.ManifestView);
            try
            {
                if (v.Manifest.Count == 0)
                {
                    var box = new Rect(l.ManifestView.x + 8f, l.ManifestView.y + l.ManifestView.height * 0.5f - 22f,
                        l.ManifestView.width - 16f, 44f);
                    _p.Rounded(box, 8f, PanelPalette.Alpha(_c.TrackBackground, 0.55f), _c.CardBorder, 1f);
                    _p.Text(new Rect(box.x, box.y + 5f, box.width, 18f), "还没有选中物品",
                        PaintText.Label, TextAnchor.MiddleCenter, _c.TextDim);
                    _p.Text(new Rect(box.x, box.y + 23f, box.width, 16f), "在左侧列表里单击要寄的东西",
                        PaintText.Tiny, TextAnchor.MiddleCenter, _c.TextFaint);
                }
                else
                {
                    for (var i = 0; i < v.Manifest.Count; i++)
                    {
                        var row = l.ManifestRowRect(i, v.ManifestScroll);
                        if (row.yMax < l.ManifestView.y - 2f) continue;
                        if (row.y > l.ManifestView.yMax + 2f) continue;
                        DrawManifestRow(v.Manifest[i], row, mouse, l.ManifestView, r);
                    }
                    DrawScrollbar(l.ManifestView, v.ManifestScroll, l.ManifestContentHeight(v.Manifest.Count));
                }
            }
            finally
            {
                _p.EndClip();
            }

            _p.Text(l.ManifestHint, v.Manifest.Count > 0 ? "单击清单行移出" : "",
                PaintText.Tiny);
        }

        private void DrawManifestRow(ShipmentRowView row, Rect rect, Vector2 mouse, Rect view, PanelRenderResult r)
        {
            var hovered = view.Contains(mouse) && rect.Contains(mouse);
            if (hovered) _p.Rounded(rect, 7f, _c.RowHover, null, 0f);
            AddHit(r, PanelHitKind.ManifestRow, "manifest:" + row.InstanceId, Clip(rect, view), row.InstanceId);

            _p.Solid(new Rect(rect.x + 2f, rect.y + 5f, 2f, rect.height - 10f), PanelPalette.Quality(row.Quality));
            _p.Icon(new Rect(rect.x + 9f, rect.y + 4f, 20f, 20f), row.Icon, row.Name, PaintText.Tiny, _c.TextDim);
            _p.Text(new Rect(rect.x + 34f, rect.y + 5f, rect.width - 34f - 84f, 18f), row.Name,
                PaintText.RowValue, TextAnchor.MiddleLeft, _c.Text);
            _p.Text(new Rect(rect.xMax - 80f, rect.y + 5f, 30f, 18f), row.CountText,
                PaintText.Tiny, TextAnchor.MiddleRight);
            _p.Text(new Rect(rect.xMax - 46f, rect.y + 5f, 40f, 18f), row.GoodsValue.ToString("N0"),
                PaintText.RowValue, TextAnchor.MiddleRight, _c.Text);
        }

        private void DrawCost(ShipmentView v, PanelLayout l)
        {
            _p.Text(l.CostTitle, "费用与风险", PaintText.Section);

            MetricPair(l.MetricRowOne, "货值", v.GoodsValue.ToString("N0"), v.FeeRateText, v.Quote.Fee.ToString("N0"),
                _c.Text);
            MetricPair(l.MetricRowTwo, "基础损耗", Percent(v.Quote.BaseLossRate), "护送费",
                v.Quote.EscortMoney.ToString("N0"), _c.Text);
            _p.Solid(l.CostRule, _c.CardBorder);

            _p.Text(l.TotalLabel, "预计总成本", PaintText.Label, TextAnchor.MiddleLeft, _c.TextDim);
            _p.Text(l.TotalValue, v.Quote.ExpectedTotalCost.ToString("N0"), PaintText.BigValue, TextAnchor.MiddleRight);

            _p.Rounded(l.RiskTrack, 4f, _c.TrackBackground, _c.TrackBorder, 1f);
            var inner = new Rect(l.RiskTrack.x + 2f, l.RiskTrack.y + 2f, l.RiskTrack.width - 4f, l.RiskTrack.height - 4f);
            var loss = Mathf.Clamp01((float)v.Quote.FinalLossRate);
            if (loss < 1f && inner.width > 0f)
                _p.Rounded(new Rect(inner.x, inner.y, inner.width * (1f - loss), inner.height), 2f,
                    PanelPalette.Alpha(_c.Accent, 0.75f), null, 0f);
            if (loss > 0.001f && inner.width > 0f)
                _p.Rounded(new Rect(inner.x + inner.width * (1f - loss), inner.y, inner.width * loss, inner.height),
                    2f, _c.Danger, null, 0f);
            _p.Text(l.RiskLegend, "丢件率 " + Percent(loss), PaintText.Tiny, TextAnchor.MiddleLeft,
                loss > 0.001f ? _c.Danger : _c.TextFaint);
            _p.Text(l.RiskCap, v.LossCapText, PaintText.Tiny, TextAnchor.MiddleRight);
        }

        private void MetricPair(Rect row, string keyA, string valueA, string keyB, string valueB, Color valueColor)
        {
            var half = row.width * 0.5f;
            MetricCell(new Rect(row.x, row.y, half - 10f, row.height), keyA, valueA, valueColor);
            MetricCell(new Rect(row.x + half + 10f, row.y, half - 10f, row.height), keyB, valueB, valueColor);
        }

        private void MetricCell(Rect cell, string key, string value, Color valueColor)
        {
            var half = cell.width * 0.5f;
            _p.Text(new Rect(cell.x, cell.y, half - 4f, cell.height), key, PaintText.MetricLabel);
            _p.Text(new Rect(cell.x + half, cell.y, half, cell.height), value,
                PaintText.MetricValue, TextAnchor.MiddleRight, valueColor);
        }

        private void DrawEscorts(ShipmentView v, PanelLayout l, Vector2 mouse, PanelRenderResult r)
        {
            _p.Text(l.EscortTitle, "护送队", PaintText.Section);
            _p.Text(l.EscortHint, "每单最多一队", PaintText.Tiny, TextAnchor.MiddleRight);

            for (var i = 0; i < 3 && i < v.Escorts.Count; i++)
            {
                var card = v.Escorts[i];
                var rect = l.EscortCardRect(i);
                var hovered = rect.Contains(mouse);
                var border = card.Selected ? _c.Accent : hovered ? _c.ButtonHoverBorder : _c.CardBorder;
                _p.Rounded(rect, 10f, card.Selected ? _c.CardRaised : _c.Card, border, card.Selected ? 1.5f : 1f);
                if (card.Selected) _p.Solid(new Rect(rect.x + 1f, rect.y + 10f, 3f, rect.height - 20f), _c.Accent);

                var titleColor = card.Affordable ? _c.Text : _c.TextFaint;
                _p.Text(new Rect(rect.x + 13f, rect.y + 5f, rect.width - 26f - 112f, 18f), card.Title,
                    PaintText.Label, TextAnchor.MiddleLeft, titleColor);
                _p.Text(new Rect(rect.x + 13f, rect.y + 25f, rect.width - 26f - 96f, 15f), card.Cost,
                    PaintText.Tiny, TextAnchor.MiddleLeft, card.Affordable ? _c.TextDim : _c.TextFaint);
                _p.Text(new Rect(rect.xMax - 108f, rect.y + 24f, 96f, 16f), card.Effect,
                    PaintText.Tiny, TextAnchor.MiddleRight, card.Selected ? _c.Accent : _c.TextDim);

                if (!card.Affordable && card.Missing != null)
                    _p.Text(new Rect(rect.xMax - 108f, rect.y + 5f, 96f, 14f), card.Missing,
                        PaintText.Tiny, TextAnchor.MiddleRight, _c.Danger);
                else if (card.Selected)
                    _p.Text(new Rect(rect.xMax - 30f, rect.y + 5f, 18f, 14f), "√",
                        PaintText.Badge, TextAnchor.MiddleRight, _c.Accent);

                AddHit(r, PanelHitKind.Escort, "escort:" + (int)card.Option, rect, 0, card.Option);
            }
        }

        // ------------------------------------------------------------------ footer

        private void DrawFooter(ShipmentView v, PanelLayout l, Vector2 mouse, PanelRenderResult r)
        {
            _p.Solid(l.FooterRule, _c.CardBorder);

            _p.Text(l.BalanceLabel, "金钱", PaintText.MetricLabel);
            _p.Text(l.BalanceValue, v.Money.ToString("N0"), PaintText.Label);
            _p.Text(l.CashLabel, "现金", PaintText.MetricLabel);
            _p.Text(l.CashValue, v.Cash.ToString("N0"), PaintText.Label);

            _p.Text(l.FooterSummary, v.Summary, PaintText.Small);
            var statusColor = v.StatusKind == FooterStatusKind.Danger ? _c.Danger
                : v.StatusKind == FooterStatusKind.Warning ? _c.Warning : _c.TextFaint;
            _p.Text(l.FooterStatus, v.Status, PaintText.Tiny, TextAnchor.MiddleLeft, statusColor);

            DrawButton(l.SelectAllButton, "全选可寄", false, true, l.SelectAllButton.Contains(mouse),
                PanelHitKind.SelectAll, "selectall", r);
            DrawButton(l.ClearButton, "清空", false, true, l.ClearButton.Contains(mouse),
                PanelHitKind.Clear, "clear", r);
            var confirmHover = l.ConfirmButton.Contains(mouse) && v.CanShip;
            DrawButton(l.ConfirmButton, v.Shipping ? "发货中…" : "确认发货", true, v.CanShip, confirmHover,
                PanelHitKind.Confirm, "confirm", r);
            DrawButton(l.CloseButton, "关闭", false, true, l.CloseButton.Contains(mouse),
                PanelHitKind.Close, "footer-close", r);
        }

        private void DrawButton(Rect rect, string label, bool primary, bool enabled, bool hovered,
            PanelHitKind kind, string id, PanelRenderResult r)
        {
            Color fill, border, text;
            if (primary && enabled)
            {
                fill = hovered ? _c.PrimaryHover : _c.Primary;
                border = fill;
                text = _c.OnPrimary;
            }
            else if (!enabled)
            {
                fill = _c.ButtonDisabled;
                border = _c.ButtonDisabledBorder;
                text = _c.TextFaint;
            }
            else
            {
                fill = hovered ? _c.ButtonHover : _c.Button;
                border = hovered ? _c.ButtonHoverBorder : _c.ButtonBorder;
                text = _c.Text;
            }

            _p.Rounded(rect, 9f, fill, border, 1f);
            _p.Text(rect, label, primary && enabled ? PaintText.ButtonPrimary : PaintText.Button,
                TextAnchor.MiddleCenter, text);
            if (enabled) AddHit(r, kind, id, rect, 0);
        }

        // ------------------------------------------------------------------ tooltip

        private void DrawTooltip(ShipmentRowView row, Rect rowRect, PanelLayout l, PanelRenderResult result)
        {
            var width = 300f;
            var height = TooltipHeight(row, width);
            // The card sits above or below the row it describes, inside the left column. Parking it
            // beside the row means parking it on the rail, where the escort cards and the delivery
            // manifest live, and those stay clickable under a card the player cannot see through.
            var x = Mathf.Clamp(rowRect.x + 74f, l.Left.x + 10f,
                Mathf.Max(l.Left.x + 10f, l.Left.xMax - width - 10f));
            var below = rowRect.yMax + 8f;
            var above = rowRect.y - height - 8f;
            float y;
            if (below + height <= l.Left.yMax - 8f) y = below;
            else if (above >= l.Left.y + 8f) y = above;
            else y = Mathf.Clamp(rowRect.y, l.Panel.y + 12f, l.Panel.yMax - height - 12f);
            var rect = new Rect(x, y, width, height);
            result.TooltipRect = rect;

            _p.Rounded(new Rect(rect.x + 2f, rect.y + 3f, rect.width, rect.height), 12f, _c.Shadow, null, 0f);
            _p.Rounded(rect, 12f, _c.CardRaised, _c.CardBorder, 1f);
            _p.Solid(new Rect(rect.x + 14f, rect.y, rect.width - 28f, 2f), _c.Accent);

            var cursor = rect.y + 12f;
            _p.Text(new Rect(rect.x + 14f, cursor, rect.width - 28f, 20f),
                row.Name + (row.StackCount > 1 ? "  ×" + row.StackCount : ""), PaintText.TooltipTitle);
            cursor += 22f;

            if (!string.IsNullOrEmpty(row.Description))
            {
                var text = row.Description.Length > 220 ? row.Description.Substring(0, 220) + "…" : row.Description;
                var descHeight = MeasureTooltip(row, text, rect.width - 28f);
                _p.Text(new Rect(rect.x + 14f, cursor, rect.width - 28f, descHeight), text,
                    PaintText.TooltipBody, TextAnchor.UpperLeft, null, true);
                cursor += descHeight + 6f;
                _p.Solid(new Rect(rect.x + 14f, cursor, rect.width - 28f, 1f), _c.CardBorder);
                cursor += 7f;
            }

            _p.Text(new Rect(rect.x + 14f, cursor, rect.width - 28f, 16f),
                "价值 " + row.UnitValue.ToString("N0") + " × " + row.StackCount + " = " + row.GoodsValue.ToString("N0"),
                PaintText.TooltipLine);
            cursor += 18f;
            _p.Text(new Rect(rect.x + 14f, cursor, rect.width - 28f, 16f),
                "重量 " + row.WeightKg.ToString("0.##") + " kg　·　品质 " + row.Quality, PaintText.TooltipLine);
            cursor += 18f;
            if (row.Eligible)
                _p.Text(new Rect(rect.x + 14f, cursor, rect.width - 28f, 16f), "可寄送　·　单击加入运送清单",
                    PaintText.TooltipLine, TextAnchor.MiddleLeft, _c.Good);
            else
                _p.Text(new Rect(rect.x + 14f, cursor, rect.width - 28f, 16f), "不可寄：" + row.BlockedReason,
                    PaintText.TooltipLine, TextAnchor.MiddleLeft, _c.Danger);
        }

        private float TooltipHeight(ShipmentRowView row, float width)
        {
            var height = 12f + 22f + 18f + 18f + 16f + 12f;
            if (!string.IsNullOrEmpty(row.Description))
            {
                var text = row.Description.Length > 220 ? row.Description.Substring(0, 220) + "…" : row.Description;
                height += MeasureTooltip(row, text, width - 28f) + 6f + 1f + 7f;
            }
            return height;
        }

        private float MeasureTooltip(ShipmentRowView row, string text, float width)
        {
            if (_measuredTooltips.TryGetValue(row.InstanceId, out var height)) return height;
            height = Mathf.Clamp(_p.Measure(text, width, PaintText.TooltipBody), 15f, 54f);
            _measuredTooltips[row.InstanceId] = height;
            return height;
        }

        // ------------------------------------------------------------------ helpers

        private void AddHit(PanelRenderResult r, PanelHitKind kind, string id, Rect rect, int instanceId,
            EscortOption escort = EscortOption.None)
        {
            r.Hits.Add(new PanelHit
            {
                Kind = kind,
                Id = id,
                InstanceId = instanceId,
                Escort = escort,
                Rect = rect
            });
        }

        private static CellRects Cells(Rect row)
        {
            var right = row.xMax;
            var weight = new Rect(right - 4f - 62f, row.y, 62f, 20f);
            var total = new Rect(weight.x - 6f - 70f, row.y, 70f, 20f);
            var unit = new Rect(total.x - 6f - 64f, row.y, 64f, 20f);
            var count = new Rect(unit.x - 6f - 40f, row.y, 40f, 20f);
            var name = new Rect(row.x + 74f, row.y, Mathf.Max(60f, count.x - 8f - (row.x + 74f)), 18f);
            return new CellRects(name, count, unit, total, weight);
        }

        /// <summary>
        /// Hit rects have to be clipped to the viewport they were drawn in. A row scrolled just out
        /// of sight still reports a rect, and without this the invisible sliver would answer clicks
        /// that land on the column header above it.
        /// </summary>
        private static Rect Clip(Rect inner, Rect outer)
        {
            var x = Mathf.Max(inner.x, outer.x);
            var y = Mathf.Max(inner.y, outer.y);
            var xMax = Mathf.Min(inner.xMax, outer.xMax);
            var yMax = Mathf.Min(inner.yMax, outer.yMax);
            return new Rect(x, y, Mathf.Max(0f, xMax - x), Mathf.Max(0f, yMax - y));
        }

        private static string Percent(double value) => (value * 100.0).ToString("0.#") + "%";

        private readonly struct CellRects
        {
            internal readonly Rect Name;
            internal readonly Rect Count;
            internal readonly Rect Unit;
            internal readonly Rect Total;
            internal readonly Rect Weight;

            internal CellRects(Rect name, Rect count, Rect unit, Rect total, Rect weight)
            {
                Name = name;
                Count = count;
                Unit = unit;
                Total = total;
                Weight = weight;
            }
        }
    }
}
