using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CaiYaStation.Domain;
using Newtonsoft.Json;

var cases = new List<(string Name, Action Run)>();
void Add(string name, Action run) => cases.Add((name, run));

// ---------------------------------------------------------------- pricing
Add("fee floor is 200 for an empty shipment", () => Assert.Equal(200L, Pricing.Fee(0, PricingRules.Default)));
Add("fee is 10% of 2,000", () => Assert.Equal(200L, Pricing.Fee(2_000, PricingRules.Default)));
Add("fee is 1,000 at 10,000", () => Assert.Equal(1_000L, Pricing.Fee(10_000, PricingRules.Default)));
Add("fee is 3,000 at 30,000", () => Assert.Equal(3_000L, Pricing.Fee(30_000, PricingRules.Default)));
Add("fee caps at 5,000 for 50,000", () => Assert.Equal(5_000L, Pricing.Fee(50_000, PricingRules.Default)));
Add("fee caps at 5,000 for 100,000", () => Assert.Equal(5_000L, Pricing.Fee(100_000, PricingRules.Default)));
Add("fee rounds half away from zero", () => Assert.Equal(235L, Pricing.Fee(2_345, PricingRules.Default)));
Add("fee half-up at 2,350", () => Assert.Equal(235L, Pricing.Fee(2_350, PricingRules.Default)));
Add("fee below floor still charges 200", () => Assert.Equal(200L, Pricing.Fee(1_999, PricingRules.Default)));
Add("fee uses configured rate", () => Assert.Equal(300L, Pricing.Fee(2_000, new PricingRules { Rate = 0.15 })));

// ---------------------------------------------------------------- loss bands
var loss = new LossSettings();
Add("loss band 2,000 is 5%", () => Assert.Near(0.05, LossModel.BaseRate(2_000, loss)));
Add("loss band 2,001 is 8%", () => Assert.Near(0.08, LossModel.BaseRate(2_001, loss)));
Add("loss band 10,000 is 8%", () => Assert.Near(0.08, LossModel.BaseRate(10_000, loss)));
Add("loss band 10,001 is 12%", () => Assert.Near(0.12, LossModel.BaseRate(10_001, loss)));
Add("loss band 30,000 is 12%", () => Assert.Near(0.12, LossModel.BaseRate(30_000, loss)));
Add("loss band 30,001 is 15%", () => Assert.Near(0.15, LossModel.BaseRate(30_001, loss)));
Add("escort-free loss is unchanged", () =>
{
    var escorts = new EscortSettings();
    Assert.Near(0.15, LossModel.FinalRate(0.15, escorts.None, loss));
});
Add("scavenger multiplies loss by 0.6", () =>
{
    var escorts = new EscortSettings();
    Assert.Near(0.09, LossModel.FinalRate(0.15, escorts.Scavenger, loss));
});
Add("mercenary multiplies loss by 0.3", () =>
{
    var escorts = new EscortSettings();
    Assert.Near(0.045, LossModel.FinalRate(0.15, escorts.Mercenary, loss));
});
Add("escort loss floor is 2%", () =>
{
    var escorts = new EscortSettings();
    Assert.Near(0.02, LossModel.FinalRate(0.02, escorts.Mercenary, loss));
});
Add("loss rate ceiling is 20%", () =>
{
    var escorts = new EscortSettings { Mercenary = new EscortRules { LossFactor = 10, MinimumLossRate = 0.02 } };
    Assert.Near(0.20, LossModel.FinalRate(0.15, escorts.Mercenary, loss));
});

