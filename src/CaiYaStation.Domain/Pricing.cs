using System;
using System.Collections.Generic;

namespace CaiYaStation.Domain
{
    public enum EscortOption
    {
        None = 0,
        Scavenger = 1,
        Mercenary = 2
    }

    /// <summary>One backpack stack as the shipment rules see it.</summary>
    public sealed class ItemFacts
    {
        public int InstanceId { get; set; }
        public int TypeId { get; set; }
        public string Name { get; set; } = "";
        public int StackCount { get; set; }
        public int UnitValue { get; set; }
        public double TotalWeight { get; set; }
        public bool IsEquipped { get; set; }
        public bool CanDrop { get; set; } = true;
        public bool HasContainerInventory { get; set; }
        public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
        /// <summary>True when the tag list could not be read completely. Protection rules must
        /// fail closed for such stacks: they may hide quest/keys/cash tags.</summary>
        public bool ProtectionCheckFailed { get; set; }

        public long GoodsValue => Math.Max(1, UnitValue) * (long)Math.Max(0, StackCount);
    }

    public sealed class SelectionTotals
    {
        public int Stacks { get; set; }
        public long GoodsValue { get; set; }
        public double WeightKg { get; set; }
    }

    public sealed class PricingRules
    {
        public double Rate { get; set; } = 0.10;
        public long Minimum { get; set; } = 200;
        public long Maximum { get; set; } = 5000;

        public static PricingRules Default => new PricingRules();
    }

    public sealed class FeeQuote
    {
        public long GoodsValue { get; set; }
        public long Fee { get; set; }
        public double BaseLossRate { get; set; }
        public double FinalLossRate { get; set; }
        public EscortOption Escort { get; set; }
        public long EscortMoney { get; set; }
        public int EscortItemId { get; set; }
        public long EscortItemAmount { get; set; }

        public long EscortFee => EscortMoney;
        public long TotalUpfront => Fee + EscortMoney;
        public long ExpectedLoss => (long)Math.Round(GoodsValue * FinalLossRate, MidpointRounding.AwayFromZero);
        public long ExpectedTotalCost => TotalUpfront + ExpectedLoss;
    }

    public static class Pricing
    {
        /// <summary>
        /// Design baseline: 10% of goods value, rounded half up, clamped to [200, 5000].
        /// Integer arithmetic avoids banker's rounding surprises at .5 boundaries.
        /// </summary>
        public static long Fee(long goodsValue, PricingRules rules)
        {
            if (goodsValue <= 0) return rules.Minimum;
            var scaled = (decimal)goodsValue * (decimal)rules.Rate;
            var fee = (long)Math.Round(scaled, MidpointRounding.AwayFromZero);
            if (fee < rules.Minimum) fee = rules.Minimum;
            if (fee > rules.Maximum) fee = rules.Maximum;
            return fee;
        }
    }

    public sealed class EscortRules
    {
        public EscortOption Option { get; set; }
        public string Name { get; set; } = "";
        public long Money { get; set; }
        public int ItemId { get; set; }
        public long ItemAmount { get; set; }
        public double LossFactor { get; set; } = 1.0;
        public double MinimumLossRate { get; set; } = 0.0;
    }

    public sealed class EscortSettings
    {
        public EscortRules None { get; set; } = new EscortRules { Option = EscortOption.None, Name = "无", LossFactor = 1.0 };
        public EscortRules Scavenger { get; set; } = new EscortRules
        {
            Option = EscortOption.Scavenger,
            Name = "拾荒者团",
            Money = 1000,
            ItemId = 368,
            ItemAmount = 4,
            LossFactor = 0.6,
            MinimumLossRate = 0.02
        };

        public EscortRules Mercenary { get; set; } = new EscortRules
        {
            Option = EscortOption.Mercenary,
            Name = "雇佣兵团",
            Money = 2000,
            ItemId = 890,
            ItemAmount = 4,
            LossFactor = 0.3,
            MinimumLossRate = 0.02
        };

        public EscortRules For(EscortOption option) => option switch
        {
            EscortOption.Scavenger => Scavenger,
            EscortOption.Mercenary => Mercenary,
            _ => None
        };
    }
}
