using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CaiYaStation.Domain;
using ItemStatsSystem;
using UnityEngine;

namespace CaiYaStation
{
    /// <summary>
    /// Shipment panel controller. Drawing lives in <see cref="PanelRenderer"/>; this class owns the
    /// live state (backpack snapshot, selection, escort, modal screens) and turns mouse events into
    /// actions using the very rects the renderer just painted.
    ///
    /// There is deliberately no GUI.BeginScrollView and no GUI.Button here: IMGUI rewrites
    /// Event.mousePosition inside groups, and mixing that with hand-rolled hover maths is what made
    /// the tooltip and row highlight flicker. Scrolling, clipping and hit-testing are all done in
    /// panel space, so hover is stable by construction.
    /// </summary>
    internal sealed class ShipmentPanel : IDisposable
    {
        private readonly List<BackpackItem> _rows = new List<BackpackItem>();
        private readonly List<BackpackItem> _readBuffer = new List<BackpackItem>();
        private readonly HashSet<int> _selected = new HashSet<int>();
        private readonly HashSet<int> _liveIds = new HashSet<int>();
        private readonly Dictionary<int, ShipmentRowView> _rowViews = new Dictionary<int, ShipmentRowView>();
        private readonly List<ShipmentRowView> _manifest = new List<ShipmentRowView>();
        private readonly List<EscortCardView> _escortCards = new List<EscortCardView>(3);
        private readonly ShipmentView _view = new ShipmentView();
        private StationWorld? _world;
        private UiTheme? _theme;
        private GuiPanelPainter? _painter;
        private PanelRenderer? _renderer;
        private string? _stationId;
        private EscortOption _escort = EscortOption.None;
        private float _listScroll;
        private float _manifestScroll;
        private float _nextRefresh;
        private bool _shipping;
        private bool _confirmPending;
        private SelectionTotals? _confirmTotals;
        private FeeQuote? _confirmQuote;
        private bool _confirmSingleStack;
        private string? _error;
        private string? _hint;
        private float _hintUntil;
        private bool _cursorSaved;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLock;
        private Receipt? _receiptData;
        private string? _receiptText;
        private bool _lossNoticePending;
        private bool _standalone;
        private string? _pressedHit;
        private float _lastClampLog = -100f;
        private int _hoverId = -1;
        private int _hoverChanges;
        private float _hoverWindow;
        private float _lastHoverLog = -100f;

        /// <summary>
        /// Logical pixels one wheel notch moves the list. A whole row would be 40; a hair more
        /// keeps the last row of a page from being clipped at the edge of the viewport.
        /// </summary>
        private const float ScrollStep = 44f;
        private GUIStyle? _badgeDanger;
        private ReceiptRenderer? _receiptRenderer;
        private float _receiptDeliveredScroll;
        private float _receiptLostScroll;
        private readonly HashSet<int> _iconlessTypeIds = new HashSet<int>();

        internal Action<string>? ModRuntimeLog;

        /// <summary>Raised after a bunker receipt is dismissed so the receipt stops being pending.</summary>
        internal Action<Receipt>? ReceiptClosed;

        internal bool IsOpen => _stationId != null;

        /// <summary>True while a receipt (with or without a station behind it) owns the screen.</summary>
        internal bool IsReceiptOpen => _receiptData != null || _receiptText != null;

        internal void Attach(StationWorld world)
        {
            _world = world;
        }

        internal void Open(string stationId)
        {
            _stationId = stationId;
            _standalone = false;
            _selected.Clear();
            _escort = EscortOption.None;
            _error = null;
            _hint = null;
            _receiptData = null;
            _receiptText = null;
            _listScroll = 0f;
            _manifestScroll = 0f;
            _nextRefresh = 0f;
            _lossNoticePending = false;
            _confirmPending = false;
            _confirmTotals = null;
            _confirmQuote = null;
            _pressedHit = null;
            _renderer?.ResetMeasurements();
            PanelInputGuard.Set(true);
            ApplyCursor();
            RefreshRows();
            ModRuntimeLog?.Invoke("Shipment panel opened station=" + stationId
                                  + " rows=" + _rows.Count);
        }