// ---------------------------------------------------------------- limits
Add("12 stacks are allowed", () =>
{
    var selected = Enumerable.Range(0, 12).Select(index => Item(index, 100)).ToList();
    var check = ShipmentRules.Check(selected, new ShipmentLimits());
    Assert.True(check.Allowed);
    Assert.Equal(12, check.Totals.Stacks);
});
Add("13 stacks are rejected", () =>
{
    var selected = Enumerable.Range(0, 13).Select(index => Item(index, 100)).ToList();
    var check = ShipmentRules.Check(selected, new ShipmentLimits());
    Assert.False(check.Allowed);
    Assert.Equal("超出 12 格上限", check.Reason);
});
Add("25.0 kg is allowed", () =>
{
    var selected = new List<ItemFacts> { Item(1, 100, weight: 25) };
    Assert.True(ShipmentRules.Check(selected, new ShipmentLimits()).Allowed);
});
Add("25.1 kg is rejected", () =>
{
    var selected = new List<ItemFacts> { Item(1, 100, weight: 25.1) };
    var check = ShipmentRules.Check(selected, new ShipmentLimits());
    Assert.False(check.Allowed);
    Assert.Equal("超出 25 kg 上限", check.Reason);
});
Add("empty selection is rejected", () =>
{
    var check = ShipmentRules.Check(new List<ItemFacts>(), new ShipmentLimits());
    Assert.False(check.Allowed);
    Assert.Equal("请选择要寄送的物品", check.Reason);
});
Add("selection totals sum value and weight", () =>
{
    var totals = ShipmentRules.Totals(new[] { Item(1, 300, count: 2, weight: 1.5), Item(2, 50, count: 4, weight: 0.25) });
    Assert.Equal(2, totals.Stacks);
    Assert.Equal(800L, totals.GoodsValue);
    Assert.Near(1.75, totals.WeightKg);
});

// ---------------------------------------------------------------- eligibility
var protection = new ProtectionRules();
Add("ordinary loot is eligible", () => Assert.True(ItemEligibility.IsEligible(Item(10, 100, tags: "Material"), protection)));
Add("quest items are protected", () => Assert.Equal("任务/钥匙/唯一物品不可寄送", ItemEligibility.BlockReason(Item(10, 100, tags: "Quest"), protection)));
Add("keys are protected", () => Assert.Equal("任务/钥匙/唯一物品不可寄送", ItemEligibility.BlockReason(Item(10, 100, tags: "Key"), protection)));
Add("special keys are protected", () => Assert.Equal("任务/钥匙/唯一物品不可寄送", ItemEligibility.BlockReason(Item(10, 100, tags: "SpecialKey"), protection)));
Add("currency tag is protected", () => Assert.Equal("现金/货币类物品不可寄送", ItemEligibility.BlockReason(Item(451, 1, tags: "Cash"), protection)));
Add("currency type id is protected", () => Assert.Equal("现金/货币类物品不可寄送", ItemEligibility.BlockReason(Item(451, 1), protection)));
Add("container inventory is protected", () => Assert.Equal("容器类物品不可寄送", ItemEligibility.BlockReason(Item(10, 100, container: true), protection)));
Add("container tag is protected", () => Assert.Equal("容器类物品不可寄送", ItemEligibility.BlockReason(Item(10, 100, tags: "Continer"), protection)));
Add("equipped items are protected", () => Assert.Equal("装备中的物品不可寄送", ItemEligibility.BlockReason(Item(10, 100, equipped: true), protection)));
Add("undroppable items are protected", () => Assert.Equal("该物品不可丢弃，不能寄送", ItemEligibility.BlockReason(Item(10, 100, canDrop: false), protection)));
Add("quest name prefix is protected", () => Assert.Equal("任务/钥匙/唯一物品不可寄送", ItemEligibility.BlockReason(Item(10, 100, name: "Item_Quest_Watch"), protection)));
Add("deliver box prefix is protected", () => Assert.Equal("任务/钥匙/唯一物品不可寄送", ItemEligibility.BlockReason(Item(10, 100, name: "DeliverBox_2102"), protection)));
Add("zero-value ordinary item stays selectable", () => Assert.True(ItemEligibility.IsEligible(Item(10, 0), protection)));

