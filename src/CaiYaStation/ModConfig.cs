using System;
using System.Collections.Generic;
using System.IO;
using CaiYaStation.Domain;
using Newtonsoft.Json;
using UnityEngine.InputSystem;

namespace CaiYaStation
{
    internal sealed class PlacementSettings
    {
        public double MinimumSeparation { get; set; } = 15.0;
        public double MinimumExtractionDistance { get; set; } = 40.0;
        public double MaximumExtractionDistance { get; set; } = 80.0;
        public double MinimumTeleporterDistance { get; set; } = 15.0;
        public double MaximumSlopeDegrees { get; set; } = 35.0;
        public List<double> SearchRings { get; set; } = new List<double> { 8, 12, 16, 22 };
        public int SearchAngles { get; set; } = 16;
    }

    internal sealed class UiSettings
    {
        public int PanelWidth { get; set; } = 920;
        public int PanelHeight { get; set; } = 700;
        public double LabelDistance { get; set; } = 80;
        public bool ShowWorldLabels { get; set; } = true;
        public bool UnlockCursor { get; set; } = true;
    }

    internal sealed class KeySettings
    {
        public string Diagnostics { get; set; } = "F8";
    }

    internal sealed class SafetySettings
    {
        public bool RequireInputGuard { get; set; } = true;
        public long ConfirmAboveValue { get; set; } = 10000;
        public bool ConfirmWhenSingleStack { get; set; } = true;
    }

    internal sealed class ModConfig
    {
        public bool Enabled { get; set; } = true;
        public ShipmentLimits Limits { get; set; } = new ShipmentLimits();
        public PricingRules Pricing { get; set; } = new PricingRules();
        public LossSettings Loss { get; set; } = new LossSettings();
        public EscortSettings Escorts { get; set; } = new EscortSettings();
        public ProtectionRules Protection { get; set; } = new ProtectionRules();
        public PlacementSettings Placement { get; set; } = new PlacementSettings();
        public UiSettings Ui { get; set; } = new UiSettings();
        public KeySettings Keys { get; set; } = new KeySettings();
        public SafetySettings Safety { get; set; } = new SafetySettings();

        public Key DiagnosticsKey { get; private set; } = Key.F8;

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            // Lists such as Loss.Bands have design defaults in their constructors; replace them
            // with the JSON content instead of appending (which doubled the bands).
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        public static ModConfig Load(string path, Action<string> log)
        {
            var config = new ModConfig();
            try
            {
                if (File.Exists(path))
                {
                    var parsed = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(path), SerializerSettings);
                    if (parsed != null) config = parsed;
                }
                else
                {
                    log("config.json not found, using defaults");
                }
            }
            catch (Exception exception)
            {
                log("config.json invalid, using defaults: " + exception.Message);
            }
            config.Normalize(log);
            return config;
        }

