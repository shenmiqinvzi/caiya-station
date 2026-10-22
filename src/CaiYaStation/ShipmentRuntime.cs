using System;
using System.Collections.Generic;
using System.Linq;
using CaiYaStation.Domain;
using Duckov.Economy;
using ItemStatsSystem;
using UnityEngine;
using Random = System.Random;

namespace CaiYaStation
{
    internal sealed class BackpackItem
    {
        internal Item Item = null!;
        internal ItemFacts Facts = null!;
        internal string Description = "";
        internal Sprite? Icon;
        internal int Quality;
    }

    internal sealed class ShipmentResult
    {
        internal bool Success { get; set; }
        internal string Message { get; set; } = "";
        internal string? ReceiptText { get; set; }
        internal CaiYaStation.Domain.Receipt? ReceiptData { get; set; }
        internal bool StationUsed { get; set; }
    }

    /// <summary>
    /// Bridges the pure domain rules to native item/economy/storage APIs. Every mutation is preceded
    /// by a re-validation against the live backpack, and goods are never re-pushed twice.
    /// </summary>
    internal sealed class ShipmentRuntime
    {
        private readonly ModConfig _config;
        private readonly StateStore _store;
        private readonly Action<string> _log;
        private Random _random = new Random();

        internal ShipmentRuntime(ModConfig config, StateStore store, Action<string> log)
        {
            _config = config;
            _store = store;
            _log = log;
        }

        internal StateStore Store => _store;

        internal IReadOnlyList<BackpackItem> ReadBackpack()
        {
            var result = new List<BackpackItem>();
            try
            {
                var actor = CharacterMainControl.Main;
                var inventory = actor != null && actor.CharacterItem != null ? actor.CharacterItem.Inventory : null;
                if (inventory == null) return result;
                foreach (var item in inventory.Content)
                {
                    if (item == null) continue;
                    var entry = new BackpackItem { Item = item, Facts = Describe(item) };
                    try { entry.Description = item.Description ?? ""; } catch { entry.Description = ""; }
                    try { entry.Icon = item.Icon; } catch { entry.Icon = null; }
                    try { entry.Quality = item.Quality; } catch { entry.Quality = 0; }
                    result.Add(entry);
                }
            }
            catch (Exception exception)
            {
                _log("Backpack read failed: " + exception.Message);
            }
            return result;
        }

        internal ItemFacts Describe(Item item)
        {
            var typeId = item.TypeID;
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (item.Tags != null && item.Tags.list != null)
                {
                    foreach (var tag in item.Tags.list)
                    {
                        if (tag == null) continue;
                        if (!string.IsNullOrWhiteSpace(tag.name)) tags.Add(tag.name);
                        var display = tag.DisplayName;
                        if (!string.IsNullOrWhiteSpace(display)) tags.Add(display);
                    }
                }
            }
            catch (Exception exception)
            {
                _log("Tag read failed for type " + typeId + ": " + exception.Message);
            }
            try
            {
                var meta = ItemAssetsCollection.GetMetaData(typeId);
                if (meta.tags != null)
                {
                    foreach (var tag in meta.tags)
                    {
                        if (tag == null) continue;
                        if (!string.IsNullOrWhiteSpace(tag.name)) tags.Add(tag.name);
                        var display = tag.DisplayName;
                        if (!string.IsNullOrWhiteSpace(display)) tags.Add(display);
                    }
                }
            }
            catch (Exception exception)
            {
                _log("Item metadata read failed for type " + typeId + ": " + exception.Message);
            }

            var inventory = item.Inventory;
            var name = item.DisplayName;
            if (string.IsNullOrWhiteSpace(name))
            {
                try { name = ItemAssetsCollection.GetMetaData(typeId).DisplayName; }
                catch { name = "物品 " + typeId; }
            }
            if (string.IsNullOrWhiteSpace(name)) name = "物品 " + typeId;
            return new ItemFacts
            {
                InstanceId = item.GetInstanceID(),
                TypeId = typeId,
                Name = name,
                StackCount = item.StackCount,
                UnitValue = item.Value,
                TotalWeight = item.TotalWeight,
                IsEquipped = SafeIsEquipped(item),
                CanDrop = item.CanDrop,
                HasContainerInventory = inventory != null && inventory.Capacity > 0,
                Tags = tags.ToList()
            };
        }

        private static bool SafeIsEquipped(Item item)
        {
            try { return item.IsInCharacterSlot(); }
            catch { return false; }
        }