// ---------------------------------------------------------------- loss planning
Add("single stack can be lost entirely at the shown rate", () =>
{
    var plan = LossModel.Plan(new[] { Item(1, 500) }, 0.05, loss, new Random(1), _ => 0.01);
    Assert.Equal(1, plan.LostStacks);
    Assert.True(plan.IsTotalLoss);
    Assert.Equal(500L, plan.LostValue);
});
Add("single stack survives when the roll is above the rate", () =>
{
    var plan = LossModel.Plan(new[] { Item(1, 500) }, 0.05, loss, new Random(1), _ => 0.99);
    Assert.Equal(0, plan.LostStacks);
    Assert.False(plan.IsTotalLoss);
});
Add("two stacks lose at most one", () =>
{
    var plan = LossModel.Plan(new[] { Item(1, 500), Item(2, 500) }, 0.15, loss, new Random(2), _ => 0.0);
    Assert.Equal(1, plan.LostStacks);
    Assert.True(plan.SelectedStacks - plan.LostStacks >= 1);
});
Add("ten stacks lose at most three and half the value", () =>
{
    var items = Enumerable.Range(0, 10).Select(index => Item(index, 1_000)).ToList();
    var plan = LossModel.Plan(items, 0.15, loss, new Random(3), _ => 0.0);
    Assert.Equal(3, plan.LostStacks);
    Assert.True(plan.LostValue <= 5_000);
    Assert.True(plan.LostIndexes.Distinct().Count() == plan.LostIndexes.Count);
    Assert.True(plan.LostIndexes.SequenceEqual(plan.LostIndexes.OrderBy(index => index)));
});
Add("value cap trims expensive candidates", () =>
{
    var items = new List<ItemFacts> { Item(1, 9_000), Item(2, 1_000), Item(3, 100), Item(4, 100) };
    var plan = LossModel.Plan(items, 0.15, loss, new Random(4), _ => 0.0);
    Assert.True(plan.LostValue <= 5_000);
    Assert.True(plan.LostStacks <= 1);
});
Add("rate zero never loses anything", () =>
{
    var plan = LossModel.Plan(new[] { Item(1, 500) }, 0, loss, new Random(5), _ => 0.0);
    Assert.Equal(0, plan.LostStacks);
});
Add("rate above 100% is clamped", () =>
{
    var plan = LossModel.Plan(Enumerable.Range(0, 5).Select(i => Item(i, 100)).ToList(), 3.0, loss, new Random(6), _ => 0.0);
    Assert.True(plan.Rate <= loss.MaximumRate);
});
Add("plan totals are consistent", () =>
{
    var items = Enumerable.Range(0, 8).Select(index => Item(index, 250, count: index + 1)).ToList();
    var plan = LossModel.Plan(items, 0.15, loss, new Random(7));
    Assert.Equal(plan.GoodsValue, plan.DeliveredValue + plan.LostValue);
});
Add("expected loss uses the final rate", () =>
{
    var quote = ShipmentRules.Quote(10_000, EscortOption.Mercenary, PricingRules.Default, new EscortSettings(), loss);
    Assert.Near(0.024, quote.FinalLossRate);
    Assert.Equal(240L, quote.ExpectedLoss);
});
Add("quote totals add fee and escort money", () =>
{
    var quote = ShipmentRules.Quote(30_000, EscortOption.Scavenger, PricingRules.Default, new EscortSettings(), loss);
    Assert.Equal(3_000L, quote.Fee);
    Assert.Equal(1_000L, quote.EscortMoney);
    Assert.Equal(4_000L, quote.TotalUpfront);
    Assert.Equal(368, quote.EscortItemId);
    Assert.Equal(4L, quote.EscortItemAmount);
});
Add("mercenary quote uses dog tags", () =>
{
    var quote = ShipmentRules.Quote(10_000, EscortOption.Mercenary, PricingRules.Default, new EscortSettings(), loss);
    Assert.Equal(890, quote.EscortItemId);
    Assert.Equal(2_000L, quote.EscortMoney);
});
Add("payment blockers report both shortages", () =>
{
    var quote = ShipmentRules.Quote(10_000, EscortOption.Scavenger, PricingRules.Default, new EscortSettings(), loss);
    var blockers = ShipmentRules.PaymentBlockers(150, 0, 2, new EscortSettings().Scavenger, quote);
    Assert.Equal(2, blockers.Count);
    Assert.True(blockers[0].Contains("运费/护送费不足"));
    Assert.True(blockers[1].Contains("护送物品不足"));
});
Add("payment is accepted with money and carried items", () =>
{
    var quote = ShipmentRules.Quote(10_000, EscortOption.Scavenger, PricingRules.Default, new EscortSettings(), loss);
    Assert.Equal(0, ShipmentRules.PaymentBlockers(1_000, 1_000, 4, new EscortSettings().Scavenger, quote).Count);
});