        private void Normalize(Action<string> log)
        {
            Limits ??= new ShipmentLimits();
            Pricing ??= new PricingRules();
            Loss ??= new LossSettings();
            Escorts ??= new EscortSettings();
            Protection ??= new ProtectionRules();
            Placement ??= new PlacementSettings();
            Ui ??= new UiSettings();
            Keys ??= new KeySettings();
            Safety ??= new SafetySettings();

            if (Limits.MaxStacks < 1 || Limits.MaxStacks > 60)
            {
                log("Limits.MaxStacks out of range, reset to 12");
                Limits.MaxStacks = 12;
            }
            if (Limits.MaxWeightKg <= 0 || Limits.MaxWeightKg > 500)
            {
                log("Limits.MaxWeightKg out of range, reset to 25");
                Limits.MaxWeightKg = 25;
            }
            if (Pricing.Rate < 0 || Pricing.Rate > 1)
            {
                log("Pricing.Rate out of range, reset to 0.10");
                Pricing.Rate = 0.10;
            }
            if (Pricing.Minimum < 0 || Pricing.Maximum < Pricing.Minimum)
            {
                log("Pricing bounds invalid, reset to 200..5000");
                Pricing.Minimum = 200;
                Pricing.Maximum = 5000;
            }
            if (Loss.Bands == null || Loss.Bands.Count == 0)
            {
                log("Loss.Bands missing, using design defaults");
                Loss.Bands = new LossSettings().Bands;
            }
            Loss.MaximumRate = Math.Min(0.20, Loss.MaximumRate <= 0 ? 0.20 : Loss.MaximumRate);
            Escorts.None ??= new EscortRules { Option = EscortOption.None, Name = "无", LossFactor = 1.0 };
            Escorts.Scavenger ??= new EscortRules();
            Escorts.Mercenary ??= new EscortRules();
            Escorts.None.Option = EscortOption.None;
            Escorts.Scavenger.Option = EscortOption.Scavenger;
            Escorts.Mercenary.Option = EscortOption.Mercenary;
            if (string.IsNullOrWhiteSpace(Escorts.Scavenger.Name)) Escorts.Scavenger.Name = "拾荒者团";
            if (string.IsNullOrWhiteSpace(Escorts.Mercenary.Name)) Escorts.Mercenary.Name = "雇佣兵团";
            if (Escorts.Scavenger.ItemId <= 0) Escorts.Scavenger.ItemId = 368;
            if (Escorts.Mercenary.ItemId <= 0) Escorts.Mercenary.ItemId = 890;
            if (Placement.MinimumSeparation < 0) Placement.MinimumSeparation = 15;
            if (Placement.MinimumExtractionDistance < 0) Placement.MinimumExtractionDistance = 40;
            if (Placement.MaximumExtractionDistance < Placement.MinimumExtractionDistance)
                Placement.MaximumExtractionDistance = Placement.MinimumExtractionDistance + 40;
            if (Placement.SearchRings == null || Placement.SearchRings.Count == 0)
                Placement.SearchRings = new List<double> { 8, 12, 16, 22 };
            if (Placement.SearchAngles < 4) Placement.SearchAngles = 16;
            if (Ui.PanelWidth < 820 || Ui.PanelWidth > 1600)
            {
                log("Ui.PanelWidth out of range, reset to 920");
                Ui.PanelWidth = 920;
            }
            if (Ui.PanelHeight < 660 || Ui.PanelHeight > 1000)
            {
                log("Ui.PanelHeight out of range, reset to 700");
                Ui.PanelHeight = 700;
            }
            if (Ui.LabelDistance < 10 || Ui.LabelDistance > 200) Ui.LabelDistance = 80;
            if (Safety.ConfirmAboveValue < 0) Safety.ConfirmAboveValue = 10000;
            if (Safety.ConfirmAboveValue > 10_000_000) Safety.ConfirmAboveValue = 10_000_000;

            if (!Enum.TryParse<Key>(Keys.Diagnostics, true, out var diagnostics))
            {
                log("Keys.Diagnostics invalid (" + Keys.Diagnostics + "), using F8");
                diagnostics = Key.F8;
            }
            DiagnosticsKey = diagnostics;
        }

        public EscortRules Escort(EscortOption option) => Escorts.For(option);
    }

    internal sealed class StationAnchor
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "菜鸭驿站";
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Yaw { get; set; }
    }

    internal sealed class StationMap
    {
        public string MapId { get; set; } = "";
        public string SceneName { get; set; } = "";
        public int StationsPerRaid { get; set; } = 1;
        public List<StationAnchor> Candidates { get; set; } = new List<StationAnchor>();

        public StationCandidate ToCandidate(StationAnchor anchor) => new StationCandidate
        {
            Id = anchor.Id,
            Name = string.IsNullOrWhiteSpace(anchor.Name) ? "菜鸭驿站" : anchor.Name,
            MapId = MapId,
            SceneName = SceneName,
            X = anchor.X,
            Y = anchor.Y,
            Z = anchor.Z,
            Yaw = anchor.Yaw
        };
    }

    internal sealed class StationLayout
    {
        public int SchemaVersion { get; set; } = 1;
        public double MinimumSeparation { get; set; } = 15;
        public List<StationMap> Sites { get; set; } = new List<StationMap>();

        public static StationLayout Load(string path, Action<string> log)
        {
            try
            {
                if (!File.Exists(path))
                {
                    log("station-layout.json not found; stations disabled");
                    return new StationLayout();
                }
                var layout = JsonConvert.DeserializeObject<StationLayout>(File.ReadAllText(path)) ?? new StationLayout();
                layout.Sites ??= new List<StationMap>();
                foreach (var site in layout.Sites)
                {
                    site.Candidates ??= new List<StationAnchor>();
                    if (site.StationsPerRaid < 0) site.StationsPerRaid = 0;
                    if (site.StationsPerRaid > 4) site.StationsPerRaid = 4;
                }
                return layout;
            }
            catch (Exception exception)
            {
                log("station-layout.json invalid; stations disabled: " + exception.Message);
                return new StationLayout();
            }
        }

        public StationMap? For(string mapId, string sceneName)
        {
            foreach (var site in Sites)
            {
                if (string.Equals(site.MapId, mapId, StringComparison.Ordinal) &&
                    string.Equals(site.SceneName, sceneName, StringComparison.Ordinal))
                    return site;
            }
            return null;
        }

        public StationMap? ForMap(string mapId)
        {
            foreach (var site in Sites)
            {
                if (string.Equals(site.MapId, mapId, StringComparison.Ordinal)) return site;
            }
            return null;
        }
    }
}