        internal long EscortItemCount(int typeId)
        {
            long count = 0;
            foreach (var entry in ReadBackpack())
                if (entry.Facts.TypeId == typeId) count += entry.Facts.StackCount;
            return count;
        }

        internal ShipmentResult Ship(
            string stationId,
            ICollection<int> selectedInstanceIds,
            EscortOption escort)
        {
            var station = _store.Session.Find(stationId);
            if (station == null) return Fail("找不到这个驿站，请重新靠近后再试。");
            if (station.Used) return Fail("这个驿站本局已经发过货了。");
            if (!_store.Session.Active) return Fail("当前不是有效的 raid，无法发货。");
            if (!string.Equals(station.MapId, CurrentMapId(), StringComparison.Ordinal))
                return Fail("驿站不在当前地图，无法发货。");
            if (selectedInstanceIds.Count == 0) return Fail("请选择要寄送的物品。");

            var backpack = ReadBackpack();
            var byInstance = backpack.ToDictionary(entry => entry.Facts.InstanceId);
            var selected = new List<BackpackItem>();
            foreach (var instanceId in selectedInstanceIds)
            {
                if (!byInstance.TryGetValue(instanceId, out var entry))
                    return Fail("有物品已经不在背包里，请刷新后重试。");
                var reason = ItemEligibility.BlockReason(entry.Facts, _config.Protection);
                if (reason != null) return Fail(entry.Facts.Name + "：" + reason);
                selected.Add(entry);
            }
            var facts = selected.Select(entry => entry.Facts).ToList();
            var check = ShipmentRules.Check(facts, _config.Limits);
            if (!check.Allowed) return Fail(check.Reason ?? "选择不合法");

            if (PlayerStorage.Loading || PlayerStorage.IncomingItemBuffer == null)
                return Fail("到货缓冲区暂不可用，请稍后再试（未扣费）。");

            var escortRules = _config.Escort(escort);
            if (escortRules.ItemId > 0 && facts.Any(item => item.TypeId == escortRules.ItemId))
                return Fail("护送所需物品不能同时寄送，请取消选择或更换护送方式。");
            var quote = ShipmentRules.Quote(check.Totals.GoodsValue, escort, _config.Pricing, _config.Escorts, _config.Loss);
            var escortCount = EscortItemCount(escortRules.ItemId);
            var blockers = ShipmentRules.PaymentBlockers(EconomyManager.Money, EconomyManager.Cash, escortCount, escortRules, quote);
            if (blockers.Count > 0) return Fail(string.Join("；", blockers));

            var plan = Shipments.Prepare(facts, escort, _config.Pricing, _config.Escorts, _config.Loss, _random);
            var shipmentId = _store.NextShipmentId();
            var journal = Shipments.Journal(shipmentId, stationId, station.MapId, facts, plan,
                DateTimeOffset.Now, station.Name, Receipts.RandomFlavor(_random));
            journal.RaidToken = _store.Session.Token;
            journal.Stage = ShipmentStage.Prepared;
            _store.SetPending(journal);
            _store.SaveAllAndFlush();
            _log("Shipment prepared " + shipmentId + " station=" + stationId + " items=" + facts.Count
                 + " value=" + quote.GoodsValue + " fee=" + quote.Fee + " escort=" + escortRules.Name
                 + " loss=" + quote.FinalLossRate.ToString("0.###"));

            var itemCosts = escortRules.ItemId > 0 && escortRules.ItemAmount > 0
                ? new[] { (escortRules.ItemId, escortRules.ItemAmount) }
                : Array.Empty<(int, long)>();
            var cost = new Cost(quote.TotalUpfront, itemCosts);
            if (!EconomyManager.IsEnough(cost, true, true))
            {
                AbortPending(journal, "支付校验失败：金钱/现金或护送物品不足");
                return Fail("支付校验失败，未扣费，也未移动物品。");
            }
            if (!EconomyManager.Pay(cost, true, true))
            {
                AbortPending(journal, "原生支付返回失败");
                return Fail("支付失败，未扣费，也未移动物品。");
            }
            journal.Stage = ShipmentStage.Paid;
            _store.SetPending(journal);
            _store.SaveAllAndFlush();
            // Mark used only after the Paid journal is on disk: a crash before this point recovers
            // the paid shipment, while a crash after it must not reopen the station.
            _store.Session.MarkUsed(stationId);
            _store.SaveStations();
            _store.SaveState();
            _log("Shipment paid " + shipmentId + " cost=" + quote.TotalUpfront
                 + " escortItem=" + escortRules.ItemId + "x" + escortRules.ItemAmount);

            try
            {
                var inventory = CharacterMainControl.Main?.CharacterItem?.Inventory;
                if (inventory == null) throw new InvalidOperationException("背包不可用");
                foreach (var line in journal.Items)
                {
                    var live = FindLive(inventory, line.InstanceId);
                    if (live == null)
                    {
                        _log("Shipment item missing during delivery id=" + line.InstanceId + " name=" + line.Name);
                        // Never claim delivery for an item we could not push. Record it as lost
                        // so the receipt stays consistent and the player is not lied to.
                        if (!line.Lost) line.Lost = true;
                        continue;
                    }
                    if (line.Lost)
                    {
                        RemoveAndDestroy(inventory, live);
                        _log("Shipment lost item " + line.Name + " x" + line.StackCount);
                    }
                    else
                    {
                        PlayerStorage.Push(live, true);
                        line.Pushed = true;
                        _log("Shipment delivered item " + line.Name + " x" + line.StackCount);
                    }
                }

                _store.Session.MarkUsed(stationId);
                _store.SaveStations();
                journal.Stage = ShipmentStage.Completed;
                var receipt = BuildReceipt(journal);
                _store.AddReceipt(receipt);
                _store.ClearPending();
                _store.SaveAllAndFlush();
                _log("Shipment completed " + shipmentId + " delivered=" + receipt.DeliveredStacks
                     + " lost=" + receipt.LostStacks);
                return new ShipmentResult
                {
                    Success = true,
                    StationUsed = true,
                    ReceiptText = Receipts.Render(receipt),
                    ReceiptData = receipt,
                    Message = "已发货 " + receipt.DeliveredStacks + " 件，丢失 " + receipt.LostStacks
                              + " 件；请回地堡马蜂自提点领取。"
                };
            }
            catch (Exception exception)
            {
                journal.Stage = ShipmentStage.Interrupted;
                journal.Message = exception.Message;
                _store.SetPending(journal);
                _store.SaveAllAndFlush();
                _log("Shipment interrupted " + shipmentId + ": " + exception);
                return Fail("发货过程中断，已扣费。重新加载后会继续把未送达的幸存物品送到自提点，不会重复扣费。");
            }
        }