// ---------------------------------------------------------------- shipment composition
Add("prepare ties quote, loss and escort together", () =>
{
    var items = new[] { Item(1, 1_000), Item(2, 1_000) };
    var plan = Shipments.Prepare(items, EscortOption.Scavenger, PricingRules.Default, new EscortSettings(), loss, new Random(8), _ => 0.01);
    Assert.Near(0.03, plan.Quote.FinalLossRate);
    Assert.Equal(200L, plan.Quote.Fee);
    Assert.Equal(1_000L, plan.Quote.EscortMoney);
    Assert.Equal(2, plan.Totals.Stacks);
});
Add("journal records lost and delivered lines", () =>
{
    var items = new[] { Item(11, 1_000), Item(12, 1_000) };
    var plan = Shipments.Prepare(items, EscortOption.None, PricingRules.Default, new EscortSettings(), loss, new Random(9), _ => 0.0);
    var journal = Shipments.Journal("ship-1", "wh-1", "Level_HiddenWarehouse_Main", items, plan, DateTimeOffset.UtcNow);
    Assert.Equal(2, journal.Items.Count);
    Assert.Equal(1, journal.Items.Count(item => item.Lost));
    Assert.Equal(1, journal.PendingDeliveries);
    Assert.False(ShipmentJournalRules.NeedsRecovery(journal.Stage));
});
Add("terminal stages are recognized", () =>
{
    Assert.True(ShipmentJournalRules.IsTerminal(ShipmentStage.Completed));
    Assert.True(ShipmentJournalRules.IsTerminal(ShipmentStage.Aborted));
    Assert.False(ShipmentJournalRules.IsTerminal(ShipmentStage.Delivering));
});

// ---------------------------------------------------------------- stations and raids
Add("station choice respects count and separation", () =>
{
    var candidates = new List<StationCandidate>
    {
        Candidate("a", 0, 0), Candidate("b", 5, 0), Candidate("c", 100, 0), Candidate("d", 200, 0)
    };
    var chosen = StationPlanning.Choose(candidates, 2, 15, new Random(10));
    Assert.Equal(2, chosen.Count);
    Assert.True(chosen[0].DistanceTo(chosen[1]) >= 15);
    Assert.Equal(2, chosen.Select(station => station.Id).Distinct().Count());
});
Add("station choice fills from a tight pool", () =>
{
    var candidates = new List<StationCandidate> { Candidate("a", 0, 0), Candidate("b", 1, 0), Candidate("c", 2, 0) };
    var chosen = StationPlanning.Choose(candidates, 3, 15, new Random(11));
    Assert.Equal(3, chosen.Count);
});
Add("station choice never returns more than the pool", () =>
{
    var chosen = StationPlanning.Choose(new List<StationCandidate> { Candidate("a", 0, 0) }, 3, 15, new Random(12));
    Assert.Equal(1, chosen.Count);
});
Add("zero station request returns nothing", () =>
    Assert.Equal(0, StationPlanning.Choose(new List<StationCandidate> { Candidate("a", 0, 0) }, 0, 15, new Random(13)).Count));
