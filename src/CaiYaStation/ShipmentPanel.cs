using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CaiYaStation.Domain;
using UnityEngine;

namespace CaiYaStation
{
    /// <summary>
    /// Centered shipment panel. Rebuilt around a generated theme: the player should be able to
    /// read goods, cost and risk at a glance, and every disabled action explains itself.
    /// </summary>
    internal sealed class ShipmentPanel : IDisposable
    {
        private readonly List<BackpackItem> _rows = new List<BackpackItem>();
        private readonly HashSet<int> _selected = new HashSet<int>();
        private StationWorld? _world;
        private UiTheme? _theme;
        private string? _stationId;
        private EscortOption _escort = EscortOption.None;
        private Vector2 _scroll;
        private Vector2 _orderScroll;
        private float _nextRefresh;
        private bool _shipping;
        private bool _confirmPending;
        private SelectionTotals? _confirmTotals;
        private FeeQuote? _confirmQuote;
        private bool _confirmSingleStack;
        private string? _error;
        private bool _cursorSaved;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLock;
        private Receipt? _receiptData;
        private string? _receiptText;
        private bool _lossNoticePending;
        private string? _tooltip;
        private Vector2 _tooltipPosition;
        private Vector2 _tooltipScreen;
        private GUIStyle? _badgeDanger;
        private GUIStyle? _badgeNeutral;
        private GUIStyle? _iconLetter;
        private readonly Dictionary<int, float> _lastClickTime = new Dictionary<int, float>();
        private int _lastClickInstance = -1;

        internal Action<string>? ModRuntimeLog;

        internal bool IsOpen => _stationId != null;

        internal void Attach(StationWorld world) => _world = world;

        internal void Open(string stationId)
        {
            _stationId = stationId;
            _selected.Clear();
            _escort = EscortOption.None;
            _error = null;
            _receiptData = null;
            _receiptText = null;
            _scroll = Vector2.zero;
            _nextRefresh = 0;
            _tooltip = null;
            _lossNoticePending = false;
            _confirmPending = false;
            _confirmTotals = null;
            _confirmQuote = null;
            PanelInputGuard.Set(true);
            ApplyCursor();
            RefreshRows();
            ModRuntimeLog?.Invoke("Shipment panel opened station=" + stationId
                                  + " rows=" + _rows.Count);
        }

        internal void Close()
        {
            _stationId = null;
            _selected.Clear();
            _escort = EscortOption.None;
            _error = null;
            _receiptData = null;
            _receiptText = null;
            _lossNoticePending = false;
            _shipping = false;
            _tooltip = null;
            _confirmPending = false;
            _confirmTotals = null;
            _confirmQuote = null;
            RestoreCursor();
            PanelInputGuard.ReleaseDelayed(0.18f);
        }

        internal void CloseIfFar(float maxDistance)
        {
            if (_stationId == null || _world == null || _receiptData != null || _receiptText != null) return;
            var station = _world.Store.Session.Find(_stationId);
            var actor = CharacterMainControl.Main;
            if (station == null || actor == null)
            {
                Close();
                return;
            }
            var position = new Vector3((float)station.X, (float)station.Y, (float)station.Z);
            if (maxDistance > 0 && Vector3.Distance(actor.transform.position, position) > maxDistance) Close();
        }

        internal void Tick()
        {
            if (_stationId == null || _world == null)
            {
                PanelInputGuard.Tick();
                return;
            }
            if (_confirmPending)
            {
                CloseIfFar(6f);
                return;
            }
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.5f;
                RefreshRows();
            }
            CloseIfFar(6f);
        }

        private void RefreshRows()
        {
            if (_world == null) return;
            try
            {
                _rows.Clear();
                _rows.AddRange(_world.Runtime.ReadBackpack());
                var live = new HashSet<int>(_rows.Select(entry => entry.Facts.InstanceId));
                _selected.RemoveWhere(id => !live.Contains(id));
            }
            catch (Exception exception)
            {
                _error = "读取背包失败：" + exception.Message;
            }
        }

