using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CaiYaStation.Domain
{
    public sealed class ReceiptLine
    {
        public int TypeId { get; set; }
        public string Name { get; set; } = "";
        public int StackCount { get; set; }
        public int UnitValue { get; set; }
        public long GoodsValue => Math.Max(1, UnitValue) * (long)Math.Max(0, StackCount);
    }

    public sealed class Receipt
    {
        public string ShipmentId { get; set; } = "";
        public string CreatedUtc { get; set; } = "";
        public string StationId { get; set; } = "";
        public string StationName { get; set; } = "";
        public string MapId { get; set; } = "";
        public List<ReceiptLine> Delivered { get; set; } = new List<ReceiptLine>();
        public List<ReceiptLine> Lost { get; set; } = new List<ReceiptLine>();
        public long GoodsValue { get; set; }
        public long Fee { get; set; }
        public long EscortMoney { get; set; }
        public string EscortName { get; set; } = "";
        public double LossRate { get; set; }
        public string Flavor { get; set; } = "";
        public string LossCulprits { get; set; } = "";
        public string LossKaomoji { get; set; } = "";

        public long DeliveredValue => Delivered.Sum(line => line.GoodsValue);
        public long LostValue => Lost.Sum(line => line.GoodsValue);
        public long TotalUpfront => Fee + EscortMoney;
        public int DeliveredStacks => Delivered.Count;
        public int LostStacks => Lost.Count;

        public bool IsConsistent =>
            DeliveredValue + LostValue == GoodsValue &&
            Delivered.All(line => line.StackCount > 0) &&
            Lost.All(line => line.StackCount > 0);

        /// <summary>True when the shipment lost at least one stack: the notice sheet applies.</summary>
        public bool NeedsLossNotice => LostStacks > 0;
    }

    public static class Receipts
    {
        private static readonly string[] FlavorTexts =
        {
            "路上遇到拾荒者拦路，丢了几件。",
            "翻越塌方时摔散了一部分货物。",
            "风暴里车队走散了，清点时少了几件。",
            "护送队说有人一路尾随，只保住了大部分。",
            "箱子在半路被撬开，好在剩下的还在。"
        };

        public static string RandomFlavor(Random random) => FlavorTexts[random.Next(FlavorTexts.Length)];

        /// <summary>
        /// Player-facing loss notice: a randomly generated culprit combo (one to three of
        /// 雇佣兵 / 拾荒者 / 机械蜘蛛) plus a pained kaomoji. Kept pure so it can be asserted.
        /// </summary>
        public sealed class LossNotice
        {
            public string Culprits { get; set; } = "";
            public string Kaomoji { get; set; } = "";
        }

        private static readonly string[] CulpritPool = { "雇佣兵", "拾荒者", "机械蜘蛛" };
        private static readonly string[] KaomojiPool =
        {
            "(´;ω;`)",
            "(╥﹏╥)",
            "(；´д｀)",
            "(T＿T)",
            "(っ˘̩╭╮˘̩)っ"
        };

        public static LossNotice RandomLossNotice(Random random)
        {
            var picked = CulpritPool.OrderBy(_ => random.Next()).ToList();
            var count = random.Next(1, picked.Count + 1);
            var culprits = count == 1 ? picked[0] : string.Join("、", picked.Take(count));
            return new LossNotice
            {
                Culprits = culprits,
                Kaomoji = KaomojiPool[random.Next(KaomojiPool.Length)]
            };
        }

        public static Receipt Build(
            string shipmentId,
            string stationId,
            string stationName,
            string mapId,
            IReadOnlyList<ItemFacts> selected,
            LossPlan loss,
            FeeQuote quote,
            EscortRules escort,
            string flavor,
            DateTimeOffset now)
        {
            var receipt = new Receipt
            {
                ShipmentId = shipmentId,
                CreatedUtc = now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                StationId = stationId,
                StationName = stationName,
                MapId = mapId,
                GoodsValue = quote.GoodsValue,
                Fee = quote.Fee,
                EscortMoney = quote.EscortMoney,
                EscortName = escort.Name,
                LossRate = quote.FinalLossRate,
                Flavor = flavor
            };
            for (var index = 0; index < selected.Count; index++)
            {
                var item = selected[index];
                var line = new ReceiptLine
                {
                    TypeId = item.TypeId,
                    Name = item.Name,
                    StackCount = item.StackCount,
                    UnitValue = item.UnitValue
                };
                if (loss.LostIndexes.Contains(index)) receipt.Lost.Add(line);
                else receipt.Delivered.Add(line);
            }
            return receipt;
        }

        /// <summary>Player-facing receipt text. Kept pure so it can be asserted in tests.</summary>
        public static string Render(Receipt receipt)
        {
            var builder = new StringBuilder();
            builder.AppendLine("菜鸭驿站 · 发货回执");
            builder.AppendLine("送达：马蜂自提点");
            builder.AppendLine($"货物：{receipt.DeliveredStacks + receipt.LostStacks} 件 / 价值 {Money(receipt.GoodsValue)}");
            builder.AppendLine($"运费：{Money(receipt.Fee)}");
            builder.AppendLine(receipt.EscortMoney > 0
                ? $"护送：{receipt.EscortName}（损耗 {Percent(receipt.LossRate)}，费用 {Money(receipt.EscortMoney)}）"
                : $"护送：无（损耗 {Percent(receipt.LossRate)}）");
            if (receipt.LostStacks > 0)
            {
                builder.AppendLine($"丢失：{receipt.LostStacks} 件 / 价值 {Money(receipt.LostValue)}");
                foreach (var line in receipt.Lost)
                    builder.AppendLine($"   - {line.Name} x{line.StackCount}");
            }
            else
            {
                builder.AppendLine("丢失：无");
            }
            builder.AppendLine($"送达：{receipt.DeliveredStacks} 件 / 价值 {Money(receipt.DeliveredValue)}");
            if (!string.IsNullOrWhiteSpace(receipt.Flavor)) builder.AppendLine(receipt.Flavor);
            return builder.ToString().TrimEnd();
        }

        private static string Money(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
        private static string Percent(double rate) => (rate * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }
}