Add("raid session tracks usage per map without reroll", () =>
{
    var session = new RaidSession { Token = "raid-1", StartedUtc = DateTimeOffset.UtcNow.ToString("O") };
    session.AddStations(new[]
    {
        new StationRecord { Id = "wh-1", MapId = "wh" },
        new StationRecord { Id = "farm-1", MapId = "farm" },
        new StationRecord { Id = "farm-2", MapId = "farm" }
    });
    Assert.Equal(2, session.ForMap("farm").Count());
    Assert.Equal(2, session.AvailableCount("farm"));
    Assert.True(session.MarkUsed("farm-1"));
    Assert.False(session.MarkUsed("farm-1"));
    Assert.Equal(2, session.AvailableCount("farm") + session.UsedCount("farm"));
    Assert.Equal(1, session.ForMap("farm").Count(station => !station.Used));
});
Add("unknown station cannot be marked used", () =>
{
    var session = new RaidSession();
    Assert.False(session.MarkUsed("missing"));
});
Add("duplicate station ids are not added twice", () =>
{
    var session = new RaidSession();
    session.AddStations(new[] { new StationRecord { Id = "wh-1" } });
    session.AddStations(new[] { new StationRecord { Id = "wh-1" } });
    Assert.Equal(1, session.Stations.Count);
});
Add("station distance is euclidean", () =>
{
    var a = new StationRecord { X = 0, Y = 0, Z = 0 };
    var b = new StationRecord { X = 3, Y = 4, Z = 0 };
    Assert.Near(5, a.DistanceTo(b));
});