        /// <summary>
        /// Shows a delivered receipt on its own, with no station behind it. Called when the
        /// player is back at the bunker: the sheet is the only reason the panel opens there.
        /// </summary>
        internal void OpenReceipt(Receipt receipt)
        {
            _stationId = null;
            _standalone = true;
            _selected.Clear();
            _escort = EscortOption.None;
            _error = null;
            _hint = null;
            _receiptData = receipt;
            _receiptText = null;
            _lossNoticePending = receipt.NeedsLossNotice;
            _confirmPending = false;
            _confirmTotals = null;
            _confirmQuote = null;
            _shipping = false;
            _pressedHit = null;
            PanelInputGuard.Set(true);
            ApplyCursor();
            ModRuntimeLog?.Invoke("Receipt opened at bunker id=" + receipt.ShipmentId
                                  + " delivered=" + receipt.DeliveredStacks
                                  + " lost=" + receipt.LostStacks);
        }

        internal void Close()
        {
            var closedReceipt = _standalone ? _receiptData : null;
            _stationId = null;
            _standalone = false;
            _selected.Clear();
            _escort = EscortOption.None;
            _error = null;
            _hint = null;
            _receiptData = null;
            _receiptText = null;
            _lossNoticePending = false;
            _shipping = false;
            _pressedHit = null;
            _confirmPending = false;
            _confirmTotals = null;
            _confirmQuote = null;
            RestoreCursor();
            PanelInputGuard.ReleaseDelayed(0.18f);
            if (closedReceipt != null) ReceiptClosed?.Invoke(closedReceipt);
        }

        /// <summary>
        /// Closes a bunker receipt without marking it read. Used when the player leaves the base
        /// with the sheet still open: the receipt stays unread and shows up again next time.
        /// </summary>
        internal void DismissReceipt()
        {
            if (!IsReceiptOpen) return;
            var standalone = _standalone;
            if (standalone) _stationId = null;
            _standalone = false;
            _receiptData = null;
            _receiptText = null;
            _lossNoticePending = false;
            _pressedHit = null;
            if (!standalone) return;
            RestoreCursor();
            PanelInputGuard.ReleaseDelayed(0.18f);
            ModRuntimeLog?.Invoke("Receipt dismissed unread (left the bunker)");
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
            if (_confirmPending || _lossNoticePending || _receiptData != null || _receiptText != null)
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
                _readBuffer.Clear();
                _readBuffer.AddRange(_world.Runtime.ReadBackpack());
                if (!RowsChanged())
                {
                    _liveIds.Clear();
                    foreach (var entry in _rows) _liveIds.Add(entry.Facts.InstanceId);
                    _selected.RemoveWhere(id => !_liveIds.Contains(id));
                    return;
                }
                _rows.Clear();
                _rows.AddRange(_readBuffer);
                _liveIds.Clear();
                foreach (var entry in _rows) _liveIds.Add(entry.Facts.InstanceId);
                _selected.RemoveWhere(id => !_liveIds.Contains(id));
            }
            catch (Exception exception)
            {
                _error = "读取背包失败：" + exception.Message;
            }
        }

