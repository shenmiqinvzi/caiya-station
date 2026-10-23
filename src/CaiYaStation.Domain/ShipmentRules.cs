using System;
using System.Collections.Generic;
using System.Linq;

namespace CaiYaStation.Domain
{
    public sealed class ProtectionRules
    {
        public HashSet<string> ProtectedTags { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Quest", "Key", "SpecialKey", "Cash", "Continer", "Backpack", "Character"
        };

        public HashSet<string> ContainerTags { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Continer", "Backpack"
        };

        public HashSet<string> CurrencyTags { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Cash"
        };

        public List<string> ProtectedNamePrefixes { get; set; } = new List<string>
        {
            "Quest_", "Item_Quest_", "Key_", "KeyCard_", "Item_Key", "DeliverBox"
        };

        public HashSet<int> ProtectedTypeIds { get; set; } = new HashSet<int> { 451 };
    }

    public static class ItemEligibility
    {
        /// <summary>
        /// Returns null when the stack may be shipped, otherwise a player-facing reason.
        /// The same rule must run in the panel and again in the settlement path.
        /// </summary>
        public static string? BlockReason(ItemFacts item, ProtectionRules rules)
        {
            // Fail closed: an unreadable tag list may be hiding Quest/Key/Cash/Container tags,
            // so the stack is refused instead of silently slipping past protection.
            if (item.ProtectionCheckFailed) return "物品信息读取失败，为保护任务/钥匙类物品已禁止寄送";
            if (item.StackCount <= 0) return "数量异常，不能寄送";
            if (item.IsEquipped) return "装备中的物品不可寄送";
            if (rules.ProtectedTypeIds.Contains(item.TypeId)) return "现金/货币类物品不可寄送";
            if (item.HasContainerInventory) return "容器类物品不可寄送";
            if (item.Tags.Any(tag => rules.ContainerTags.Contains(tag))) return "容器类物品不可寄送";
            if (item.Tags.Any(tag => rules.CurrencyTags.Contains(tag))) return "现金/货币类物品不可寄送";
            if (item.Tags.Any(tag => rules.ProtectedTags.Contains(tag))) return "任务/钥匙/唯一物品不可寄送";
            foreach (var prefix in rules.ProtectedNamePrefixes)
            {
                if (item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return "任务/钥匙/唯一物品不可寄送";
            }
            if (!item.CanDrop) return "该物品不可丢弃，不能寄送";
            return null;
        }

        public static bool IsEligible(ItemFacts item, ProtectionRules rules) => BlockReason(item, rules) == null;
    }

    public sealed class ShipmentLimits
    {
        public int MaxStacks { get; set; } = 12;
        public double MaxWeightKg { get; set; } = 25.0;
    }

    public sealed class SelectionCheck
    {
        public bool Allowed { get; set; }
        public string? Reason { get; set; }
        public SelectionTotals Totals { get; set; } = new SelectionTotals();
    }

    public static class ShipmentRules
    {
        public static SelectionTotals Totals(IEnumerable<ItemFacts> selected)
        {
            var totals = new SelectionTotals();
            foreach (var item in selected)
            {
                totals.Stacks++;
                totals.GoodsValue += item.GoodsValue;
                totals.WeightKg += Math.Max(0, item.TotalWeight);
            }
            return totals;
        }

        public static SelectionCheck Check(IReadOnlyList<ItemFacts> selected, ShipmentLimits limits)
        {
            var totals = Totals(selected);
            if (totals.Stacks <= 0)
                return new SelectionCheck { Reason = "请选择要寄送的物品", Totals = totals };
            if (totals.Stacks > limits.MaxStacks)
                return new SelectionCheck { Reason = $"超出 {limits.MaxStacks} 格上限", Totals = totals };
            if (totals.WeightKg > limits.MaxWeightKg + 0.0001)
                return new SelectionCheck { Reason = $"超出 {limits.MaxWeightKg:0.#} kg 上限", Totals = totals };
            return new SelectionCheck { Allowed = true, Totals = totals };
        }

        public static FeeQuote Quote(
            long goodsValue,
            EscortOption escort,
            PricingRules pricing,
            EscortSettings escorts,
            LossSettings loss)
        {
            var fee = Pricing.Fee(goodsValue, pricing);
            var baseRate = LossModel.BaseRate(goodsValue, loss);
            var rules = escorts.For(escort);
            var finalRate = LossModel.FinalRate(baseRate, rules, loss);
            return new FeeQuote
            {
                GoodsValue = goodsValue,
                Fee = fee,
                BaseLossRate = baseRate,
                FinalLossRate = finalRate,
                Escort = escort,
                EscortMoney = rules.Money,
                EscortItemId = rules.ItemId,
                EscortItemAmount = rules.ItemAmount
            };
        }

        public static List<string> PaymentBlockers(
            long money,
            long cash,
            long escortItemCount,
            EscortRules escort,
            FeeQuote quote)
        {
            var reasons = new List<string>();
            if (money + cash < quote.TotalUpfront)
                reasons.Add($"运费/护送费不足：需要 {quote.TotalUpfront}，现有 {money + cash}");
            if (escort.ItemId > 0 && escortItemCount < escort.ItemAmount)
                reasons.Add($"护送物品不足：需要 {escort.ItemAmount} 个（随身携带 {escortItemCount} 个）");
            return reasons;
        }
    }
}
