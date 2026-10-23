using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CaiYaStation.Domain;
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
        private GUIStyle? _badgeDanger;
        private GUIStyle? _receiptHeadline;

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
                    if (layout.ListView.Contains(mouse))
                    {
                        _listScroll = Mathf.Clamp(_listScroll - Mathf.Sign(current.delta.y) * 44f, 0f,
                            layout.MaxListScroll(_rows.Count));
                        current.Use();
                    }
                    else if (layout.ManifestView.Contains(mouse))
                    {
                        _manifestScroll = Mathf.Clamp(_manifestScroll - Mathf.Sign(current.delta.y) * 44f, 0f,
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
                ShowHint("这个物品已经不在背包里了");
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

        private void DrawReceipt(UiTheme theme, float logicalWidth, float logicalHeight)
        {
            HandleEscape();
            if (_receiptData == null && _receiptText == null) return;
            theme.DrawDim(logicalWidth, logicalHeight);
            var layout = ReceiptLayout.Compute(logicalWidth, logicalHeight);
            var panel = layout.Panel;
            theme.DrawPanel(panel);
            var receipt = _receiptData;

            theme.DrawHeader(layout.Header);
            theme.DrawSolid(layout.HeaderAccent, theme.Accent);
            GUI.Label(layout.HeaderTitle, "发货回执", theme.Title);
            theme.DrawPill(layout.HeaderBadge);
            GUI.Label(layout.HeaderBadge, receipt == null ? "已完成" : receipt.DeliveredStacks > 0 ? "已送达" : "全部丢失",
                receipt == null || receipt.DeliveredStacks > 0 ? theme.Badge : _badgeDanger!);
            GUI.Label(layout.HeaderSubtitle,
                "菜鸭驿站 → 地堡马蜂自提点　·　货物已写入到货缓冲区", theme.Subtitle);
            if (GUI.Button(layout.HeaderClose, "×", theme.CloseButton))
            {
                Close();
                return;
            }

            if (receipt == null)
            {
                GUI.Label(new Rect(panel.x + 24, panel.y + 90, panel.width - 48, panel.height - 150),
                    _receiptText ?? "", theme.Label);
                if (GUI.Button(layout.Button, "关闭", theme.Button)) Close();
                return;
            }

            var escortName = string.IsNullOrWhiteSpace(receipt.EscortName) ? "无" : receipt.EscortName;
            DrawChip(theme, layout.ChipOne, "送达",
                receipt.DeliveredStacks + " 件", "价值 " + receipt.DeliveredValue.ToString("N0"), theme.Good);
            DrawChip(theme, layout.ChipTwo, "丢失",
                receipt.LostStacks + " 件", "价值 " + receipt.LostValue.ToString("N0"),
                receipt.LostStacks > 0 ? theme.Danger : theme.TextDim);
            DrawChip(theme, layout.ChipThree, "运费", receipt.Fee.ToString("N0"),
                receipt.EscortMoney > 0 ? "护送 " + receipt.EscortMoney.ToString("N0") : "无护送", theme.Text);
            DrawChip(theme, layout.ChipFour, "护送", escortName,
                "损耗 " + Percent(receipt.LossRate), theme.Accent);

            DrawReceiptList(theme, layout.Delivered, "已送达", receipt.Delivered, theme.Good);
            DrawReceiptList(theme, layout.Lost, "丢失", receipt.Lost, theme.Danger);

            var lost = receipt.LostStacks > 0;
            var culprits = string.IsNullOrWhiteSpace(receipt.LossCulprits) ? Receipts.FallbackCulprits : receipt.LossCulprits;
            var kaomoji = lost
                ? (string.IsNullOrWhiteSpace(receipt.LossKaomoji) ? Receipts.FallbackLossKaomoji : receipt.LossKaomoji)
                : Receipts.DeliveredKaomoji;
            var headline = lost
                ? "我们的人被" + culprits + "暴打了，货也弄丢了…" + kaomoji
                : "整批货安全入库，清点无误。" + kaomoji;
            if (_receiptHeadline == null)
                _receiptHeadline = new GUIStyle(theme.Label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _receiptHeadline.normal.textColor = lost ? theme.Danger : theme.Good;
            GUI.Label(layout.Headline, headline, _receiptHeadline);
            if (!string.IsNullOrWhiteSpace(receipt.Flavor))
                GUI.Label(layout.Flavor, receipt.Flavor, theme.Small);

            if (GUI.Button(layout.Button, "关闭", theme.Button)) Close();
        }

        private static void DrawChip(UiTheme theme, Rect rect, string label, string value, string sub, Color valueColor)
        {
            theme.DrawCard(rect, false);
            GUI.Label(new Rect(rect.x + 10, rect.y + ReceiptLayout.ChipLabelTop, rect.width - 20, 16), label, theme.Small);
            var previous = GUI.color;
            GUI.color = valueColor;
            GUI.Label(new Rect(rect.x + 10, rect.y + ReceiptLayout.ChipValueTop, rect.width - 20, 24), value, theme.RowValueBold);
            GUI.color = previous;
            GUI.Label(new Rect(rect.x + 10, rect.y + ReceiptLayout.ChipSubTop, rect.width - 20, 16), sub, theme.Small);
        }

        private static void DrawReceiptList(UiTheme theme, Rect area, string title, List<ReceiptLine> lines, Color accent)
        {
            theme.DrawCard(area, false);
            theme.DrawSolid(new Rect(area.x, area.y + 10, 3, 22), accent);
            GUI.Label(new Rect(area.x + 12, area.y + 8, area.width - 24, 24), title + "　" + lines.Count + " 件", theme.Section);
            var y = area.y + ReceiptLayout.ListTitleHeight;
            var capacity = ReceiptLayout.ListCapacity(area.height);
            var shown = Mathf.Min(lines.Count, capacity);
            for (var index = 0; index < shown; index++)
            {
                var line = lines[index];
                GUI.Label(new Rect(area.x + 12, y, area.width - 116, 20),
                    line.Name + (line.StackCount > 1 ? " ×" + line.StackCount : ""), theme.RowName);
                GUI.Label(new Rect(area.xMax - 100, y, 88, 20), line.GoodsValue.ToString("N0"), theme.RowValue);
                y += ReceiptLayout.ListRowHeight;
            }
            if (lines.Count == 0)
            {
                GUI.Label(new Rect(area.x + 12, y, area.width - 24, 20), "无", theme.Small);
            }
            else if (shown < lines.Count)
            {
                GUI.Label(new Rect(area.x + 12, area.yMax - ReceiptLayout.ListFooterHeight, area.width - 24, 18),
                    "另有 " + (lines.Count - shown) + " 条未显示（见回执历史）", theme.Small);
            }
        }

        /// <summary>
        /// Loss sheet: culprits line, lost stacks, total value, sign button. Laid out top-down from
        /// measured heights, so the note line and the buttons can no longer land on each other.
        /// </summary>
        private void DrawLossNotice(UiTheme theme, float logicalWidth, float logicalHeight)
        {
            var receipt = _receiptData;
            // The sheet is a prologue to the receipt, not a dead end: both Esc and × hand the
            // screen over to the receipt instead of closing the whole panel.
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

            var culprits = string.IsNullOrWhiteSpace(receipt.LossCulprits) ? Receipts.FallbackCulprits : receipt.LossCulprits;
            var kaomoji = string.IsNullOrWhiteSpace(receipt.LossKaomoji) ? Receipts.FallbackLossKaomoji : receipt.LossKaomoji;
            var culpritsStyle = new GUIStyle(theme.Label) { fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = true };
            culpritsStyle.normal.textColor = new Color(1f, 0.66f, 0.60f, 1f);
            var culpritsText = $"我们的人被{culprits}暴打了，货也弄丢了…{kaomoji}";

            var width = Mathf.Min(580, logicalWidth - 40);
            var textHeight = culpritsStyle.CalcHeight(new GUIContent(culpritsText), width - 48f);
            var listHeight = Mathf.Clamp(receipt.Lost.Count * 26f + 44f, 74f, 190f);
            var height = 84f + textHeight + 10f + listHeight + 10f + 30f + 6f + 18f + 12f + 38f + 14f;
            if (height > logicalHeight - 24f)
            {
                listHeight = Mathf.Max(52f, listHeight - (height - (logicalHeight - 24f)));
                height = 84f + textHeight + 10f + listHeight + 10f + 30f + 6f + 18f + 12f + 38f + 14f;
            }
            var top = Mathf.Max(12f, (logicalHeight - height) * 0.5f);
            var panel = new Rect((logicalWidth - width) * 0.5f, top, width, Mathf.Min(height, logicalHeight - 12f));

            theme.DrawDim(logicalWidth, logicalHeight);
            theme.DrawPanel(panel);
            theme.DrawHeader(new Rect(panel.x, panel.y, panel.width, 64));
            theme.DrawSolid(new Rect(panel.x, panel.y, 4, 64), theme.Danger);
            GUI.Label(new Rect(panel.x + 20, panel.y + 8, 340, 32), "运送损失通知单", theme.Title);
            var badge = new Rect(panel.x + 196, panel.y + 16, 92, 24);
            theme.DrawPill(badge);
            GUI.Label(badge, "丢件确认", _badgeDanger!);
            GUI.Label(new Rect(panel.x + 20, panel.y + 40, panel.width - 120, 18),
                "菜鸭驿站 · 配送事故备案　·　签收后不再追偿（雾）", theme.Subtitle);
            if (GUI.Button(new Rect(panel.xMax - 54, panel.y + 14, 36, 34), "×", theme.CloseButton))
            {
                _lossNoticePending = false;
                return;
            }

            var cursor = panel.y + 80f;
            GUI.Label(new Rect(panel.x + 20, cursor, panel.width - 40, textHeight), culpritsText, culpritsStyle);
            cursor += textHeight + 10f;

            theme.DrawCard(new Rect(panel.x + 20, cursor, panel.width - 40, listHeight), false);
            theme.DrawSolid(new Rect(panel.x + 20, cursor + 10, 3, 22), theme.Danger);
            GUI.Label(new Rect(panel.x + 34, cursor + 8, panel.width - 80, 24),
                "丢失物品　" + receipt.Lost.Count + " 件", theme.Section);
            var rowY = cursor + 36f;
            foreach (var line in receipt.Lost)
            {
                if (rowY + 24 > cursor + listHeight - 6) break;
                GUI.Label(new Rect(panel.x + 34, rowY, panel.width - 170, 20),
                    line.Name + (line.StackCount > 1 ? " ×" + line.StackCount : ""), theme.RowName);
                GUI.Label(new Rect(panel.xMax - 140, rowY, 118, 20), "-" + line.GoodsValue.ToString("N0"), theme.SmallRight);
                rowY += 26f;
            }
            cursor += listHeight + 10f;

            GUI.Label(new Rect(panel.x + 20, cursor, 240, 30), "总计损失价值", theme.Label);
            var lossStyle = new GUIStyle(theme.BigValue) { alignment = TextAnchor.MiddleRight };
            lossStyle.normal.textColor = theme.Danger;
            GUI.Label(new Rect(panel.xMax - 240, cursor - 2, 220, 32), "-" + receipt.LostValue.ToString("N0"), lossStyle);
            cursor += 36f;

            GUI.Label(new Rect(panel.x + 20, cursor, panel.width - 40, 18),
                $"本次送达 {receipt.DeliveredStacks} 件 / 价值 {receipt.DeliveredValue:N0}　·　已扣运费 {receipt.Fee:N0}",
                theme.Small);
            cursor += 24f;

            if (GUI.Button(new Rect(panel.x + 20, cursor, 150, 38), "直接看回执", theme.Button))
                _lossNoticePending = false;
            if (GUI.Button(new Rect(panel.xMax - 220, cursor, 200, 38), "签收（确认损失）", theme.ButtonPrimary))
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
            _theme?.Dispose();
            _theme = null;
        }
    }
}