        private bool RowsChanged()
        {
            if (_readBuffer.Count != _rows.Count) return true;
            for (var i = 0; i < _rows.Count; i++)
            {
                var a = _rows[i].Facts;
                var b = _readBuffer[i].Facts;
                if (a.InstanceId != b.InstanceId || a.StackCount != b.StackCount || a.UnitValue != b.UnitValue
                    || a.TypeId != b.TypeId || a.HasContainerInventory != b.HasContainerInventory)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ drawing

        internal UiTheme EnsureTheme()
        {
            if (_theme != null) return _theme;
            // UiTheme touches GUI.skin, so it may only be built inside OnGUI.
            _theme = new UiTheme(line => ModRuntimeLog?.Invoke(line));
            _badgeDanger = new GUIStyle(_theme.Badge);
            _badgeDanger.normal.textColor = _theme.Danger;
            var palette = _theme.Palette;
            _painter = new GuiPanelPainter(palette);
            _renderer = new PanelRenderer(palette, _painter);
            _receiptRenderer = new ReceiptRenderer(_painter, palette, ItemIcon, ItemName,
                line => ModRuntimeLog?.Invoke(line));
            return _theme;
        }

        internal void Draw(float logicalWidth, float logicalHeight)
        {
            if (_world == null) return;
            try
            {
                var theme = EnsureTheme();
                // Every screen of the panel needs a visible pointer: the game re-hides the
                // cursor while the player keeps looking around, so re-assert it each frame.
                ApplyCursor();
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
                if (_stationId == null) return;
                var station = _world.Store.Session.Find(_stationId);
                if (station == null || station.Used)
                {
                    // Closing without a word looks like the panel crashed; say why it went away.
                    _world.Notify(station == null
                            ? "这个驿站已经不在当前存档里了，面板已关闭。"
                            : "这个驿站本局已经发过货了，下一局再来。", false,
                        "菜鸭驿站 · 已停业", StationWorld.NoticeKind.Warning, "每站每局只能发货一次", "停业");
                    Close();
                    return;
                }
                HandleEscape();
                if (_stationId == null) return;

                var mouse = new Vector2(Event.current.mousePosition.x, Event.current.mousePosition.y);
                var panelRect = PanelLayout.PanelRect(logicalWidth, logicalHeight,
                    _world.Config.Ui.PanelWidth, _world.Config.Ui.PanelHeight);
                var layout = PanelLayout.Compute(panelRect);
                var view = BuildView(station);
                _listScroll = Mathf.Clamp(_listScroll, 0f, layout.MaxListScroll(_rows.Count));
                _manifestScroll = Mathf.Clamp(_manifestScroll, 0f, layout.MaxManifestScroll(view.Manifest.Count));
                WarnIfOverflow(layout);

                view.ListScroll = _listScroll;
                view.ManifestScroll = _manifestScroll;

                var result = _renderer!.Draw(view, layout, mouse, logicalWidth, logicalHeight);
                TrackHover(result);
                HandleInput(layout, mouse, result);
            }
            catch (Exception exception)
            {
                _error = "面板异常：" + exception.Message;
                ModRuntimeLog?.Invoke("Panel draw failed: " + exception);
            }
        }

        /// <summary>
        /// Hover should only change when the pointer crosses into another row. If it flips dozens of
        /// times per second something is remapping the mouse again; leave a breadcrumb instead of
        /// letting the next tester guess whether the symptom is still there.
        /// </summary>
        private void TrackHover(PanelRenderResult result)
        {
            var id = result.TooltipRow != null ? result.TooltipRow.InstanceId : -1;
            if (id != _hoverId)
            {
                _hoverId = id;
                _hoverChanges++;
            }
            var now = Time.unscaledTime;
            if (now - _hoverWindow < 1f) return;
            var span = Mathf.Max(0.001f, now - _hoverWindow);
            if (_hoverChanges > 20 && now - _lastHoverLog > 10f)
            {
                _lastHoverLog = now;
                ModRuntimeLog?.Invoke("Hover oscillated " + _hoverChanges + "x in " + span.ToString("0.00") + "s (id=" + id + ")");
            }
            _hoverWindow = now;
            _hoverChanges = 0;
        }

        /// <summary>
        /// If a config edit shrinks the panel below the design floor, the rail can no longer fit all
        /// three sections. The renderer clips each card, but the log should still say why content
        /// looks tight instead of leaving it a mystery.
        /// </summary>
        private void WarnIfOverflow(PanelLayout layout)
        {
            var needed = PanelLayout.RailPadding * 2f + PanelLayout.RailGap * 2f
                         + PanelLayout.CostHeight + PanelLayout.EscortHeight + PanelLayout.MinManifestHeight;
            if (layout.RailCard.height >= needed - 0.5f) return;
            if (Time.unscaledTime - _lastClampLog < 10f) return;
            _lastClampLog = Time.unscaledTime;
            ModRuntimeLog?.Invoke("Shipment panel is short for the rail: height=" + layout.RailCard.height
                                  + " needs=" + needed);
        }

        private ShipmentView BuildView(StationRecord station)
        {
            var view = _view;
            var config = _world!.Config;
            if (_hint != null && Time.unscaledTime >= _hintUntil) _hint = null;
            var available = _world.Store.Session.AvailableCount(station.MapId);
            view.StationOpen = available > 0;
            view.StatusLabel = view.StationOpen ? "营业中" : "已停业";
            view.Subtitle = MapName(station.MapId) + "　·　送达：地堡马蜂自提点　·　每站每局一次";
            view.StationIcon = _world.Visuals.Icon;
            view.Money = Duckov.Economy.EconomyManager.Money;
            view.Cash = Duckov.Economy.EconomyManager.Cash;
            view.MaxStacks = config.Limits.MaxStacks;
            view.MaxWeightKg = config.Limits.MaxWeightKg;
            view.FeeRateText = "运费 " + Percent(config.Pricing.Rate);
            view.LossCapText = "上限 " + Percent(config.Loss.MaxLostStackFraction)
                                    + "件 / " + Percent(config.Loss.MaxLostValueFraction) + "值";

            var escortRules = config.Escort(_escort);
            view.Rows.Clear();
            _manifest.Clear();
            foreach (var entry in _rows)
            {
                var row = RowView(entry, escortRules, config);
                view.Rows.Add(row);
                if (row.Selected) _manifest.Add(row);
            }
            view.Manifest.Clear();
            view.Manifest.AddRange(_manifest);

            var facts = SelectedFacts();
            var totals = ShipmentRules.Totals(facts);
            var check = ShipmentRules.Check(facts, config.Limits);
            var quote = ShipmentRules.Quote(totals.GoodsValue, _escort, config.Pricing, config.Escorts, config.Loss);
            var scavengerCount = CountCarried(config.Escorts.Scavenger.ItemId);
            var mercenaryCount = CountCarried(config.Escorts.Mercenary.ItemId);
            var noBackpack = NoBackpack();
            var paymentBlockers = ShipmentRules.PaymentBlockers(
                Duckov.Economy.EconomyManager.Money, Duckov.Economy.EconomyManager.Cash,
                EscortCarried(_escort, scavengerCount, mercenaryCount), escortRules, quote);

            view.SelectedStacks = totals.Stacks;
            view.GoodsValue = totals.GoodsValue;
            view.SelectedWeight = totals.WeightKg;
            view.Quote = quote;

            var reasons = new List<string>();
            if (noBackpack) reasons.Add("需要背包");
            if (!check.Allowed && check.Reason != null) reasons.Add(check.Reason);
            reasons.AddRange(paymentBlockers);
            if (PlayerStorage.Loading || PlayerStorage.IncomingItemBuffer == null) reasons.Add("到货缓冲区不可用");
            if (!string.IsNullOrEmpty(_error)) reasons.Add(_error!);
            var warnings = new List<string>();
            if (totals.Stacks == 1 && quote.FinalLossRate > 0) warnings.Add("单件寄送可能全损，建议请护送");
            if (totals.Stacks > 0 && quote.Fee > totals.GoodsValue) warnings.Add("运费高于货值");
            if (totals.Stacks > 0 && quote.EscortMoney > totals.GoodsValue) warnings.Add("护送费高于货值");

            view.CanShip = !_shipping && reasons.Count == 0;
            view.Shipping = _shipping;
            view.Summary = $"已选 {totals.Stacks} 件　·　实扣 {quote.TotalUpfront:N0}　·　预计总成本 {quote.ExpectedTotalCost:N0}";
            if (reasons.Count > 0)
            {
                view.StatusKind = FooterStatusKind.Danger;
                view.Status = "× " + reasons[0] + (reasons.Count > 1 ? $"（另 {reasons.Count - 1} 条）" : "");
            }
            else if (!string.IsNullOrEmpty(_hint))
            {
                // Transient feedback (blocked row, unaffordable escort, failed ship attempt).
                // It explains the last action but must not keep "确认发货" disabled.
                view.StatusKind = FooterStatusKind.Danger;
                view.Status = "× " + _hint;
            }
            else if (warnings.Count > 0)
            {
                view.StatusKind = FooterStatusKind.Warning;
                view.Status = "! " + warnings[0];
            }
            else
            {
                view.StatusKind = FooterStatusKind.Hint;
                view.Status = "选择物品后即可发货；发货后回地堡马蜂自提点领取。";
            }

            BuildEscortCards(view, config, totals.GoodsValue, scavengerCount, mercenaryCount);
            return view;
        }

        private ShipmentRowView RowView(BackpackItem entry, EscortRules escortRules, ModConfig config)
        {
            if (!_rowViews.TryGetValue(entry.Facts.InstanceId, out var row))
            {
                row = new ShipmentRowView { InstanceId = entry.Facts.InstanceId };
                _rowViews[entry.Facts.InstanceId] = row;
            }
            row.Name = entry.Facts.Name;
            row.StackCount = entry.Facts.StackCount;
            row.UnitValue = entry.Facts.UnitValue;
            row.GoodsValue = entry.Facts.GoodsValue;
            row.WeightKg = entry.Facts.TotalWeight;
            row.Quality = entry.Quality;
            row.Icon = entry.Icon;
            row.Description = entry.Description;
            var reason = ItemEligibility.BlockReason(entry.Facts, config.Protection);
            if (reason == null && _escort != EscortOption.None && entry.Facts.TypeId == escortRules.ItemId)
                reason = "护送物品不可同时寄送";
            row.BlockedReason = reason;
            row.Equipped = entry.Facts.IsEquipped;
            row.Selected = _selected.Contains(entry.Facts.InstanceId);
            return row;
        }

        private void BuildEscortCards(ShipmentView view, ModConfig config, long goodsValue,
            long scavengerCount, long mercenaryCount)
        {
            if (_escortCards.Count == 0)
            {
                _escortCards.Add(new EscortCardView { Option = EscortOption.None });
                _escortCards.Add(new EscortCardView { Option = EscortOption.Scavenger });
                _escortCards.Add(new EscortCardView { Option = EscortOption.Mercenary });
            }

            var scavenger = config.Escorts.Scavenger;
            var mercenary = config.Escorts.Mercenary;
            FillEscort(_escortCards[0], EscortOption.None, "不请护送", "免费", goodsValue, long.MaxValue, 0);
            FillEscort(_escortCards[1], EscortOption.Scavenger, scavenger.Name,
                $"{scavenger.Money:N0} + {scavenger.ItemAmount} 羽毛", goodsValue, scavengerCount, scavenger.ItemAmount);
            FillEscort(_escortCards[2], EscortOption.Mercenary, mercenary.Name,
                $"{mercenary.Money:N0} + {mercenary.ItemAmount} 狗牌", goodsValue, mercenaryCount, mercenary.ItemAmount);

            view.Escorts.Clear();
            view.Escorts.AddRange(_escortCards);
        }

        private void FillEscort(EscortCardView card, EscortOption option, string title, string cost,
            long goodsValue, long carried, long needed)
        {
            var config = _world!.Config;
            var rules = config.Escort(option);
            var quote = ShipmentRules.Quote(goodsValue, option, config.Pricing, config.Escorts, config.Loss);
            card.Option = option;
            card.Title = title;
            card.Cost = cost;
            card.Effect = "损耗 " + Percent(quote.FinalLossRate);
            card.Carried = carried;
            card.Needed = needed;
            card.Selected = _escort == option;
            var moneyOk = Duckov.Economy.EconomyManager.Money + Duckov.Economy.EconomyManager.Cash >= quote.TotalUpfront;
            var itemOk = rules.ItemId <= 0 || carried >= needed;
            // "No escort" has no extra cost to afford: only an escort that adds money or items can
            // be out of reach, otherwise the free card would grey out whenever the fee itself is short.
            card.Affordable = option == EscortOption.None || (moneyOk && itemOk);
            card.Missing = card.Affordable ? null : !itemOk ? $"随身 {carried}/{needed}" : "金钱不足";
        }

        // ------------------------------------------------------------------ input

        private void HandleInput(PanelLayout layout, Vector2 mouse, PanelRenderResult result)
        {
            var current = Event.current;
            if (current == null) return;
            switch (current.type)
            {
                case EventType.MouseDown:
                    if (current.button != 0) return;
                    _pressedHit = HitAt(result, mouse)?.Id;
                    if (layout.Panel.Contains(mouse)) current.Use();
                    return;
                case EventType.MouseUp:
                    if (current.button != 0) return;
                    var hit = HitAt(result, mouse);
                    if (hit != null && hit.Id == _pressedHit) Activate(hit, layout);
                    _pressedHit = null;
                    if (layout.Panel.Contains(mouse)) current.Use();
                    return;
                case EventType.ScrollWheel:
                    // Unity's IMGUI reports a positive delta.y for a notch rolled towards the user
                    // (the same direction the canonical `scrollPos.y += delta.y * step` snippet
                    // relies on), and a larger scroll value reveals later rows. Adding the delta is
                    // therefore the natural direction — wheel down walks down the list. The old
                    // code subtracted it, which made the list run backwards under the wheel.
                    if (layout.ListView.Contains(mouse))
                    {
                        _listScroll = Mathf.Clamp(_listScroll + Mathf.Sign(current.delta.y) * ScrollStep, 0f,
                            layout.MaxListScroll(_rows.Count));
                        current.Use();
                    }
                    else if (layout.ManifestView.Contains(mouse))
                    {
                        _manifestScroll = Mathf.Clamp(_manifestScroll + Mathf.Sign(current.delta.y) * ScrollStep, 0f,
                            layout.MaxManifestScroll(_manifest.Count));
                        current.Use();
                    }
                    return;
                case EventType.KeyDown:
                    HandleKey(current);
                    return;
            }
        }

        private void HandleKey(Event current)
        {
            if (current.keyCode == KeyCode.Escape)
            {
                Close();
                current.Use();
                return;
            }
            if ((current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) && _view.CanShip)
            {
                // Enter is a globally armed key, so it always routes through the confirmation
                // sheet instead of charging the player on a stray keystroke.
                RequestShip(true);
                current.Use();
            }
        }

        private static PanelHit? HitAt(PanelRenderResult result, Vector2 mouse)
        {
            // Last painted wins: the tooltip is not a hit target, but later cards are drawn on top.
            for (var i = result.Hits.Count - 1; i >= 0; i--)
                if (result.Hits[i].Rect.Contains(mouse)) return result.Hits[i];
            return null;
        }

        private void Activate(PanelHit hit, PanelLayout layout)
        {
            switch (hit.Kind)
            {
                case PanelHitKind.Close:
                    Close();
                    return;
                case PanelHitKind.Row:
                    ToggleRow(hit.InstanceId);
                    return;
                case PanelHitKind.ManifestRow:
                    _error = null;
                    _hint = null;
                    _selected.Remove(hit.InstanceId);
                    _manifestScroll = Mathf.Clamp(_manifestScroll, 0f, layout.MaxManifestScroll(_manifest.Count));
                    return;
                case PanelHitKind.Escort:
                    SelectEscort(hit.Escort);
                    return;
                case PanelHitKind.SelectAll:
                    SelectAllEligible();
                    return;
                case PanelHitKind.Clear:
                    _error = null;
                    _hint = null;
                    _selected.Clear();
                    _manifestScroll = 0f;
                    return;
                case PanelHitKind.Confirm:
                    RequestShip();
                    return;
            }
        }

        /// <summary>
        /// Footer feedback about the last action. It fades on its own so a single mis-click no
        /// longer leaves a red line sitting under the panel for the rest of the visit.
        /// </summary>
        private void ShowHint(string text)
        {
            _hint = text;
            _hintUntil = Time.unscaledTime + 3.5f;
        }

        private void ToggleRow(int instanceId)
        {
            var entry = _rows.Find(item => item.Facts.InstanceId == instanceId);
            if (entry == null)
            {
                ShowHint("这个物品已经不在背包或装备栏里了");
                return;
            }
            var reason = ItemEligibility.BlockReason(entry.Facts, _world!.Config.Protection);
            if (reason == null && _escort != EscortOption.None && entry.Facts.TypeId == _world.Config.Escort(_escort).ItemId)
                reason = "护送物品不可同时寄送";
            if (reason != null)
            {
                ShowHint(entry.Facts.Name + "：" + reason);
                return;
            }
            _error = null;
            _hint = null;
            if (_selected.Contains(instanceId))
            {
                _selected.Remove(instanceId);
                return;
            }
            var weightAfter = SelectedFacts().Sum(item => item.TotalWeight) + entry.Facts.TotalWeight;
            if (_selected.Count >= _world.Config.Limits.MaxStacks
                || weightAfter > _world.Config.Limits.MaxWeightKg + 0.0001)
            {
                ShowHint($"已达 {_world.Config.Limits.MaxStacks} 格 / {_world.Config.Limits.MaxWeightKg:0.#} kg 上限，先移除一些再试。");
                return;
            }
            _selected.Add(instanceId);
        }

        private void SelectEscort(EscortOption option)
        {
            var card = _escortCards.FirstOrDefault(item => item.Option == option);
            if (card != null && !card.Affordable)
            {
                ShowHint(card.Title + "：" + (card.Missing ?? "暂时无法选择"));
                return;
            }
            _error = null;
            _hint = null;
            _escort = option;
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
            _hint = null;
            _selected.Clear();
            var stacks = 0;
            var weight = 0.0;
            var truncated = false;
            foreach (var entry in _rows)
            {
                if (ItemEligibility.BlockReason(entry.Facts, _world.Config.Protection) != null) continue;
                if (_escort != EscortOption.None && entry.Facts.TypeId == _world.Config.Escort(_escort).ItemId) continue;
                if (stacks + 1 > _world.Config.Limits.MaxStacks)
                {
                    truncated = true;
                    break;
                }
                if (weight + entry.Facts.TotalWeight > _world.Config.Limits.MaxWeightKg + 0.0001)
                {
                    truncated = true;
                    continue;
                }
                _selected.Add(entry.Facts.InstanceId);
                stacks++;
                weight += entry.Facts.TotalWeight;
            }
            if (truncated)
                ShowHint($"装不下了：只选了 {stacks} 格 / {weight:0.##} kg"
                         + $"（上限 {_world.Config.Limits.MaxStacks} 格 / {_world.Config.Limits.MaxWeightKg:0.#} kg）。");
        }

        private List<ItemFacts> SelectedFacts()
        {
            var result = new List<ItemFacts>();
            foreach (var entry in _rows)
                if (_selected.Contains(entry.Facts.InstanceId)) result.Add(entry.Facts);
            return result;
        }

        private void RequestShip(bool forceConfirm = false)
        {
            if (_world == null || _shipping) return;
            var facts = SelectedFacts();
            var check = ShipmentRules.Check(facts, _world.Config.Limits);
            if (!check.Allowed || facts.Count == 0)
            {
                ShowHint(check.Reason ?? "请选择要寄送的物品");
                return;
            }
            var quote = ShipmentRules.Quote(check.Totals.GoodsValue, _escort,
                _world.Config.Pricing, _world.Config.Escorts, _world.Config.Loss);
            var safety = _world.Config.Safety;
            // The Enter key has no visible target, so it always asks first; clicking the button
            // keeps the configured thresholds.
            var needsConfirmation = forceConfirm
                                    || (safety.ConfirmAboveValue > 0 && check.Totals.GoodsValue >= safety.ConfirmAboveValue)
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
            _hint = null;
        }

        private void CloseConfirmation()
        {
            _confirmPending = false;
            _confirmTotals = null;
            _confirmQuote = null;
        }

        private void Ship()
        {
            if (_world == null || _shipping || _stationId == null) return;
            _shipping = true;
            _error = null;
            _hint = null;
            try
            {
                var result = _world.Runtime.Ship(_stationId, _selected, _escort);
                if (result.Success)
                {
                    _world.MarkStationUsed(_stationId);
                    _selected.Clear();
                    _world.Notify(result.Message, true, "菜鸭驿站 · 发货完成",
                        StationWorld.NoticeKind.Success, "回地堡马蜂自提点查看发货回执", "完成");
                    // The receipt is handed over at the bunker, not here: close the station and
                    // let StationWorld show it once the player is back (evacuated or died).
                    Close();
                }
                else
                {
                    _hint = result.Message;
                }
            }
            catch (Exception exception)
            {
                _hint = "发货失败：" + exception.Message;
                ModRuntimeLog?.Invoke("Shipment failed: " + exception);
            }
            finally
            {
                _shipping = false;
                RefreshRows();
            }
        }

        // ------------------------------------------------------------------ modal screens

        /// <summary>
        /// Delivery receipt. The goods are already in the arrival buffer by the time this sheet is
        /// shown, so there is no "claim" button here: the only honest action is to acknowledge and
        /// close. Nothing on this screen pays out, re-rolls the loss or pushes items again.
        ///
        /// All painting lives in ReceiptRenderer so the offline preview replays the exact same code;
        /// this method only feeds it state, collects the wheel, and turns the painted rects into
        /// click targets.
        /// </summary>
        private void DrawReceipt(UiTheme theme, float logicalWidth, float logicalHeight)
        {
            HandleEscape();
            if (_receiptData == null && _receiptText == null) return;
            var receipt = _receiptData;
            var lostEmpty = receipt == null || receipt.LostStacks == 0;
            var layout = ReceiptLayout.Compute(logicalWidth, logicalHeight, lostEmpty);

            if (receipt != null)
            {
                HandleTableScroll(layout.Delivered, receipt.Delivered.Count, ref _receiptDeliveredScroll);
                if (!lostEmpty) HandleTableScroll(layout.Lost, receipt.Lost.Count, ref _receiptLostScroll);
            }

            _receiptRenderer!.DrawReceipt(layout, logicalWidth, logicalHeight, receipt, _receiptText,
                _world?.Visuals.Icon, ref _receiptDeliveredScroll, ref _receiptLostScroll);

            if (GUI.Button(layout.HeaderClose, GUIContent.none, GUIStyle.none))
            {
                Close();
                return;
            }
            if (GUI.Button(layout.Button, GUIContent.none, GUIStyle.none)) Close();
        }

        /// <summary>
        /// Loss notice: a prologue to the receipt, not a dead end. Esc, ×, both buttons only hand the
        /// screen to the receipt — closing the sheet never cancels or re-runs the settlement.
        /// </summary>
        private void DrawLossNotice(UiTheme theme, float logicalWidth, float logicalHeight)
        {
            var receipt = _receiptData;
            var current = Event.current;
            if (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                current.Use();
                _lossNoticePending = false;
                return;
            }
            if (_receiptData == null && _receiptText == null) return;
            if (receipt == null || !receipt.NeedsLossNotice)
            {
                _lossNoticePending = false;
                return;
            }

            _receiptRenderer!.DrawLossNotice(receipt, logicalWidth, logicalHeight, ref _receiptLostScroll,
                out _, out var lostCard, out var closeRect, out var fullReceiptRect, out var signRect);

            HandleTableScroll(lostCard, receipt.Lost.Count, ref _receiptLostScroll);

            if (GUI.Button(closeRect, GUIContent.none, GUIStyle.none)
                || GUI.Button(fullReceiptRect, GUIContent.none, GUIStyle.none)
                || GUI.Button(signRect, GUIContent.none, GUIStyle.none))
            {
                _lossNoticePending = false;
            }
        }

        /// <summary>
        /// Wheel scrolling for one goods table. The hit area is the table's scrolling window, so a
        /// wheel turn over either table only moves that table.
        /// </summary>
        private void HandleTableScroll(Rect card, int count, ref float scroll)
        {
            var view = ReceiptLayout.ListView(card);
            var maxScroll = Mathf.Max(0f, count * ReceiptLayout.ListRowHeight - view.height);
            scroll = Mathf.Clamp(scroll, 0f, maxScroll);
            if (maxScroll <= 0f) return;
            var current = Event.current;
            if (current == null || current.type != EventType.ScrollWheel) return;
            if (!view.Contains(current.mousePosition)) return;
            scroll = Mathf.Clamp(scroll - Mathf.Sign(current.delta.y) * ReceiptLayout.ListRowHeight, 0f, maxScroll);
            current.Use();
        }

        /// <summary>Native item icon for a receipt line. Never instantiates an item to get it.</summary>
        private Sprite? ItemIcon(int typeId)
        {
            if (typeId <= 0) return null;
            try
            {
                var meta = ItemAssetsCollection.GetMetaData(typeId);
                if (meta.icon != null) return meta.icon;
            }
            catch
            {
                // Fall through to the logged placeholder below.
            }
            if (_iconlessTypeIds.Add(typeId))
                ModRuntimeLog?.Invoke("Receipt icon missing for typeId=" + typeId + "; using placeholder tile.");
            return null;
        }

        /// <summary>
        /// Localised name for a receipt material. The stored display name is a localisation key
        /// (e.g. Item_BatteryR6), so it is resolved through the game's own table; a missing key
        /// keeps the raw key, which still identifies the item.
        /// </summary>
        private string ItemName(int typeId)
        {
            if (typeId <= 0) return "物品";
            try
            {
                var name = ItemAssetsCollection.GetMetaData(typeId).DisplayName;
                if (string.IsNullOrWhiteSpace(name)) return "物品 " + typeId;
                try
                {
                    var plain = SodaCraft.Localizations.LocalizationManager.ToPlainText(name);
                    if (!string.IsNullOrWhiteSpace(plain)) return plain;
                }
                catch
                {
                    // Localisation table unavailable: the raw key still identifies the item.
                }
                return name;
            }
            catch
            {
                return "物品 " + typeId;
            }
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
            GUI.Label(new Rect(panel.x + 20, panel.y + 40, panel.width - 40, 22),
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
            var keyWidth = 220f;
            var valueWidth = 220f;
            GUI.Label(new Rect(panel.x + 20, y, keyWidth, 22), key, theme.Small);
            GUI.Label(new Rect(panel.xMax - 20 - valueWidth, y, valueWidth, 22),
                UiTheme.Fit(theme.RowName, value, valueWidth), theme.RowName);
            y += 24;
        }

        // ------------------------------------------------------------------ helpers

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

        private bool NoBackpack()
        {
            try
            {
                var backpack = CharacterMainControl.Main?.CharacterItem?.Inventory;
                return backpack == null || backpack.Capacity <= 0;
            }
            catch
            {
                return true;
            }
        }

        private static string Percent(double value) => (value * 100).ToString("0.#") + "%";

        private static string MapName(string mapId)
        {
            switch (mapId)
            {
                case "Level_HiddenWarehouse_Main": return "仓库区";
                case "Level_Farm_Main": return "农场镇";
            }
            // Modded or future maps: show something readable instead of the raw Level_ id.
            var name = mapId.StartsWith("Level_", StringComparison.Ordinal) ? mapId.Substring(6) : mapId;
            if (name.EndsWith("_Main", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 5);
            name = name.Replace('_', ' ').Trim();
            return string.IsNullOrEmpty(name) ? "未知区域" : name;
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
            if (!Cursor.visible) Cursor.visible = true;
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
            catch
            {
                // Cursor restore is best-effort; the game re-applies its own state on focus.
            }
        }

        public void Dispose()
        {
            RestoreCursor();
            PanelInputGuard.Set(false);
            _painter?.Dispose();
            _painter = null;
            _renderer = null;
            _receiptRenderer = null;
            _theme?.Dispose();
            _theme = null;
        }
    }
}