// ---------------------------------------------------------------- receipts
Add("receipt splits delivered and lost value", () =>
{
    var items = new[] { Item(1, 1_000, count: 2), Item(2, 500), Item(3, 100) };
    var plan = Shipments.Prepare(items, EscortOption.None, PricingRules.Default, new EscortSettings(), loss, new Random(14), _ => 0.0);
    var receipt = Receipts.Build("ship-2", "wh-1", "菜鸭驿站", "Level_HiddenWarehouse_Main", items, plan.Loss,
        plan.Quote, plan.Escort, "测试风味", DateTimeOffset.UtcNow);
    Assert.True(receipt.IsConsistent);
    Assert.Equal(receipt.GoodsValue, receipt.DeliveredValue + receipt.LostValue);
    Assert.Equal(plan.Loss.LostStacks, receipt.LostStacks);
});
Add("receipt text lists the destination and loss", () =>
{
    var items = new[] { Item(1, 1_000), Item(2, 500) };
    var plan = Shipments.Prepare(items, EscortOption.Scavenger, PricingRules.Default, new EscortSettings(), loss, new Random(15), _ => 0.0);
    var receipt = Receipts.Build("ship-3", "wh-1", "菜鸭驿站", "Level_HiddenWarehouse_Main", items, plan.Loss,
        plan.Quote, plan.Escort, "", DateTimeOffset.UtcNow);
    var text = Receipts.Render(receipt);
    Assert.True(text.Contains("菜鸭驿站 · 发货回执"));
    Assert.True(text.Contains("送达：马蜂自提点"));
    Assert.True(text.Contains("护送：拾荒者团"));
    Assert.True(text.Contains("丢失："));
});
Add("receipt with no losses says so", () =>
{
    var items = new[] { Item(1, 1_000) };
    var plan = Shipments.Prepare(items, EscortOption.None, PricingRules.Default, new EscortSettings(), loss, new Random(16), _ => 0.99);
    var receipt = Receipts.Build("ship-4", "wh-1", "菜鸭驿站", "Level_HiddenWarehouse_Main", items, plan.Loss,
        plan.Quote, plan.Escort, "", DateTimeOffset.UtcNow);
    Assert.True(Receipts.Render(receipt).Contains("丢失：无"));
    Assert.True(receipt.IsConsistent);
});
Add("zero-value items still count as value one", () => Assert.Equal(3L, Item(1, 0, count: 3).GoodsValue));
Add("loss notice is generated for lost shipments", () =>
{
    var random = new Random(42);
    var notice = Receipts.RandomLossNotice(random);
    Assert.True(notice.Culprits.Length > 0);
    Assert.True(notice.Kaomoji.Length > 0);
    Assert.True(notice.Culprits.Contains("雇佣兵") || notice.Culprits.Contains("拾荒者") || notice.Culprits.Contains("机械蜘蛛"));
});
Add("loss notice only fires when stacks were lost", () =>
{
    var lost = new Receipt { Lost = { new ReceiptLine { Name = "x", StackCount = 1, UnitValue = 10 } } };
    var clean = new Receipt { Delivered = { new ReceiptLine { Name = "y", StackCount = 1, UnitValue = 10 } } };
    Assert.True(lost.NeedsLossNotice);
    Assert.False(clean.NeedsLossNotice);
});
Add("loss notice fields survive JSON round-trip", () =>
{
    var receipt = new Receipt { LossCulprits = "机械蜘蛛、拾荒者", LossKaomoji = "(╥﹏╥)" };
    var json = JsonConvert.SerializeObject(receipt);
    var restored = JsonConvert.DeserializeObject<Receipt>(json)!;
    Assert.Equal("机械蜘蛛、拾荒者", restored.LossCulprits);
    Assert.Equal("(╥﹏╥)", restored.LossKaomoji);
});
Add("receipts wait for the bunker before counting as read", () =>
{
    var receipt = new Receipt { Viewed = false };
    Assert.True(receipt.AwaitingView);
    receipt.Viewed = true;
    Assert.False(receipt.AwaitingView);
    // Written before receipts were deferred: no flag, must not replay on upgrade.
    Assert.False(new Receipt().AwaitingView);
    var json = JsonConvert.SerializeObject(new Receipt { Viewed = false });
    Assert.True(JsonConvert.DeserializeObject<Receipt>(json)!.AwaitingView);
});
Add("flavour never claims a loss that did not happen", () =>
{
    for (var seed = 0; seed < 60; seed++)
    {
        var clean = Receipts.RandomFlavor(new Random(seed), false);
        Assert.False(clean.Contains("丢") || clean.Contains("摔散") || clean.Contains("撬开"),
            "delivered flavour mentions a loss: " + clean);
    }
    for (var seed = 0; seed < 60; seed++)
    {
        var lost = Receipts.RandomFlavor(new Random(seed), true);
        Assert.False(lost.Contains("一件没少") || lost.Contains("整批货都到了"),
            "loss flavour claims everything arrived: " + lost);
    }
});
Add("fail-closed: unreadable tags block shipping", () =>
{
    // A stack whose tags could not be read must be refused even if the visible
    // name/type look shippable: the tag list may hide quest/key/cash tags.
    var item = Item(999, 100);
    Assert.Equal(null, ItemEligibility.BlockReason(item, new ProtectionRules()));
    item.ProtectionCheckFailed = true;
    Assert.True(ItemEligibility.BlockReason(item, new ProtectionRules()) != null);
});
Add("receipt JSON round-trips with computed fields", () =>
{
    var items = new[] { Item(1, 1_000), Item(2, 500) };
    var plan = Shipments.Prepare(items, EscortOption.Scavenger, PricingRules.Default, new EscortSettings(), loss, new Random(17), _ => 0.0);
    var receipt = Receipts.Build("ship-json", "wh-1", "菜鸭驿站", "Level_HiddenWarehouse_Main", items, plan.Loss,
        plan.Quote, plan.Escort, "风味", DateTimeOffset.UtcNow);
    var json = JsonConvert.SerializeObject(receipt);
    var restored = JsonConvert.DeserializeObject<Receipt>(json)!;
    Assert.Equal(receipt.ShipmentId, restored.ShipmentId);
    Assert.Equal(receipt.GoodsValue, restored.GoodsValue);
    Assert.Equal(receipt.DeliveredStacks, restored.DeliveredStacks);
    Assert.Equal(receipt.LostStacks, restored.LostStacks);
    Assert.True(restored.IsConsistent);
});
Add("shipment journal JSON round-trips", () =>
{
    var items = new[] { Item(11, 1_000), Item(12, 500) };
    var plan = Shipments.Prepare(items, EscortOption.Mercenary, PricingRules.Default, new EscortSettings(), loss, new Random(18), _ => 0.0);
    var journal = Shipments.Journal("ship-j", "farm-1", "Level_Farm_Main", items, plan, DateTimeOffset.UtcNow, "菜鸭驿站", "风味");
    journal.RaidToken = "raid-token-1";
    journal.Stage = ShipmentStage.Delivering;
    var json = JsonConvert.SerializeObject(journal);
    var restored = JsonConvert.DeserializeObject<ShipmentJournal>(json)!;
    Assert.Equal("raid-token-1", restored.RaidToken);
    Assert.Equal(ShipmentStage.Delivering, restored.Stage);
    Assert.Equal(journal.Items.Count, restored.Items.Count);
    Assert.Equal(journal.Fee, restored.Fee);
    Assert.Equal(journal.EscortItemId, restored.EscortItemId);
});

