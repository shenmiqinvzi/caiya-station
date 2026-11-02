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
        // Keyed by (instance id, available room in quarter-pixels): the same item can be described
        // by a taller or shorter band when the panel is resized, and a stale measurement would put
        // the ellipsis in the wrong place.
        private readonly Dictionary<(int InstanceId, int Room), TooltipBody> _measuredTooltips =
            new Dictionary<(int InstanceId, int Room), TooltipBody>();

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

            _p.Text(l.HeaderTitle, PanelText.Fit(_p, v.Title, l.HeaderTitle.width, PaintText.Title), PaintText.Title);

            var badgeColor = v.StationOpen ? _c.Accent : _c.Danger;
            _p.Rounded(l.HeaderBadge, 11f, PanelPalette.Alpha(_c.CardRaised, 0.95f),
                PanelPalette.Alpha(badgeColor, 0.35f), 1f);
            _p.Text(l.HeaderBadge,
                PanelText.Fit(_p, v.StatusLabel, l.HeaderBadge.width - 8f, PaintText.Badge),
                PaintText.Badge, TextAnchor.MiddleCenter, badgeColor);

            _p.Text(l.HeaderSubtitle,
                PanelText.Fit(_p, v.Subtitle, l.HeaderSubtitle.width, PaintText.Subtitle), PaintText.Subtitle);

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
            _p.Text(l.ListTitle, "背包与装备", PaintText.Section);
            _p.Text(l.ListCapacity,
                PanelText.Fit(_p, v.CapacityText, l.ListCapacity.width, PaintText.Small),
                PaintText.Small, TextAnchor.MiddleRight);

            _p.Rounded(l.ListCard, 14f, _c.Card, _c.CardBorder, 1f);
            DrawColumnHeader(l);

            _p.BeginClip(l.ListView);
            try
            {
                if (v.Rows.Count == 0)
                {
                    var empty = new Rect(l.ListView.x, l.ListView.y + l.ListView.height * 0.5f - 28f, l.ListView.width, 22f);
                    _p.Text(empty, "背包和装备里都没有可寄的东西", PaintText.Label, TextAnchor.MiddleCenter, _c.TextDim);
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
            _p.Text(cells.Name, PanelText.Fit(_p, "名称", cells.Name.width, PaintText.Tiny), PaintText.Tiny);
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
            var nameText = PanelText.Fit(_p, row.Name, cells.Name.width, PaintText.RowName);
            // Worn gear gets the small print too: it is shippable, but the player has to know the
            // item comes off the equipment slot.
            var note = !row.Eligible ? "不可寄 · " + row.BlockedReason
                : row.Equipped ? "装备中 · 发货时自动卸下" : null;
            if (note == null)
            {
                _p.Text(new Rect(cells.Name.x, rect.y + 8f, cells.Name.width, 22f), nameText, PaintText.RowName,
                    TextAnchor.MiddleLeft, nameColor);
            }
            else
            {
                _p.Text(new Rect(cells.Name.x, rect.y + 4f, cells.Name.width, 18f), nameText, PaintText.RowName,
                    TextAnchor.MiddleLeft, nameColor);
                _p.Text(new Rect(cells.Name.x, rect.y + 22f, cells.Name.width, 16f),
                    PanelText.Fit(_p, note, cells.Name.width, PaintText.Tiny),
                    PaintText.Tiny, TextAnchor.MiddleLeft, row.Eligible ? _c.TextDim : _c.Danger);
            }

            // Keep the numbers on the item's own line when the row carries a second line of small
            // print, so a two-line row still reads as one item instead of a floating block.
            var valueY = note == null ? rect.y + 8f : rect.y + 4f;
            var valueHeight = note == null ? 22f : 18f;

            _p.Text(new Rect(cells.Count.x, valueY, cells.Count.width, valueHeight),
                PanelText.Fit(_p, row.CountText, cells.Count.width, PaintText.RowValue),
                PaintText.RowValue, TextAnchor.MiddleRight, row.Eligible ? _c.TextDim : _c.TextFaint);
            _p.Text(new Rect(cells.Unit.x, valueY, cells.Unit.width, valueHeight), row.UnitValue.ToString("N0"),
                PaintText.RowValue, TextAnchor.MiddleRight, row.Eligible ? _c.TextDim : _c.TextFaint);
            _p.Text(new Rect(cells.Total.x, valueY, cells.Total.width, valueHeight), row.GoodsValue.ToString("N0"),
                PaintText.RowStrong, TextAnchor.MiddleRight, row.Eligible ? _c.Text : _c.TextFaint);
            _p.Text(new Rect(cells.Weight.x, valueY, cells.Weight.width, valueHeight), row.WeightKg.ToString("0.##") + "kg",
                PaintText.RowValue, TextAnchor.MiddleRight, _c.TextFaint);

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
            var nameWidth = Mathf.Max(40f, rect.width - 34f - 84f);
            _p.Text(new Rect(rect.x + 34f, rect.y + 5f, nameWidth, 20f),
                PanelText.Fit(_p, row.Name, nameWidth, PaintText.RowValue),
                PaintText.RowValue, TextAnchor.MiddleLeft, _c.Text);
            _p.Text(new Rect(rect.xMax - 80f, rect.y + 5f, 30f, 20f), row.CountText,
                PaintText.Tiny, TextAnchor.MiddleRight);
            _p.Text(new Rect(rect.xMax - 46f, rect.y + 5f, 40f, 20f), row.GoodsValue.ToString("N0"),
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
            _p.Text(l.RiskCap, PanelText.Fit(_p, v.LossCapText, l.RiskCap.width, PaintText.Tiny),
                PaintText.Tiny, TextAnchor.MiddleRight);
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
            _p.Text(new Rect(cell.x, cell.y, half - 4f, cell.height),
                PanelText.Fit(_p, key, half - 4f, PaintText.MetricLabel), PaintText.MetricLabel);
            _p.Text(new Rect(cell.x + half, cell.y, half, cell.height),
                PanelText.Fit(_p, value, half, PaintText.MetricValue),
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
                // The badge sits left of both text lines; the lines start after it so nothing collides.
                DrawEscortBadge(new Rect(rect.x + 13f, rect.y + 8f, 28f, 28f), card.Option, card.Selected,
                    card.Affordable);
                var titleWidth = rect.width - 47f - 13f - 112f;
                var costWidth = rect.width - 47f - 13f - 96f;
                _p.Text(new Rect(rect.x + 47f, rect.y + 4f, titleWidth, 20f),
                    PanelText.Fit(_p, card.Title, titleWidth, PaintText.Label),
                    PaintText.Label, TextAnchor.MiddleLeft, titleColor);
                _p.Text(new Rect(rect.x + 47f, rect.y + 25f, costWidth, 17f),
                    PanelText.Fit(_p, card.Cost, costWidth, PaintText.Tiny),
                    PaintText.Tiny, TextAnchor.MiddleLeft, card.Affordable ? _c.TextDim : _c.TextFaint);
                _p.Text(new Rect(rect.xMax - 108f, rect.y + 24f, 96f, 18f),
                    PanelText.Fit(_p, card.Effect, 96f, PaintText.Tiny),
                    PaintText.Tiny, TextAnchor.MiddleRight, card.Selected ? _c.Accent : _c.TextDim);

                if (!card.Affordable && card.Missing != null)
                    _p.Text(new Rect(rect.xMax - 108f, rect.y + 4f, 96f, 16f),
                        PanelText.Fit(_p, card.Missing, 96f, PaintText.Tiny),
                        PaintText.Tiny, TextAnchor.MiddleRight, _c.Danger);
                else if (card.Selected)
                    _p.Text(new Rect(rect.xMax - 30f, rect.y + 4f, 18f, 16f), "√",
                        PaintText.Badge, TextAnchor.MiddleRight, _c.Accent);

                AddHit(r, PanelHitKind.Escort, "escort:" + (int)card.Option, rect, 0, card.Option);
            }
        }

        /// <summary>
        /// Escort badge. The game ships no scavenger/mercenary portrait — there is no sprite key
        /// for them, no character prefab, and CharacterIconTypes is only a classification enum —
        /// so each card gets a hand-drawn badge built from the same rounded-rect primitives as the
        /// rest of the panel: no asset, no load step, no missing-texture fallback to go wrong.
        /// </summary>
        private void DrawEscortBadge(Rect rect, EscortOption option, bool selected, bool affordable)
        {
            var tint = !affordable
                ? _c.TextFaint
                : option == EscortOption.Scavenger ? _c.Warning
                : option == EscortOption.Mercenary ? _c.Blue
                : _c.TextDim;
            _p.Rounded(rect, 8f, PanelPalette.Alpha(tint, selected ? 0.20f : 0.12f),
                PanelPalette.Alpha(tint, selected ? 0.55f : 0.28f), 1f);

            var body = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
            switch (option)
            {
                case EscortOption.Scavenger:
                    // Hooded scavenger with the pack on his back: hood, shoulders, then the pack.
                    _p.Rounded(new Rect(body.x + 5f, body.y + 1f, 10f, 10f), 5f, tint, null, 0f);
                    _p.Rounded(new Rect(body.x + 3f, body.y + 11f, 14f, 9f), 4f,
                        PanelPalette.Alpha(tint, 0.78f), null, 0f);
                    _p.Rounded(new Rect(body.x + 13f, body.y + 9f, 7f, 10f), 2f,
                        PanelPalette.Alpha(tint, 0.30f), tint, 1f);
                    break;
                case EscortOption.Mercenary:
                    // Helmeted mercenary with a rifle held across the body.
                    _p.Rounded(new Rect(body.x + 5f, body.y + 2f, 12f, 8f), 4f, tint, null, 0f);
                    _p.Solid(new Rect(body.x + 6.5f, body.y + 6.5f, 9f, 2f), PanelPalette.Alpha(_c.Card, 0.85f));
                    _p.Rounded(new Rect(body.x + 2f, body.y + 11f, 15f, 9f), 4f,
                        PanelPalette.Alpha(tint, 0.78f), null, 0f);
                    _p.Solid(new Rect(body.x + 11f, body.y + 13f, 9f, 2f), tint);
                    _p.Solid(new Rect(body.x + 13f, body.y + 16f, 2f, 3f), PanelPalette.Alpha(tint, 0.85f));
                    break;
                default:
                    // No escort: a plain dash, the same shape as the "none" option everywhere else.
                    _p.Rounded(new Rect(body.x + 5f, body.y + 10f, 12f, 3f), 1.5f,
                        PanelPalette.Alpha(tint, 0.80f), null, 0f);
                    break;
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

            _p.Text(l.FooterSummary, PanelText.Fit(_p, v.Summary, l.FooterSummary.width, PaintText.Small),
                PaintText.Small);
            var statusColor = v.StatusKind == FooterStatusKind.Danger ? _c.Danger
                : v.StatusKind == FooterStatusKind.Warning ? _c.Warning : _c.TextFaint;
            _p.Text(l.FooterStatus, PanelText.Fit(_p, v.Status, l.FooterStatus.width, PaintText.Tiny),
                PaintText.Tiny, TextAnchor.MiddleLeft, statusColor);

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
            // The card lives inside the left column, so its whole height budget comes from that
            // column minus the 8px breathing room kept above and below it.
            var maxHeight = Mathf.Max(140f, l.Left.height - 16f);
            var room = DescriptionRoom(maxHeight);
            var height = Mathf.Min(TooltipHeight(row, width, room), maxHeight);
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
            var titleWidth = rect.width - 28f;
            _p.Text(new Rect(rect.x + 14f, cursor, titleWidth, 22f),
                PanelText.Fit(_p, row.Name + (row.StackCount > 1 ? "  ×" + row.StackCount : ""), titleWidth,
                    PaintText.TooltipTitle), PaintText.TooltipTitle);
            cursor += 22f;

            if (!string.IsNullOrEmpty(row.Description))
            {
                var body = MeasureTooltip(row, rect.width - 28f, DescriptionRoom(maxHeight));
                _p.Text(new Rect(rect.x + 14f, cursor, rect.width - 28f, body.Height), body.Text,
                    PaintText.TooltipBody, TextAnchor.UpperLeft, null, true);
                cursor += body.Height + 6f;
                _p.Solid(new Rect(rect.x + 14f, cursor, rect.width - 28f, 1f), _c.CardBorder);
                cursor += 7f;
            }

            _p.Text(new Rect(rect.x + 14f, cursor, rect.width - 28f, 18f),
                PanelText.Fit(_p, "价值 " + row.UnitValue.ToString("N0") + " × " + row.StackCount + " = "
                    + row.GoodsValue.ToString("N0"), rect.width - 28f, PaintText.TooltipLine),
                PaintText.TooltipLine);
            cursor += 18f;
            _p.Text(new Rect(rect.x + 14f, cursor, rect.width - 28f, 18f),
                PanelText.Fit(_p, "重量 " + row.WeightKg.ToString("0.##") + " kg　·　品质 " + row.Quality,
                    rect.width - 28f, PaintText.TooltipLine), PaintText.TooltipLine);
            cursor += 18f;
            if (row.Eligible)
                _p.Text(new Rect(rect.x + 14f, cursor, rect.width - 28f, 18f),
                    row.Equipped
                        ? "装备中 · 可寄送，发货时自动卸下"
                        : "可寄送　·　单击加入运送清单",
                    PaintText.TooltipLine, TextAnchor.MiddleLeft, _c.Good);
            else
                _p.Text(new Rect(rect.x + 14f, cursor, rect.width - 28f, 18f),
                    PanelText.Fit(_p, "不可寄：" + row.BlockedReason, rect.width - 28f, PaintText.TooltipLine),
                    PaintText.TooltipLine, TextAnchor.MiddleLeft, _c.Danger);
        }

        private float TooltipHeight(ShipmentRowView row, float width, float room)
        {
            // 12 top + title 22 + three 18px lines + 12 bottom; the description band is added below.
            var height = 12f + 22f + 18f + 18f + 18f + 12f;
            if (!string.IsNullOrEmpty(row.Description))
            {
                height += MeasureTooltip(row, width - 28f, room).Height + 6f + 1f + 7f;
            }
            return height;
        }

        /// <summary>
        /// Height the description band may occupy: the card budget minus its fixed chrome.
        /// </summary>
        private static float DescriptionRoom(float maxHeight) => Mathf.Max(15f, maxHeight - 12f - 22f - 54f - 14f);

        /// <summary>
        /// The description band used to be measured and then clamped to 54px, while the style drew
        /// with Clip: any description longer than three lines lost its tail silently, with no
        /// ellipsis to show there was more. Measure the real height instead, and if the card would
        /// outgrow the column, cut the text at a line boundary and append an ellipsis so what is
        /// shown is a complete thought rather than a severed glyph run.
        /// </summary>
        private TooltipBody MeasureTooltip(ShipmentRowView row, float width, float room)
        {
            var key = (row.InstanceId, Mathf.RoundToInt(room * 4f));
            if (_measuredTooltips.TryGetValue(key, out var cached)) return cached;
            var source = row.Description ?? string.Empty;
            if (source.Length > 220) source = source.Substring(0, 220) + "…";
            var height = _p.Measure(source, width, PaintText.TooltipBody);
            if (height > room)
            {
                var clipped = source;
                while (clipped.Length > 1 && _p.Measure(clipped + "…", width, PaintText.TooltipBody) > room)
                    clipped = clipped.Substring(0, clipped.Length - 1);
                source = clipped.Length > 1 ? clipped + "…" : source;
                height = Mathf.Min(_p.Measure(source, width, PaintText.TooltipBody), room);
            }
            var body = new TooltipBody(source, Mathf.Max(15f, height));
            _measuredTooltips[key] = body;
            return body;
        }

        private readonly struct TooltipBody
        {
            internal readonly string Text;
            internal readonly float Height;

            internal TooltipBody(string text, float height)
            {
                Text = text;
                Height = height;
            }
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