        internal UiTheme EnsureTheme()
        {
            if (_theme != null) return _theme;
            _theme = new UiTheme(line => ModRuntimeLog?.Invoke(line));
            _badgeDanger = new GUIStyle(_theme.Badge);
            _badgeDanger.normal.textColor = _theme.Danger;
            _badgeNeutral = new GUIStyle(_theme.Badge);
            _badgeNeutral.normal.textColor = _theme.TextDim;
            _iconLetter = new GUIStyle(_theme.Label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
            _iconLetter.normal.textColor = _theme.Text;
            return _theme;
        }

        internal void Draw(float logicalWidth, float logicalHeight)
        {
            if (_stationId == null || _world == null)
            {
                PanelInputGuard.Tick();
                return;
            }
            try
            {
                var theme = EnsureTheme();
                if (_lossNoticePending)
                {
                    DrawLossNotice(theme, logicalWidth, logicalHeight);
                    return;
                }
                if (_receiptData != null || _receiptText != null)
                {
                    DrawReceipt(theme, logicalWidth, logicalHeight);
                    return;
                }
                if (_confirmPending)
                {
                    DrawConfirmation(theme, logicalWidth, logicalHeight);
                    return;
                }
                ApplyCursor();
                var station = _world.Store.Session.Find(_stationId);
                if (station == null || station.Used)
                {
                    Close();
                    return;
                }
                HandleEscape();

                theme.DrawDim(logicalWidth, logicalHeight);
                var width = Mathf.Min(_world.Config.Ui.PanelWidth, logicalWidth - 32);
                var height = Mathf.Min(_world.Config.Ui.PanelHeight, logicalHeight - 32);
                var panel = new Rect((logicalWidth - width) * 0.5f, (logicalHeight - height) * 0.5f, width, height);
                theme.DrawPanel(panel);
                DrawHeader(theme, panel, station);

                var bodyTop = panel.y + 84;
                var bodyHeight = panel.height - 84 - 74;
                var left = new Rect(panel.x + 20, bodyTop, panel.width - 20 - 340 - 24, bodyHeight);
                var right = new Rect(panel.xMax - 20 - 340, bodyTop, 340, bodyHeight);

                var facts = SelectedFacts();
                var totals = ShipmentRules.Totals(facts);
                var check = ShipmentRules.Check(facts, _world.Config.Limits);
                var quote = ShipmentRules.Quote(totals.GoodsValue, _escort, _world.Config.Pricing, _world.Config.Escorts, _world.Config.Loss);
                // Count each escort item against ITS OWN ItemId. The old code counted with
                // Config.Escort(_escort), whose ItemId is 0 while no escort is selected, so the
                // cards always reported "随身 0/4" even with feathers in the backpack.
                var scavengerCount = CountCarried(_world.Config.Escorts.Scavenger.ItemId);
                var mercenaryCount = CountCarried(_world.Config.Escorts.Mercenary.ItemId);
                var escortRules = _world.Config.Escort(_escort);
                var noBackpack = NoBackpack();
                var paymentBlockers = ShipmentRules.PaymentBlockers(
                    Duckov.Economy.EconomyManager.Money, Duckov.Economy.EconomyManager.Cash,
                    EscortCarried(_escort, scavengerCount, mercenaryCount), escortRules, quote);

                // Warehouse-style layout: left is the backpack, right is the shipping order
                // (selected goods, cost and escort), like the native storage screens.
                DrawItemArea(theme, left);
                DrawShippingOrder(theme, right, totals, quote, scavengerCount, mercenaryCount);
                DrawWarnings(theme, new Rect(right.x, right.yMax - 88, right.width, 84),
                    check, paymentBlockers, noBackpack, totals, quote);
                DrawFooter(theme, panel, totals, check, paymentBlockers, noBackpack, quote);

                if (!string.IsNullOrEmpty(_tooltip)) DrawTooltip(theme, logicalWidth, logicalHeight);
            }
            catch (Exception exception)
            {
                _error = "面板异常：" + exception.Message;
                ModRuntimeLog?.Invoke("Panel draw failed: " + exception);
            }
        }

        private void DrawHeader(UiTheme theme, Rect panel, StationRecord station)
        {
            var header = new Rect(panel.x, panel.y, panel.width, 68);
            theme.DrawHeader(header);
            theme.DrawSolid(new Rect(panel.x, panel.y, 4, 68), theme.Accent);
            var iconRect = new Rect(panel.x + 18, panel.y + 16, 36, 36);
            DrawSprite(iconRect, _world!.Visuals.Icon, "菜", theme, _iconLetter!);

            GUI.Label(new Rect(panel.x + 64, panel.y + 9, 190, 34), "菜鸭驿站", theme.Title);
            var available = _world.Store.Session.AvailableCount(station.MapId);
            var badgeRect = new Rect(panel.x + 190, panel.y + 16, 66, 24);
            theme.DrawPill(badgeRect);
            GUI.Label(badgeRect, available > 0 ? "营业中" : "已停业", available > 0 ? theme.Badge : _badgeDanger!);
            GUI.Label(new Rect(panel.x + 64, panel.y + 41, panel.width - 140, 20),
                MapName(station.MapId) + "　·　送达：地堡马蜂自提点　·　每站每局一次", theme.Subtitle);

            if (GUI.Button(new Rect(panel.xMax - 56, panel.y + 16, 38, 36), "×", theme.CloseButton)) Close();
        }

        private void DrawItemArea(UiTheme theme, Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width - 200, 24), "背包物品", theme.Section);
            var totals = ShipmentRules.Totals(SelectedFacts());
            GUI.Label(new Rect(area.xMax - 260, area.y, 260, 24),
                $"已选 {totals.Stacks}/{_world!.Config.Limits.MaxStacks} 格　{totals.WeightKg:0.##}/{_world.Config.Limits.MaxWeightKg:0.#} kg",
                theme.SmallRight);