// ---------------------------------------------------------------- shipped config files
var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
var layoutPath = Path.Combine(AppContext.BaseDirectory, "station-layout.json");
var jsonSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
var shipped = JsonConvert.DeserializeObject<ConfigProbe>(File.ReadAllText(configPath), jsonSettings)
              ?? throw new InvalidOperationException("config.json did not parse");
var layoutProbe = JsonConvert.DeserializeObject<LayoutProbe>(File.ReadAllText(layoutPath), jsonSettings)
                  ?? throw new InvalidOperationException("station-layout.json did not parse");

Add("config is enabled", () => Assert.True(shipped.Enabled));
Add("config keeps 12 stacks / 25 kg", () =>
{
    Assert.Equal(12, shipped.Limits.MaxStacks);
    Assert.Near(25, shipped.Limits.MaxWeightKg);
});
Add("config pricing matches the design", () =>
{
    Assert.Near(0.10, shipped.Pricing.Rate);
    Assert.Equal(200L, shipped.Pricing.Minimum);
    Assert.Equal(5_000L, shipped.Pricing.Maximum);
});
Add("config has four loss bands ending at 15%", () =>
{
    Assert.Equal(4, shipped.Loss.Bands.Count);
    Assert.Near(0.05, shipped.Loss.Bands[0].Rate);
    Assert.Near(0.15, shipped.Loss.Bands[3].Rate);
    Assert.Near(0.20, shipped.Loss.MaximumRate);
});
Add("config escort ids and prices are fixed", () =>
{
    Assert.Equal(368, shipped.Escorts.Scavenger.ItemId);
    Assert.Equal(1_000L, shipped.Escorts.Scavenger.Money);
    Assert.Equal(4L, shipped.Escorts.Scavenger.ItemAmount);
    Assert.Near(0.6, shipped.Escorts.Scavenger.LossFactor);
    Assert.Equal(890, shipped.Escorts.Mercenary.ItemId);
    Assert.Equal(2_000L, shipped.Escorts.Mercenary.Money);
    Assert.Near(0.3, shipped.Escorts.Mercenary.LossFactor);
});
Add("config protects quest, key, cash and containers", () =>
{
    Assert.True(shipped.Protection.ProtectedTags.Contains("Quest"));
    Assert.True(shipped.Protection.ProtectedTags.Contains("Key"));
    Assert.True(shipped.Protection.ProtectedTags.Contains("Cash"));
    Assert.True(shipped.Protection.ProtectedTags.Contains("Continer"));
    Assert.True(shipped.Protection.ProtectedTypeIds.Contains(451));
    Assert.False(shipped.Protection.ProtectedTags.Contains("Equipment"));
});
Add("config safety gate and confirmation thresholds", () =>
{
    Assert.True(shipped.Safety.RequireInputGuard);
    Assert.Equal(10_000L, shipped.Safety.ConfirmAboveValue);
    Assert.True(shipped.Safety.ConfirmWhenSingleStack);
});
Add("config produces the designed quote for 10,000", () =>
{
    var quote = ShipmentRules.Quote(10_000, EscortOption.None, shipped.Pricing, shipped.Escorts, shipped.Loss);
    Assert.Equal(1_000L, quote.Fee);
    Assert.Near(0.08, quote.FinalLossRate);
});
Add("station layout covers warehouse and farm", () =>
{
    Assert.Equal(2, layoutProbe.Sites.Count);
    Assert.True(layoutProbe.Sites.Any(site => site.MapId == "Level_HiddenWarehouse_Main" && site.StationsPerRaid == 1));
    Assert.True(layoutProbe.Sites.Any(site => site.MapId == "Level_Farm_Main" && site.StationsPerRaid == 2));
});
Add("station layout has enough distinct anchors", () =>
{
    Assert.True(layoutProbe.Sites.All(site => site.Candidates.Count >= site.StationsPerRaid));
    Assert.Equal(layoutProbe.Sites.Sum(site => site.Candidates.Count),
        layoutProbe.Sites.SelectMany(site => site.Candidates).Select(candidate => candidate.Id).Distinct().Count());
});
Add("station layout keeps scene names used by the game", () =>
{
    Assert.True(layoutProbe.Sites.Any(site => site.SceneName == "Level_HiddenWarehouse"));
    Assert.True(layoutProbe.Sites.Any(site => site.SceneName == "Level_Farm_01"));
});

