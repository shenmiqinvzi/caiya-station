using System;
using System.Collections.Generic;
using System.Linq;

namespace CaiYaStation.Domain
{
    public sealed class LossBand
    {
        public long MaxValue { get; set; }
        public double Rate { get; set; }
    }

    public sealed class LossSettings
    {
        public List<LossBand> Bands { get; set; } = new List<LossBand>
        {
            new LossBand { MaxValue = 2000, Rate = 0.05 },
            new LossBand { MaxValue = 10000, Rate = 0.08 },
            new LossBand { MaxValue = 30000, Rate = 0.12 },
            new LossBand { MaxValue = long.MaxValue, Rate = 0.15 }
        };

        /// <summary>Hard ceiling for any configured band or escort multiplier.</summary>
        public double MaximumRate { get; set; } = 0.20;

        /// <summary>At most 30% of selected stacks may be lost.</summary>
        public double MaxLostStackFraction { get; set; } = 0.30;

        /// <summary>At most 50% of selected goods value may be lost.</summary>
        public double MaxLostValueFraction { get; set; } = 0.50;

        /// <summary>When two or more stacks are selected, at least this many survive.</summary>
        public int MinimumKeptStacks { get; set; } = 1;
    }

    public sealed class LossPlan
    {
        public int SelectedStacks { get; set; }
        public long GoodsValue { get; set; }
        public double Rate { get; set; }
        public List<int> LostIndexes { get; set; } = new List<int>();
        public long LostValue { get; set; }
        public bool AllowsTotalLoss { get; set; }

        public int LostStacks => LostIndexes.Count;
        public long DeliveredValue => GoodsValue - LostValue;
        public bool IsTotalLoss => SelectedStacks > 0 && LostIndexes.Count == SelectedStacks;
    }

    public static class LossModel
    {
        public static double BaseRate(long goodsValue, LossSettings settings)
        {
            foreach (var band in settings.Bands.OrderBy(band => band.MaxValue))
            {
                if (goodsValue <= band.MaxValue) return ClampRate(band.Rate, settings);
            }
            return ClampRate(settings.Bands.Count == 0 ? 0 : settings.Bands.Max(band => band.Rate), settings);
        }

        public static double FinalRate(double baseRate, EscortRules escort, LossSettings settings)
        {
            var rate = baseRate * escort.LossFactor;
            if (rate < escort.MinimumLossRate) rate = escort.MinimumLossRate;
            return ClampRate(rate, settings);
        }

        public static double ClampRate(double rate, LossSettings settings)
        {
            if (rate < 0) return 0;
            var ceiling = Math.Min(settings.MaximumRate, 1.0);
            return rate > ceiling ? ceiling : rate;
        }

        /// <summary>
        /// Rolls each selected stack independently, then trims candidates to the count and value caps.
        /// A single selected stack may be lost entirely when the roll succeeds (the panel warns about it).
        /// </summary>
        public static LossPlan Plan(
            IReadOnlyList<ItemFacts> selected,
            double rate,
            LossSettings settings,
            Random random,
            Func<int, double>? roll = null)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var plan = new LossPlan
            {
                SelectedStacks = selected.Count,
                GoodsValue = selected.Sum(item => item.GoodsValue),
                Rate = ClampRate(rate, settings),
                AllowsTotalLoss = selected.Count == 1
            };
            if (selected.Count == 0 || plan.Rate <= 0) return plan;

            var candidates = new List<int>();
            for (var index = 0; index < selected.Count; index++)
            {
                var value = roll != null ? roll(index) : random.NextDouble();
                if (value < plan.Rate) candidates.Add(index);
            }
            if (candidates.Count == 0) return plan;

            if (selected.Count == 1)
            {
                plan.LostIndexes.Add(0);
                plan.LostValue = selected[0].GoodsValue;
                return plan;
            }

            var maxLostStacks = Math.Max(1, (int)Math.Floor(selected.Count * settings.MaxLostStackFraction));
            if (maxLostStacks > selected.Count - Math.Max(0, settings.MinimumKeptStacks))
                maxLostStacks = Math.Max(0, selected.Count - Math.Max(0, settings.MinimumKeptStacks));
            var maxLostValue = (long)Math.Floor(plan.GoodsValue * settings.MaxLostValueFraction);

            while (candidates.Count > maxLostStacks)
                RemoveRandom(candidates, random);
            while (candidates.Count > 0 && ValueOf(selected, candidates) > maxLostValue)
                RemoveRandom(candidates, random);
            while (candidates.Count > 0 && selected.Count - candidates.Count < settings.MinimumKeptStacks)
                RemoveRandom(candidates, random);

            candidates.Sort();
            plan.LostIndexes.AddRange(candidates);
            plan.LostValue = ValueOf(selected, candidates);
            return plan;
        }

        private static long ValueOf(IReadOnlyList<ItemFacts> selected, List<int> indexes)
        {
            long total = 0;
            foreach (var index in indexes) total += selected[index].GoodsValue;
            return total;
        }

        private static void RemoveRandom(List<int> indexes, Random random)
        {
            if (indexes.Count == 0) return;
            indexes.RemoveAt(random.Next(indexes.Count));
        }
    }
}