            var list = new Rect(area.x, area.y + 30, area.width, area.height - 56);
            theme.DrawCard(list, false);
            var contentWidth = list.width - 22;
            DrawListHeader(theme, new Rect(list.x + 8, list.y + 6, contentWidth, 20));
            var view = new Rect(list.x + 4, list.y + 28, list.width - 8, list.height - 32);
            var content = new Rect(0, 0, contentWidth, Math.Max(view.height, _rows.Count * 36 + 4));
            // Capture the mouse BEFORE BeginScrollView: inside the scroll group IMGUI rewrites
            // Event.current.mousePosition into content space, which made the tooltip drift by
            // the scroll offset and flicker near the top of the panel.
            var panelMouse = new Vector2(Event.current.mousePosition.x, Event.current.mousePosition.y);
            _scroll = GUI.BeginScrollView(view, _scroll, content);
            var mouse = Event.current.mousePosition;
            var localMouse = new Vector2(mouse.x - view.x + _scroll.x, mouse.y - view.y + _scroll.y);
            _tooltip = null;
            var rowY = 0f;
            foreach (var entry in _rows)
            {
                var reason = ItemEligibility.BlockReason(entry.Facts, _world.Config.Protection);
                if (reason == null && _escort != EscortOption.None
                    && entry.Facts.TypeId == _world.Config.Escort(_escort).ItemId)
                    reason = "护送物品不可同时寄送";
                DrawRow(theme, entry, reason, new Rect(0, rowY, contentWidth, 36), localMouse, mouse, panelMouse);
                rowY += 36;
            }
            if (_rows.Count == 0)
                GUI.Label(new Rect(14, 8, contentWidth - 28, 24), "背包里没有可显示物品。", theme.Small);
            GUI.EndScrollView();
            GUI.Label(new Rect(area.x, area.yMax - 20, area.width, 18),
                "单击勾选　·　双击加入右侧运送清单　·　悬停看详情　·　灰色为不可寄", theme.Small);
        }

        private void DrawListHeader(UiTheme theme, Rect rect)
        {
            var width = rect.width;
            GUI.Label(new Rect(rect.x + 32, rect.y, 30, 18), "", theme.Small);
            GUI.Label(new Rect(rect.x + 70, rect.y, width - 370, 18), "名称", theme.Small);
            GUI.Label(new Rect(rect.x + width - 296, rect.y, 42, 18), "数量", theme.SmallRight);
            GUI.Label(new Rect(rect.x + width - 250, rect.y, 62, 18), "单件", theme.SmallRight);
            GUI.Label(new Rect(rect.x + width - 184, rect.y, 76, 18), "总价", theme.SmallRight);
            GUI.Label(new Rect(rect.x + width - 104, rect.y, 54, 18), "重量", theme.SmallRight);
        }

        private void DrawRow(UiTheme theme, BackpackItem entry, string? reason, Rect row, Vector2 localMouse, Vector2 guiMouse, Vector2 panelMouse)
        {
            var selected = _selected.Contains(entry.Facts.InstanceId);
            var hovered = row.Contains(localMouse);
            if (selected) theme.DrawRowSelected(row);
            if (GUI.Button(row, GUIContent.none, theme.RowButton))
            {
                _error = null;
                if (reason == null)
                {
                    // Double click on a shippable row toggles it into the shipping order,
                    // matching the storage-screen interaction the player already knows.
                    var now = Time.unscaledTime;
                    var isDouble = _lastClickInstance == entry.Facts.InstanceId
                                   && now - _lastClickTime.GetValueOrDefault(_lastClickInstance) < 0.45f;
                    _lastClickInstance = entry.Facts.InstanceId;
                    _lastClickTime[entry.Facts.InstanceId] = now;
                    if (isDouble)
                    {
                        _lastClickInstance = -1;
                        if (selected) _selected.Remove(entry.Facts.InstanceId);
                        else
                        {
                            var weightAfter = SelectedFacts().Sum(item => item.TotalWeight) + entry.Facts.TotalWeight;
                            if (_selected.Count < _world!.Config.Limits.MaxStacks
                                && weightAfter <= _world.Config.Limits.MaxWeightKg + 0.0001)
                                _selected.Add(entry.Facts.InstanceId);
                            else
                                _error = "已达 12 格 / 25 kg 上限，先移除一些再试。";
                        }
                    }
                    else
                    {
                        if (selected) _selected.Remove(entry.Facts.InstanceId);
                        else _selected.Add(entry.Facts.InstanceId);
                    }
                }
            }
            if (hovered)
            {
                _tooltip = BuildTooltip(entry, reason);
                _tooltipPosition = guiMouse;
                _tooltipScreen = panelMouse; // panel-space: stable, no scroll-transform drift
            }

            theme.DrawSolid(new Rect(row.x + 1, row.y + 7, 3, 22), UiTheme.QualityColor(entry.Quality));
            theme.DrawCheck(new Rect(row.x + 8, row.y + 9, 18, 18), selected);
            DrawSprite(new Rect(row.x + 32, row.y + 4, 28, 28), entry.Icon, entry.Facts.Name, theme, _iconLetter!);
            GUI.Label(new Rect(row.x + 68, row.y + 7, row.width - 370, 22), entry.Facts.Name,
                reason == null ? theme.RowName : theme.Small);
            if (reason != null)
                GUI.Label(new Rect(row.x + 68, row.y + 22, row.width - 370, 14), "不可寄", theme.Small);
            GUI.Label(new Rect(row.x + row.width - 296, row.y + 8, 42, 20),
                entry.Facts.StackCount > 1 ? "x" + entry.Facts.StackCount : "1", reason == null ? theme.RowValue : theme.SmallRight);
            GUI.Label(new Rect(row.x + row.width - 250, row.y + 8, 62, 20), entry.Facts.UnitValue.ToString("N0"), theme.RowValue);
            GUI.Label(new Rect(row.x + row.width - 184, row.y + 8, 76, 20), entry.Facts.GoodsValue.ToString("N0"),
                reason == null ? theme.RowValue : theme.SmallRight);
            GUI.Label(new Rect(row.x + row.width - 104, row.y + 8, 54, 20),
                entry.Facts.TotalWeight.ToString("0.##") + "kg", theme.SmallRight);
        }

        /// <summary>
        /// Right column of the warehouse layout: the shipping order. From top to bottom: the
        /// manifest of selected goods (double-click in the backpack adds a row), the fee/risk
        /// summary and the escort cards. Mirrors the native storage screen structure.
        /// </summary>
        private void DrawShippingOrder(UiTheme theme, Rect area, SelectionTotals totals, FeeQuote quote,
            long scavengerCount, long mercenaryCount)
        {
            var manifestHeight = 150f;
            var summaryHeight = 168f;
            var escortTitle = new Rect(area.x, area.y + manifestHeight + summaryHeight + 6, area.width, 20);
            var escortY = escortTitle.yMax + 2;

            // --- manifest card -------------------------------------------------------
            var manifest = new Rect(area.x, area.y, area.width, manifestHeight);
            theme.DrawCard(manifest, false);
            GUI.Label(new Rect(manifest.x + 14, manifest.y + 6, manifest.width - 28, 22),
                $"待运送货物　{totals.Stacks} 件", theme.Section);
            var manifestView = new Rect(manifest.x + 6, manifest.y + 30, manifest.width - 12, manifest.height - 36);
            var manifestContentHeight = 0f;
            foreach (var entry in _rows)
                if (_selected.Contains(entry.Facts.InstanceId)) manifestContentHeight += 30f;
            var manifestContent = new Rect(0, 0, manifestView.width - 14, Math.Max(manifestView.height, manifestContentHeight + 2));
            _orderScroll = GUI.BeginScrollView(manifestView, _orderScroll, manifestContent);
            var orderMouse = Event.current.mousePosition;
            var orderRowY = 0f;
            foreach (var entry in _rows)
            {
                if (!_selected.Contains(entry.Facts.InstanceId)) continue;
                var orderRow = new Rect(0, orderRowY, manifestContent.width, 30);
                DrawOrderRow(theme, entry, orderRow, orderMouse);
                orderRowY += 30f;
            }
            if (totals.Stacks == 0)
                GUI.Label(new Rect(10, 8, manifestContent.width - 20, 40),
                    "双击左侧背包物品加入运送清单", theme.Small);
            GUI.EndScrollView();

            // --- cost & risk card ----------------------------------------------------
            var card = new Rect(area.x, manifest.yMax + 6, area.width, summaryHeight);
            theme.DrawCard(card, false);
            GUI.Label(new Rect(card.x + 16, card.y + 8, card.width - 32, 20), "费用与风险", theme.Section);

            var y = card.y + 32f;
            DrawKeyValue(theme, card, ref y, "货值", totals.GoodsValue.ToString("N0"), theme.RowName);
            DrawKeyValue(theme, card, ref y, "运费（10%）", quote.Fee.ToString("N0"), theme.RowName);
            DrawKeyValue(theme, card, ref y, $"基础损耗 {Percent(quote.BaseLossRate)}", "约 " + Math.Round(totals.GoodsValue * quote.BaseLossRate).ToString("N0"), theme.RowName);
            DrawKeyValue(theme, card, ref y, "护送费", quote.EscortMoney.ToString("N0"), theme.RowName);

            theme.DrawSolid(new Rect(card.x + 16, y + 2, card.width - 32, 1), new Color(0.14f, 0.22f, 0.18f, 1f));
            y += 6;
            GUI.Label(new Rect(card.x + 16, y, 130, 24), "预计总成本", theme.Label);
            GUI.Label(new Rect(card.x + card.width - 170, y - 3, 154, 28), quote.ExpectedTotalCost.ToString("N0"), theme.BigValue);
            y += 28;

            var rate = Mathf.Clamp01((float)quote.FinalLossRate);
            var track = new Rect(card.x + 16, y, card.width - 32, 10);
            theme.DrawRiskTrack(track);
            if (rate < 1f)
                theme.DrawSolid(new Rect(track.x + 2, track.y + 2, (track.width - 4) * (1f - rate), 6), theme.Accent);
            if (rate > 0.001f)
                theme.DrawSolid(new Rect(track.x + 2 + (track.width - 4) * (1f - rate), track.y + 2, (track.width - 4) * rate, 6), theme.Danger);
            y += 13;
            GUI.Label(new Rect(card.x + 16, y, card.width - 32, 18),
                $"护送后损耗 {Percent(rate)}　·　最多 30% 件数 / 50% 价值", theme.Small);

            // --- escort cards --------------------------------------------------------
            GUI.Label(escortTitle, "护送队（每单最多一队）", theme.Section);
            var escortArea = new Rect(area.x, escortY, area.width, area.yMax - escortY);
            var ey = escortArea.y;
            var cardHeight = Mathf.Clamp((escortArea.height - 8) / 3f, 44f, 54f);
            DrawEscortCard(theme, new Rect(escortArea.x, ey, escortArea.width, cardHeight), EscortOption.None, "不请护送", "免费", "损耗 " + Percent(QuoteRate(totals.GoodsValue, EscortOption.None)), true, 0, 0);
            ey += cardHeight + 4;
            var scavenger = _world!.Config.Escorts.Scavenger;
            var mercenary = _world.Config.Escorts.Mercenary;
            DrawEscortCard(theme, new Rect(escortArea.x, ey, escortArea.width, cardHeight), EscortOption.Scavenger, scavenger.Name,
                $"{scavenger.Money:N0} + {scavenger.ItemAmount} 羽毛", "损耗 " + Percent(QuoteRate(totals.GoodsValue, EscortOption.Scavenger)),
                CanAfford(scavenger, totals.GoodsValue, scavengerCount), scavengerCount, scavenger.ItemAmount);
            ey += cardHeight + 4;
            DrawEscortCard(theme, new Rect(escortArea.x, ey, escortArea.width, cardHeight), EscortOption.Mercenary, mercenary.Name,
                $"{mercenary.Money:N0} + {mercenary.ItemAmount} 狗牌", "损耗 " + Percent(QuoteRate(totals.GoodsValue, EscortOption.Mercenary)),
                CanAfford(mercenary, totals.GoodsValue, mercenaryCount), mercenaryCount, mercenary.ItemAmount);
        }

        private void DrawOrderRow(UiTheme theme, BackpackItem entry, Rect row, Vector2 mouse)
        {
            var hovered = row.Contains(mouse);
            if (hovered) theme.DrawRowHover(row);
            if (GUI.Button(row, GUIContent.none, theme.RowButton))
                _selected.Remove(entry.Facts.InstanceId);
            theme.DrawSolid(new Rect(row.x + 1, row.y + 5, 3, 20), UiTheme.QualityColor(entry.Quality));
            DrawSprite(new Rect(row.x + 10, row.y + 3, 24, 24), entry.Icon, entry.Facts.Name, theme, _iconLetter!);
            GUI.Label(new Rect(row.x + 40, row.y + 5, row.width - 130, 20), entry.Facts.Name, theme.RowName);
            GUI.Label(new Rect(row.x + row.width - 88, row.y + 6, 44, 18),
                entry.Facts.StackCount > 1 ? "x" + entry.Facts.StackCount : "1", theme.SmallRight);
            GUI.Label(new Rect(row.x + row.width - 42, row.y + 6, 40, 18),
                entry.Facts.GoodsValue.ToString("N0"), theme.SmallRight);
        }

        private void DrawEscortCard(UiTheme theme, Rect rect, EscortOption option, string title, string cost, string effect,
            bool affordable, long carried, long needed)
        {
            var selected = _escort == option;
            GUI.enabled = affordable;
            if (GUI.Button(rect, GUIContent.none, selected ? theme.CardSelected : theme.CardStyle))
            {
                _error = null;
                _escort = option;
            }
            GUI.enabled = true;
            if (selected) theme.DrawSolid(new Rect(rect.x, rect.y + 8, 3, rect.height - 16), theme.Accent);
            GUI.Label(new Rect(rect.x + 14, rect.y + 5, rect.width - 28, 20), title,
                affordable ? (selected ? theme.Label : theme.RowName) : theme.Small);
            GUI.Label(new Rect(rect.x + 14, rect.y + 25, rect.width - 120, 18), cost, theme.Small);
            GUI.Label(new Rect(rect.xMax - 112, rect.y + 25, 100, 18), effect, theme.SmallRight);
            if (!affordable)
                GUI.Label(new Rect(rect.x + 14, rect.y + 38, rect.width - 28, 14),
                    carried < needed ? $"随身 {carried}/{needed}" : "金钱/现金不足", _badgeDanger!);
        }

        private static void DrawKeyValue(UiTheme theme, Rect card, ref float y, string key, string value, GUIStyle valueStyle)
        {
            GUI.Label(new Rect(card.x + 16, y, card.width - 160, 20), key, theme.Small);
            GUI.Label(new Rect(card.x + card.width - 136, y, 120, 20), value, valueStyle);
            y += 22;
        }

        private void DrawWarnings(UiTheme theme, Rect area, SelectionCheck check, List<string> paymentBlockers,
            bool noBackpack, SelectionTotals totals, FeeQuote quote)
        {
            var reasons = new List<string>();
            var warnings = new List<string>();
            if (noBackpack) reasons.Add("需要背包");
            if (!check.Allowed && check.Reason != null) reasons.Add(check.Reason);
            reasons.AddRange(paymentBlockers);
            if (PlayerStorage.Loading || PlayerStorage.IncomingItemBuffer == null) reasons.Add("到货缓冲区不可用");
            if (totals.Stacks == 1 && quote.FinalLossRate > 0) warnings.Add("单件寄送可能全损，建议请护送");
            if (totals.Stacks > 0 && quote.Fee > totals.GoodsValue) warnings.Add("运费高于货值");
            if (totals.Stacks > 0 && quote.EscortMoney > totals.GoodsValue) warnings.Add("护送费高于货值");
            if (!string.IsNullOrEmpty(_error)) reasons.Add(_error!);

            var text = new StringBuilder();
            foreach (var warning in warnings) text.Append("· ").Append(warning).Append('\n');
            foreach (var reason in reasons.Distinct()) text.Append("× ").Append(reason).Append('\n');
            if (text.Length == 0) text.Append("选择物品后即可发货；发货后请回地堡马蜂自提点领取。");
            var style = reasons.Count > 0 ? theme.WarningText : theme.Small;
            GUI.Label(new Rect(area.x, area.y, area.width, area.height), text.ToString().TrimEnd(), style);
        }

        private void DrawFooter(UiTheme theme, Rect panel, SelectionTotals totals, SelectionCheck check,
            List<string> paymentBlockers, bool noBackpack, FeeQuote quote)
        {
            var footer = new Rect(panel.x + 20, panel.yMax - 64, panel.width - 40, 48);
            theme.DrawSolid(new Rect(panel.x + 20, panel.yMax - 68, panel.width - 40, 1), new Color(0.14f, 0.22f, 0.18f, 1f));
            GUI.Label(new Rect(footer.x, footer.y + 4, 300, 20),
                $"金钱 {Duckov.Economy.EconomyManager.Money:N0}　现金 {Duckov.Economy.EconomyManager.Cash:N0}", theme.Label);
            GUI.Label(new Rect(footer.x, footer.y + 25, 420, 18),
                $"已选 {totals.Stacks} 件　·　实扣 {quote.TotalUpfront:N0}（运费+护送费）　·　预计总成本 {quote.ExpectedTotalCost:N0}", theme.Small);

            var reasons = new List<string>();
            if (noBackpack) reasons.Add("需要背包");
            if (!check.Allowed && check.Reason != null) reasons.Add(check.Reason);
            reasons.AddRange(paymentBlockers);
            if (PlayerStorage.Loading || PlayerStorage.IncomingItemBuffer == null) reasons.Add("缓冲区不可用");
            if (!string.IsNullOrEmpty(_error)) reasons.Add(_error!);

            var close = new Rect(footer.xMax - 82, footer.y + 8, 82, 32);
            if (GUI.Button(close, "关闭", theme.ButtonSmall)) Close();
            var confirm = new Rect(close.x - 176, footer.y + 8, 160, 32);
            var canShip = !_shipping && reasons.Count == 0;
            GUI.enabled = canShip;
            if (GUI.Button(confirm, _shipping ? "发货中…" : "确认发货", canShip ? theme.ButtonPrimary : theme.Button)) RequestShip();
            GUI.enabled = true;
            if (canShip && Event.current != null && Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                RequestShip();
                Event.current.Use();
            }
            var clear = new Rect(confirm.x - 92, footer.y + 8, 84, 32);
            if (GUI.Button(clear, "清空", theme.ButtonSmall))
            {
                _error = null;
                _selected.Clear();
            }
            var all = new Rect(clear.x - 118, footer.y + 8, 110, 32);
            if (GUI.Button(all, "全选可寄", theme.ButtonSmall)) SelectAllEligible();
        }

        private void DrawReceipt(UiTheme theme, float logicalWidth, float logicalHeight)
        {
            HandleEscape();
            theme.DrawDim(logicalWidth, logicalHeight);
            var width = Mathf.Min(760, logicalWidth - 40);
            var height = Mathf.Min(570, logicalHeight - 40);
            var panel = new Rect((logicalWidth - width) * 0.5f, (logicalHeight - height) * 0.5f, width, height);
            theme.DrawPanel(panel);
            var receipt = _receiptData;
            var header = new Rect(panel.x, panel.y, panel.width, 68);
            theme.DrawHeader(header);
            theme.DrawSolid(new Rect(panel.x, panel.y, 4, 68), theme.Accent);
            GUI.Label(new Rect(panel.x + 20, panel.y + 10, 300, 32), "发货回执", theme.Title);
            var badge = new Rect(panel.x + 150, panel.y + 17, 92, 24);
            theme.DrawPill(badge);
            GUI.Label(badge, receipt == null ? "已完成" : receipt.DeliveredStacks > 0 ? "已送达" : "全部丢失",
                receipt == null || receipt.DeliveredStacks > 0 ? theme.Badge : _badgeDanger!);
            GUI.Label(new Rect(panel.x + 20, panel.y + 42, panel.width - 40, 20),
                "菜鸭驿站 → 地堡马蜂自提点　·　货物已写入到货缓冲区", theme.Subtitle);
            if (GUI.Button(new Rect(panel.xMax - 54, panel.y + 16, 36, 34), "×", theme.CloseButton)) Close();

            if (receipt == null)
            {
                GUI.Label(new Rect(panel.x + 24, panel.y + 90, panel.width - 48, panel.height - 140), _receiptText ?? "", theme.Label);
                if (GUI.Button(new Rect(panel.xMax - 130, panel.yMax - 50, 100, 32), "关闭", theme.Button)) Close();
                return;
            }

            var chipY = panel.y + 84;
            DrawChip(theme, new Rect(panel.x + 24, chipY, 170, 52), "送达", receipt.DeliveredStacks + " 件", receipt.DeliveredValue.ToString("N0") + " 价值", theme.Good);
            DrawChip(theme, new Rect(panel.x + 204, chipY, 170, 52), "丢失", receipt.LostStacks + " 件", receipt.LostValue.ToString("N0") + " 价值", receipt.LostStacks > 0 ? theme.Danger : theme.TextDim);
            DrawChip(theme, new Rect(panel.x + 384, chipY, 160, 52), "运费", receipt.Fee.ToString("N0"), receipt.EscortMoney > 0 ? "护送 " + receipt.EscortMoney.ToString("N0") : "无护送", theme.Text);
            DrawChip(theme, new Rect(panel.x + 554, chipY, 170, 52), "护送", receipt.EscortName, "损耗 " + Percent(receipt.LossRate), theme.Accent);

            var listTop = panel.y + 152;
            var listHeight = panel.height - 152 - 92;
            DrawReceiptList(theme, new Rect(panel.x + 24, listTop, (panel.width - 60) / 2, listHeight), "已送达", receipt.Delivered, theme.Good);
            DrawReceiptList(theme, new Rect(panel.x + 36 + (panel.width - 60) / 2, listTop, (panel.width - 60) / 2, listHeight), "丢失", receipt.Lost, theme.Danger);

            if (!string.IsNullOrWhiteSpace(receipt.Flavor))
                GUI.Label(new Rect(panel.x + 24, panel.yMax - 82, panel.width - 48, 20), receipt.Flavor, theme.Small);
            if (GUI.Button(new Rect(panel.xMax - 130, panel.yMax - 52, 100, 32), "关闭", theme.Button)) Close();
        }

        private static void DrawChip(UiTheme theme, Rect rect, string label, string value, string sub, Color valueColor)
        {
            theme.DrawCard(rect, false);
            GUI.Label(new Rect(rect.x + 10, rect.y + 4, rect.width - 20, 16), label, theme.Small);
            var previous = GUI.color;
            GUI.color = valueColor;
            GUI.Label(new Rect(rect.x + 10, rect.y + 18, rect.width - 20, 22), value, theme.Label);
            GUI.color = previous;
            GUI.Label(new Rect(rect.x + 10, rect.y + 38, rect.width - 20, 14), sub, theme.Small);
        }

        private static void DrawReceiptList(UiTheme theme, Rect area, string title, List<ReceiptLine> lines, Color accent)
        {
            theme.DrawCard(area, false);
            theme.DrawSolid(new Rect(area.x, area.y + 10, 3, 22), accent);
            GUI.Label(new Rect(area.x + 12, area.y + 8, area.width - 24, 24), title + "　" + lines.Count + " 件", theme.Section);
            var y = area.y + 40f;
            foreach (var line in lines)
            {
                if (y + 22 > area.yMax) break;
                GUI.Label(new Rect(area.x + 12, y, area.width - 110, 20), line.Name + " x" + line.StackCount, theme.RowName);
                GUI.Label(new Rect(area.xMax - 96, y, 84, 20), line.GoodsValue.ToString("N0"), theme.RowValue);
                y += 22;
            }
            if (lines.Count == 0)
                GUI.Label(new Rect(area.x + 12, y, area.width - 24, 20), "无", theme.Small);
        }

        private void HandleEscape()
        {
            var current = Event.current;
            if (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                Close();
                current.Use();
            }
        }

        private void SelectAllEligible()
        {
            if (_world == null) return;
            _error = null;
            _selected.Clear();
            var stacks = 0;
            var weight = 0.0;
            foreach (var entry in _rows)
            {
                if (ItemEligibility.BlockReason(entry.Facts, _world.Config.Protection) != null) continue;
                if (_escort != EscortOption.None && entry.Facts.TypeId == _world.Config.Escort(_escort).ItemId) continue;
                if (stacks + 1 > _world.Config.Limits.MaxStacks) break;
                if (weight + entry.Facts.TotalWeight > _world.Config.Limits.MaxWeightKg + 0.0001) continue;
                _selected.Add(entry.Facts.InstanceId);
                stacks++;
                weight += entry.Facts.TotalWeight;
            }
        }

        private List<ItemFacts> SelectedFacts()
        {
            var result = new List<ItemFacts>();
            foreach (var entry in _rows)
                if (_selected.Contains(entry.Facts.InstanceId)) result.Add(entry.Facts);
            return result;
        }

        private void RequestShip()
        {
            if (_world == null || _shipping) return;
            var facts = SelectedFacts();
            var check = ShipmentRules.Check(facts, _world.Config.Limits);
            if (!check.Allowed || facts.Count == 0)
            {
                _error = check.Reason ?? "请选择要寄送的物品";
                return;
            }
            var quote = ShipmentRules.Quote(check.Totals.GoodsValue, _escort,
                _world.Config.Pricing, _world.Config.Escorts, _world.Config.Loss);
            var safety = _world.Config.Safety;
            var needsConfirmation = (safety.ConfirmAboveValue > 0 && check.Totals.GoodsValue >= safety.ConfirmAboveValue)
                                    || (safety.ConfirmWhenSingleStack && check.Totals.Stacks == 1 && quote.FinalLossRate > 0);
            if (!needsConfirmation)
            {
                Ship();
                return;
            }
            _confirmPending = true;
            _confirmTotals = check.Totals;
            _confirmQuote = quote;
            _confirmSingleStack = check.Totals.Stacks == 1;
            _error = null;
        }

        private void CloseConfirmation()
        {
            _confirmPending = false;
            _confirmTotals = null;
            _confirmQuote = null;
        }

        /// <summary>
        /// The loss notice sheet: culprit line + pained kaomoji, the list of lost stacks,
        /// the total value lost, and a sign button that closes the sheet.
        /// </summary>
        private void DrawLossNotice(UiTheme theme, float logicalWidth, float logicalHeight)
        {
            var receipt = _receiptData;
            HandleEscape();
            if (receipt == null || !receipt.NeedsLossNotice)
            {
                _lossNoticePending = false;
                return;
            }

            theme.DrawDim(logicalWidth, logicalHeight);
            var width = Mathf.Min(560, logicalWidth - 40);
            var listHeight = Mathf.Min(190, Mathf.Max(74, receipt.Lost.Count * 30 + 46));
            var height = 300f + listHeight - 74f;
            var panel = new Rect((logicalWidth - width) * 0.5f, (logicalHeight - height) * 0.5f, width, height);
            theme.DrawPanel(panel);
            theme.DrawHeader(new Rect(panel.x, panel.y, panel.width, 64));
            theme.DrawSolid(new Rect(panel.x, panel.y, 4, 64), theme.Danger);
            theme.DrawSolid(new Rect(panel.x, panel.y, panel.width, 5),
                new Color(theme.Danger.r, theme.Danger.g, theme.Danger.b, 1f));
            GUI.Label(new Rect(panel.x + 20, panel.y + 8, 320, 32), "运送损失通知单", theme.Title);
            var badge = new Rect(panel.x + 186, panel.y + 16, 92, 24);
            theme.DrawPill(badge);
            GUI.Label(badge, "丢件确认", _badgeDanger!);
            GUI.Label(new Rect(panel.x + 20, panel.y + 40, panel.width - 40, 18),
                "菜鸭驿站 · 配送事故备案　·　签收后不再追偿（雾）", theme.Subtitle);

            var y = panel.y + 84f;
            var culprits = string.IsNullOrWhiteSpace(receipt.LossCulprits) ? "不明劫掠者" : receipt.LossCulprits;
            var kaomoji = string.IsNullOrWhiteSpace(receipt.LossKaomoji) ? "(´;ω;`)" : receipt.LossKaomoji;
            var culpritsStyle = new GUIStyle(theme.Label) { fontSize = 18, fontStyle = FontStyle.Bold };
            culpritsStyle.normal.textColor = new Color(1f, 0.62f, 0.56f, 1f);
            var culpritsText = $"我们的人被{culprits}暴打了，货也弄丢了…{kaomoji}";
            var culpritsContent = new GUIContent(culpritsText);
            var culpritsHeight = culpritsStyle.CalcHeight(culpritsContent, panel.width - 40);
            GUI.Label(new Rect(panel.x + 20, y, panel.width - 40, culpritsHeight), culpritsContent, culpritsStyle);
            culpritsStyle.wordWrap = true;
            y += culpritsHeight + 8;

            // Lost item list.
            var listTop = y;
            theme.DrawCard(new Rect(panel.x + 20, listTop, panel.width - 40, listHeight), false);
            theme.DrawSolid(new Rect(panel.x + 20, listTop + 10, 3, 22), theme.Danger);
            GUI.Label(new Rect(panel.x + 34, listTop + 8, panel.width - 80, 24),
                "丢失物品　" + receipt.Lost.Count + " 件", theme.Section);
            var rowY = listTop + 36f;
            foreach (var line in receipt.Lost)
            {
                if (rowY + 24 > listTop + listHeight - 6) break;
                GUI.Label(new Rect(panel.x + 34, rowY, panel.width - 170, 20),
                    line.Name + (line.StackCount > 1 ? " ×" + line.StackCount : ""), theme.RowName);
                GUI.Label(new Rect(panel.xMax - 140, rowY, 118, 20),
                    "-" + line.GoodsValue.ToString("N0"), theme.SmallRight);
                rowY += 24f;
            }

            // Total loss.
            var totalY = listTop + listHeight + 8;
            GUI.Label(new Rect(panel.x + 20, totalY, 220, 28), "总计损失价值", theme.Label);
            var lossStyle = new GUIStyle(theme.BigValue);
            lossStyle.normal.textColor = theme.Danger;
            lossStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(panel.xMax - 220, totalY - 3, 200, 30),
                "-" + receipt.LostValue.ToString("N0"), lossStyle);
            GUI.Label(new Rect(panel.x + 20, totalY + 30, panel.width - 40, 18),
                $"本次送达 {receipt.DeliveredStacks} 件 / 价值 {receipt.DeliveredValue:N0}　·　已扣运费 {receipt.Fee:N0}", theme.Small);

            // Sign button.
            var sign = new Rect(panel.xMax - 220, panel.yMax - 58, 200, 38);
            if (GUI.Button(sign, "签收（确认损失）", theme.ButtonPrimary)
                || (Event.current != null && Event.current.type == EventType.KeyDown
                    && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)))
            {
                _lossNoticePending = false;
                if (Event.current != null && Event.current.type == EventType.KeyDown) Event.current.Use();
            }
            if (GUI.Button(new Rect(panel.x + 20, panel.yMax - 58, 150, 38), "直接看回执", theme.Button))
                _lossNoticePending = false;
        }

        private void DrawConfirmation(UiTheme theme, float logicalWidth, float logicalHeight)
        {
            var current = Event.current;
            if (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                CloseConfirmation();
                current.Use();
                return;
            }
            var totals = _confirmTotals;
            var quote = _confirmQuote;
            if (totals == null || quote == null)
            {
                CloseConfirmation();
                return;
            }
            theme.DrawDim(logicalWidth, logicalHeight);
            var width = Mathf.Min(560, logicalWidth - 40);
            var height = 300f;
            var panel = new Rect((logicalWidth - width) * 0.5f, (logicalHeight - height) * 0.5f, width, height);
            theme.DrawPanel(panel);
            theme.DrawHeader(new Rect(panel.x, panel.y, panel.width, 64));
            theme.DrawSolid(new Rect(panel.x, panel.y, 4, 64), theme.Accent);
            GUI.Label(new Rect(panel.x + 20, panel.y + 8, 280, 32), "确认发货", theme.Title);
            var badge = new Rect(panel.x + 150, panel.y + 16, 90, 24);
            theme.DrawPill(badge);
            GUI.Label(badge, _confirmSingleStack ? "单件风险" : "高价值", theme.Badge);
            GUI.Label(new Rect(panel.x + 20, panel.y + 40, panel.width - 40, 18),
                "确认后立即扣除费用并随机结算丢件，不能撤销。", theme.Subtitle);

            var y = panel.y + 82f;
            DrawConfirmKeyValue(theme, panel, ref y, "货值", totals.GoodsValue.ToString("N0"));
            DrawConfirmKeyValue(theme, panel, ref y, "预计损耗", Percent(quote.FinalLossRate));
            DrawConfirmKeyValue(theme, panel, ref y, "实际扣除", quote.TotalUpfront.ToString("N0") + "（运费+护送费）");
            DrawConfirmKeyValue(theme, panel, ref y, "预计总成本", quote.ExpectedTotalCost.ToString("N0"));
            var warning = _confirmSingleStack
                ? "只选了一个堆叠：按面板显示的概率，可能全损。"
                : "货值较高：请确认选中的物品和护送方式无误。";
            GUI.Label(new Rect(panel.x + 20, y + 4, panel.width - 40, 40), warning, theme.WarningText);

            if (GUI.Button(new Rect(panel.x + 20, panel.yMax - 54, 150, 34), "返回修改", theme.Button)) CloseConfirmation();
            if (GUI.Button(new Rect(panel.xMax - 220, panel.yMax - 54, 200, 34), "确认扣费并发货", theme.ButtonPrimary))
            {
                CloseConfirmation();
                Ship();
            }
        }

        private static void DrawConfirmKeyValue(UiTheme theme, Rect panel, ref float y, string key, string value)
        {
            GUI.Label(new Rect(panel.x + 20, y, 220, 20), key, theme.Small);
            GUI.Label(new Rect(panel.xMax - 220, y, 200, 20), value, theme.RowName);
            y += 22;
        }

        private void Ship()
        {
            if (_world == null || _shipping || _stationId == null) return;
            _shipping = true;
            _error = null;
            try
            {
                var result = _world.Runtime.Ship(_stationId, _selected, _escort);
                if (result.Success)
                {
                    _world.MarkStationUsed(_stationId);
                    _receiptData = result.ReceiptData;
                    _receiptText = result.ReceiptText ?? result.Message;
                    // Lost goods come with a signed loss-notice sheet; show it before the receipt.
                    _lossNoticePending = result.ReceiptData != null && result.ReceiptData.NeedsLossNotice;
                    _selected.Clear();
                    _world.Notify(result.Message, true, "菜鸭驿站 · 发货完成",
                        StationWorld.NoticeKind.Success, "回地堡马蜂自提点领取", "完成");
                }
                else
                {
                    _error = result.Message;
                }
            }
            catch (Exception exception)
            {
                _error = "发货失败：" + exception.Message;
                ModRuntimeLog?.Invoke("Shipment failed: " + exception);
            }
            finally
            {
                _shipping = false;
                RefreshRows();
            }
        }

        private void DrawTooltip(UiTheme theme, float width, float height)
        {
            // The hover position is captured inside BeginScrollView (content space). Convert to
            // panel space by subtracting the scroll offset delta: guiMouse already is panel
            // space, while _tooltipPosition is content space; storing both would drift and make
            // the box jitter near the top of the panel. Use the panel-space mouse captured at
            // hover time, clamped every frame — no flicker.
            var content = new GUIContent(_tooltip);
            var tooltipWidth = 300f;
            var tooltipHeight = Mathf.Min(210, theme.Tooltip.CalcHeight(content, tooltipWidth - 20));
            var x = Mathf.Clamp(_tooltipScreen.x + 18, 0, width - tooltipWidth);
            var y = Mathf.Clamp(_tooltipScreen.y + 18, 0, height - tooltipHeight - 8);
            GUI.Box(new Rect(x, y, tooltipWidth, tooltipHeight + 22), _tooltip, theme.Tooltip);
        }

        private string BuildTooltip(BackpackItem entry, string? reason)
        {
            var builder = new StringBuilder();
            builder.Append(entry.Facts.Name);
            if (entry.Facts.StackCount > 1) builder.Append(" ×").Append(entry.Facts.StackCount);
            builder.Append('\n');
            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                var description = entry.Description.Replace("<", "‹").Replace(">", "›");
                if (description.Length > 180) description = description.Substring(0, 180) + "…";
                builder.Append(description).Append('\n');
            }
            builder.Append("价值 ").Append(entry.Facts.UnitValue.ToString("N0")).Append(" × ").Append(entry.Facts.StackCount)
                .Append(" = ").Append(entry.Facts.GoodsValue.ToString("N0")).Append("　·　重量 ")
                .Append(entry.Facts.TotalWeight.ToString("0.##")).Append(" kg\n");
            builder.Append(reason != null ? "不可寄：" + reason : "点击选择 / 取消");
            return builder.ToString().TrimEnd();
        }

        private double QuoteRate(long goodsValue, EscortOption option)
        {
            var quote = ShipmentRules.Quote(goodsValue, option, _world!.Config.Pricing, _world.Config.Escorts, _world.Config.Loss);
            return quote.FinalLossRate;
        }

        private long CountCarried(int typeId)
        {
            if (typeId <= 0) return 0;
            return _rows.Where(entry => entry.Facts.TypeId == typeId)
                .Sum(entry => (long)entry.Facts.StackCount);
        }

        private static long EscortCarried(EscortOption option, long scavengerCount, long mercenaryCount)
            => option switch
            {
                EscortOption.Scavenger => scavengerCount,
                EscortOption.Mercenary => mercenaryCount,
                _ => long.MaxValue
            };

        private bool CanAfford(EscortRules rules, long goodsValue, long escortCount)
        {
            var quote = ShipmentRules.Quote(goodsValue, rules.Option, _world!.Config.Pricing, _world.Config.Escorts, _world.Config.Loss);
            return Duckov.Economy.EconomyManager.Money + Duckov.Economy.EconomyManager.Cash >= quote.TotalUpfront
                   && escortCount >= rules.ItemAmount;
        }

        private bool NoBackpack()
        {
            try
            {
                var backpack = CharacterMainControl.Main?.CharacterItem?.Inventory;
                return backpack == null || backpack.Capacity <= 0;
            }
            catch { return true; }
        }

        private static void DrawSprite(Rect rect, Sprite? sprite, string fallback, UiTheme theme, GUIStyle letterStyle)
        {
            try
            {
                if (sprite != null && sprite.texture != null)
                {
                    var texture = sprite.texture;
                    var textureRect = sprite.textureRect;
                    GUI.DrawTextureWithTexCoords(rect, texture,
                        new Rect(textureRect.x / texture.width, textureRect.y / texture.height,
                            textureRect.width / texture.width, textureRect.height / texture.height), true);
                    return;
                }
            }
            catch { }
            theme.DrawSolid(rect, new Color(0.10f, 0.16f, 0.13f, 0.9f));
            if (!string.IsNullOrEmpty(fallback)) GUI.Label(rect, fallback.Substring(0, 1), letterStyle);
        }

        private static string Percent(double value) => (value * 100).ToString("0.#") + "%";

        private static string MapName(string mapId) => mapId switch
        {
            "Level_HiddenWarehouse_Main" => "仓库区",
            "Level_Farm_Main" => "农场镇",
            _ => mapId
        };

        public void Dispose()
        {
            RestoreCursor();
            PanelInputGuard.Set(false);
            _theme?.Dispose();
            _theme = null;
        }

        private void ApplyCursor()
        {
            if (_world == null || !_world.Config.Ui.UnlockCursor) return;
            if (!_cursorSaved)
            {
                _previousCursorVisible = Cursor.visible;
                _previousCursorLock = Cursor.lockState;
                _cursorSaved = true;
            }
            Cursor.visible = true;
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        }

        private void RestoreCursor()
        {
            if (!_cursorSaved) return;
            _cursorSaved = false;
            try
            {
                Cursor.visible = _previousCursorVisible;
                Cursor.lockState = _previousCursorLock;
            }
            catch { }
        }
    }
}