// ---------------------------------------------------------------- output
var failed = 0;
foreach (var (name, run) in cases)
{
    try
    {
        run();
        Console.WriteLine("PASS " + name);
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine("FAIL " + name + " :: " + exception.Message);
    }
}
Console.WriteLine($"\n{cases.Count - failed}/{cases.Count} passed");
return failed == 0 ? 0 : 1;

static ItemFacts Item(
    int typeId,
    int unitValue,
    int count = 1,
    double weight = 1,
    bool equipped = false,
    bool canDrop = true,
    bool container = false,
    string name = "测试物品",
    params string[] tags) => new ItemFacts
{
    InstanceId = typeId * 1000 + count,
    TypeId = typeId,
    Name = name,
    StackCount = count,
    UnitValue = unitValue,
    TotalWeight = weight,
    IsEquipped = equipped,
    CanDrop = canDrop,
    HasContainerInventory = container,
    Tags = tags
};

static StationCandidate Candidate(string id, double x, double z) => new StationCandidate
{
    Id = id,
    Name = id,
    MapId = "map",
    SceneName = "scene",
    X = x,
    Z = z
};

sealed class ConfigProbe
{
    public bool Enabled { get; set; }
    public ShipmentLimits Limits { get; set; } = new ShipmentLimits();
    public PricingRules Pricing { get; set; } = new PricingRules();
    public LossSettings Loss { get; set; } = new LossSettings();
    public EscortSettings Escorts { get; set; } = new EscortSettings();
    public ProtectionRules Protection { get; set; } = new ProtectionRules();
    public SafetyProbe Safety { get; set; } = new SafetyProbe();
}

sealed class SafetyProbe
{
    public bool RequireInputGuard { get; set; }
    public long ConfirmAboveValue { get; set; }
    public bool ConfirmWhenSingleStack { get; set; }
}

sealed class LayoutProbe
{
    public List<SiteProbe> Sites { get; set; } = new List<SiteProbe>();
}

sealed class SiteProbe
{
    public string MapId { get; set; } = "";
    public string SceneName { get; set; } = "";
    public int StationsPerRaid { get; set; }
    public List<AnchorProbe> Candidates { get; set; } = new List<AnchorProbe>();
}

sealed class AnchorProbe
{
    public string Id { get; set; } = "";
    public double X { get; set; }
    public double Z { get; set; }
}

static class Assert
{
    public static void True(bool value, string? message = null)
    {
        if (!value) throw new InvalidOperationException(message ?? "expected true");
    }

    public static void False(bool value, string? message = null)
    {
        if (value) throw new InvalidOperationException(message ?? "expected false");
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"expected {expected}, got {actual}");
    }

    public static void Near(double expected, double actual, double tolerance = 0.000001)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"expected {expected}, got {actual}");
    }
}
