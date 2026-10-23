using System;
using System.Collections.Generic;
using CaiYaStation.Domain;
using UnityEngine;

namespace CaiYaStation
{
    /// <summary>One backpack stack as the panel shows it.</summary>
    internal sealed class ShipmentRowView
    {
        internal int InstanceId;
        internal string Name = "";
        internal int StackCount = 1;
        internal long UnitValue;
        internal long GoodsValue;
        internal double WeightKg;
        internal int Quality;
        internal Sprite? Icon;
        internal string Description = "";
        internal string? BlockedReason;
        internal bool Selected;

        internal bool Eligible => BlockedReason == null;

        internal string CountText => StackCount > 1 ? "×" + StackCount : "1";
    }

    /// <summary>One escort option card. Mirrors EscortRules plus the player's carried items.</summary>
    internal sealed class EscortCardView
    {
        internal EscortOption Option;
        internal string Title = "";
        internal string Cost = "";
        internal string Effect = "";
        internal bool Affordable = true;
        internal bool Selected;
        internal long Carried;
        internal long Needed;
        internal string? Missing;
    }

    internal enum FooterStatusKind
    {
        Hint,
        Warning,
        Danger
    }

    /// <summary>
    /// Everything the shipment panel draws, with no reference to live game objects. The live
    /// controller fills this from the runtime; the offline preview fills it with fixtures.
    /// </summary>
    internal sealed class ShipmentView
    {
        internal string Title = "菜鸭驿站";
        internal string StatusLabel = "营业中";
        internal bool StationOpen = true;
        internal string Subtitle = "";
        internal Sprite? StationIcon;

        internal long Money;
        internal long Cash;

        internal int MaxStacks = 12;
        internal double MaxWeightKg = 25;

        internal readonly List<ShipmentRowView> Rows = new List<ShipmentRowView>();
        internal readonly List<ShipmentRowView> Manifest = new List<ShipmentRowView>();

        internal int SelectedStacks;
        internal long GoodsValue;
        internal double SelectedWeight;
        internal FeeQuote Quote = new FeeQuote();

        internal readonly List<EscortCardView> Escorts = new List<EscortCardView>();

        /// <summary>Comes from the config, so a tuned fee rate never leaves the card lying.</summary>
        internal string FeeRateText = "运费 10%";
        internal string LossCapText = "上限 30%件 / 50%值";

        internal string Summary = "";
        internal string Status = "";
        internal FooterStatusKind StatusKind = FooterStatusKind.Hint;
        internal bool CanShip;
        internal bool Shipping;

        internal float ListScroll;
        internal float ManifestScroll;

        internal string CapacityText =>
            $"已选 {SelectedStacks}/{MaxStacks} 格　{SelectedWeight:0.##}/{MaxWeightKg:0.#} kg";
    }

    internal enum PanelHitKind
    {
        Row,
        ManifestRow,
        Escort,
        SelectAll,
        Clear,
        Confirm,
        Close
    }

    internal sealed class PanelHit
    {
        internal PanelHitKind Kind;
        internal string Id = "";
        internal int InstanceId;
        internal EscortOption Escort;
        internal Rect Rect;
    }

    internal sealed class PanelRenderResult
    {
        internal readonly List<PanelHit> Hits = new List<PanelHit>();
        internal ShipmentRowView? TooltipRow;
        internal Rect HoveredRowRect;
        internal Rect TooltipRect;
    }
}