        /// <summary>Finishes a paid shipment left behind by a crash or forced exit. Never charges again.</summary>
        internal string? RecoverPending()
        {
            var journal = _store.Pending;
            if (journal == null) return null;
            if (ShipmentJournalRules.IsTerminal(journal.Stage))
            {
                _store.ClearPending();
                _store.SaveAllAndFlush();
                return null;
            }
            if (!ShipmentJournalRules.NeedsRecovery(journal.Stage))
            {
                if (journal.Stage == ShipmentStage.Prepared)
                {
                    // Crashed after the write-ahead record but before payment: nothing was charged or moved.
                    _log("Discarding unprepared shipment " + journal.ShipmentId);
                    _store.ClearPending();
                    _store.SaveAllAndFlush();
                }
                return null;
            }
            try
            {
                var inventory = CharacterMainControl.Main?.CharacterItem?.Inventory;
                if (inventory == null)
                {
                    _log("Shipment recovery deferred: backpack unavailable");
                    return null;
                }
                var claimed = new HashSet<int>();
                foreach (var line in journal.Items.Where(line => !line.Lost && !line.Pushed))
                {
                    // Unity instance ids are not stable across process restarts. Fall back to a
                    // one-to-one fingerprint (type / stack / unit value) for a paid shipment
                    // that was interrupted before its push completed.
                    var live = FindLive(inventory, line.InstanceId);
                    if (live == null) live = FindLiveByFingerprint(inventory, line, claimed);
                    if (live == null)
                    {
                        _log("Recovery could not locate item id=" + line.InstanceId
                             + " name=" + line.Name + " x" + line.StackCount);
                        continue;
                    }
                    PlayerStorage.Push(live, true);
                    line.Pushed = true;
                    _log("Recovered delivery " + line.Name + " x" + line.StackCount);
                }
                var unresolved = journal.Items.Count(line => !line.Lost && !line.Pushed);
                if (unresolved > 0)
                {
                    journal.Stage = ShipmentStage.Interrupted;
                    journal.Message = "恢复时仍有 " + unresolved + " 件未在背包中找到";
                    _store.SetPending(journal);
                    _store.SaveAllAndFlush();
                    _log("Shipment recovery incomplete " + journal.ShipmentId + " unresolved=" + unresolved);
                    return "菜鸭驿站：上次发货仍有 " + unresolved
                           + " 件未能确认送达，已保留发货记录。请查看 F8 日志。";
                }
                var station = _store.Session.Find(journal.StationId);
                if (station != null && string.Equals(journal.RaidToken, _store.Session.Token, StringComparison.Ordinal))
                {
                    _store.Session.MarkUsed(journal.StationId);
                    _store.SaveStations();
                }
                journal.Stage = ShipmentStage.Completed;
                journal.Message = "启动时恢复";
                var receipt = BuildReceipt(journal);
                _store.AddReceipt(receipt);
                _store.ClearPending();
                _store.SaveAllAndFlush();
                _log("Recovered shipment " + journal.ShipmentId + " delivered=" + receipt.DeliveredStacks
                     + " lost=" + receipt.LostStacks);
                return "菜鸭驿站：上次发货已恢复，送达 " + receipt.DeliveredStacks
                       + " 件、丢失 " + receipt.LostStacks + " 件。请到马蜂自提点领取。";
            }
            catch (Exception exception)
            {
                _log("Shipment recovery failed: " + exception);
                return null;
            }
        }

