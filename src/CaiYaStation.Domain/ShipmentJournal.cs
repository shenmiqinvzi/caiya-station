using System;
using System.Collections.Generic;
using System.Linq;

namespace CaiYaStation.Domain
{
    public enum ShipmentStage
    {
        Prepared = 0,
        Paid = 1,
        Delivering = 2,
        Completed = 3,
        Aborted = 4,
        Interrupted = 5
    }

    public sealed class JournalItem
    {
        public int InstanceId { get; set; }
        public int TypeId { get; set; }
        public string Name { get; set; } = "";
        public int StackCount { get; set; }
        public int UnitValue { get; set; }
        public bool Lost { get; set; }
        public bool Pushed { get; set; }
    }

    /// <summary>
    /// Write-ahead record for one shipment. Persisted before money is taken so a crash can never
    /// silently swallow goods: recovery only finishes deliveries that were already paid for.
    /// </summary>
    public sealed class ShipmentJournal
    {
        public string ShipmentId { get; set; } = "";
        public ShipmentStage Stage { get; set; } = ShipmentStage.Prepared;
        public string RaidToken { get; set; } = "";
        public string StationId { get; set; } = "";
        public string StationName { get; set; } = "";
        public string MapId { get; set; } = "";
        public string CreatedUtc { get; set; } = "";
        public long Fee { get; set; }
        public long EscortMoney { get; set; }
        public int EscortItemId { get; set; }
        public long EscortItemAmount { get; set; }
        public string EscortName { get; set; } = "";
        public double LossRate { get; set; }
        public string Flavor { get; set; } = "";
        public List<JournalItem> Items { get; set; } = new List<JournalItem>();
        public string Message { get; set; } = "";

        public long GoodsValue => Items.Sum(item => Math.Max(1, item.UnitValue) * (long)Math.Max(0, item.StackCount));
        public int PendingDeliveries => Items.Count(item => !item.Lost && !item.Pushed);
        public bool AllDelivered => Items.Where(item => !item.Lost).All(item => item.Pushed);
    }

    public static class ShipmentJournalRules
    {
        public static bool IsTerminal(ShipmentStage stage) =>
            stage == ShipmentStage.Completed || stage == ShipmentStage.Aborted;

        public static bool NeedsRecovery(ShipmentStage stage) =>
            stage == ShipmentStage.Paid || stage == ShipmentStage.Delivering || stage == ShipmentStage.Interrupted;
    }

    public sealed class ShipmentPlan
    {
        public SelectionTotals Totals { get; set; } = new SelectionTotals();
        public FeeQuote Quote { get; set; } = new FeeQuote();
        public LossPlan Loss { get; set; } = new LossPlan();
        public EscortRules Escort { get; set; } = new EscortRules();
    }

    public static class Shipments
    {
        public static ShipmentPlan Prepare(
            IReadOnlyList<ItemFacts> selected,
            EscortOption escortOption,
            PricingRules pricing,
            EscortSettings escorts,
            LossSettings loss,
            Random random,
            Func<int, double>? roll = null)
        {
            var totals = ShipmentRules.Totals(selected);
            var quote = ShipmentRules.Quote(totals.GoodsValue, escortOption, pricing, escorts, loss);
            var plan = LossModel.Plan(selected, quote.FinalLossRate, loss, random, roll);
            return new ShipmentPlan
            {
                Totals = totals,
                Quote = quote,
                Loss = plan,
                Escort = escorts.For(escortOption)
            };
        }

        public static ShipmentJournal Journal(
            string shipmentId,
            string stationId,
            string mapId,
            IReadOnlyList<ItemFacts> selected,
            ShipmentPlan plan,
            DateTimeOffset now,
            string stationName = "",
            string flavor = "")
        {
            var journal = new ShipmentJournal
            {
                ShipmentId = shipmentId,
                Stage = ShipmentStage.Prepared,
                StationId = stationId,
                StationName = stationName,
                MapId = mapId,
                CreatedUtc = now.ToUniversalTime().ToString("O"),
                Fee = plan.Quote.Fee,
                EscortMoney = plan.Quote.EscortMoney,
                EscortItemId = plan.Quote.EscortItemId,
                EscortItemAmount = plan.Quote.EscortItemAmount,
                EscortName = plan.Escort.Name,
                LossRate = plan.Quote.FinalLossRate,
                Flavor = flavor
            };
            for (var index = 0; index < selected.Count; index++)
            {
                var item = selected[index];
                journal.Items.Add(new JournalItem
                {
                    InstanceId = item.InstanceId,
                    TypeId = item.TypeId,
                    Name = item.Name,
                    StackCount = item.StackCount,
                    UnitValue = item.UnitValue,
                    Lost = plan.Loss.LostIndexes.Contains(index),
                    Pushed = false
                });
            }
            return journal;
        }
    }
}