        internal void AbortPending(ShipmentJournal journal, string reason)
        {
            journal.Stage = ShipmentStage.Aborted;
            journal.Message = reason;
            _store.SetPending(journal);
            _store.SaveState();
            _store.ClearPending();
            _store.SaveAllAndFlush();
            _log("Shipment aborted " + journal.ShipmentId + ": " + reason);
        }

        private static Item? FindLive(Inventory inventory, int instanceId)
        {
            foreach (var item in inventory.Content)
                if (item != null && item.GetInstanceID() == instanceId) return item;
            return null;
        }

        /// <summary>
        /// Unity instance ids change when the game process reloads. A paid shipment that was
        /// interrupted before pushing can still be matched to the same stack by its type,
        /// stack count and unit value; each live item is claimed only once.
        /// </summary>
        private static Item? FindLiveByFingerprint(Inventory inventory, JournalItem line, HashSet<int> claimed)
        {
            foreach (var item in inventory.Content)
            {
                if (item == null) continue;
                var instanceId = item.GetInstanceID();
                if (claimed.Contains(instanceId)) continue;
                if (item.TypeID != line.TypeId) continue;
                if (item.StackCount != line.StackCount) continue;
                if (item.Value != line.UnitValue) continue;
                claimed.Add(instanceId);
                return item;
            }
            return null;
        }

        private static void RemoveAndDestroy(Inventory inventory, Item item)
        {
            inventory.RemoveItem(item);
            ItemTreeExtensions.DestroyTree(item);
        }

        private Receipt BuildReceipt(ShipmentJournal journal)
        {
            var receipt = new Receipt
            {
                ShipmentId = journal.ShipmentId,
                CreatedUtc = journal.CreatedUtc,
                StationId = journal.StationId,
                StationName = journal.StationName,
                MapId = journal.MapId,
                GoodsValue = journal.GoodsValue,
                Fee = journal.Fee,
                EscortMoney = journal.EscortMoney,
                EscortName = journal.EscortName,
                LossRate = journal.LossRate,
                Flavor = journal.Flavor
            };
            if (journal.Items.Any(line => line.Lost))
            {
                var notice = Receipts.RandomLossNotice(_random);
                receipt.LossCulprits = notice.Culprits;
                receipt.LossKaomoji = notice.Kaomoji;
            }
            foreach (var line in journal.Items)
            {
                var entry = new ReceiptLine
                {
                    TypeId = line.TypeId,
                    Name = line.Name,
                    StackCount = line.StackCount,
                    UnitValue = line.UnitValue
                };
                if (line.Lost) receipt.Lost.Add(entry);
                else receipt.Delivered.Add(entry);
            }
            return receipt;
        }

        private static string CurrentMapId()
        {
            try { return Duckov.Scenes.MultiSceneCore.MainSceneID ?? ""; }
            catch { return ""; }
        }

        private static ShipmentResult Fail(string message) => new ShipmentResult { Success = false, Message = message };
    }
}
