using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CaiYaStation;
using CaiYaStation.Domain;
using UnityEngine;

/// <summary>
/// Headless check + preview generator for the shipment panel.
///
/// It compiles the real PanelLayout/PanelRenderer code, runs the panel against fixtures, and
/// records every paint command. From that recording it
///   1. asserts that no two sibling layout bands overlap and everything stays inside the panel,
///   2. asserts that no two text boxes collide when no tooltip is shown,
///   3. writes the paint commands to JSON so tools/render_panel_preview.py can draw a PNG
///      that is generated from the same code the game runs.
/// </summary>
internal static class Program
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static int Main(string[] args)
    {
        var root = ResolveRoot();
        var outDir = Path.Combine(root, "artifacts", "inspection");
        Directory.CreateDirectory(outDir);
        var failures = new List<string>();

        CheckLayouts(failures);
        CheckReceipt(failures);
        if (failures.Count == 0)
            Console.WriteLine("Receipt layout check passed: 6 sizes, chips fit their three lines, no overlap.");

        var defaultPainter = new RecordPainter();
        var defaultResult = RenderScene(defaultPainter, BuildFixture(false, false), new Vector2(-100f, -100f), 1280f, 800f);
        CheckTextCollisions(defaultPainter, failures);
        CheckHits(defaultResult, PanelLayout.Compute(PanelLayout.PanelRect(1280f, 800f, 920f, 700f)), failures, "1280x800");
        File.WriteAllText(Path.Combine(outDir, "panel-default.json"),
            BuildDocument(defaultPainter, 1280f, 800f), new UTF8Encoding(false));

        var hoverPainter = new RecordPainter();
        var hoverLayout = PanelLayout.Compute(PanelLayout.PanelRect(1280f, 800f, 920f, 700f));
        var hoverRow = hoverLayout.ListRowRect(3, 0f);
        var hoverResult = RenderScene(hoverPainter, BuildFixture(false, false),
            new Vector2(hoverRow.x + 120f, hoverRow.y + hoverRow.height * 0.5f), 1280f, 800f);
        CheckTooltip(hoverResult, hoverLayout, failures);
        File.WriteAllText(Path.Combine(outDir, "panel-hover.json"),
            BuildDocument(hoverPainter, 1280f, 800f), new UTF8Encoding(false));

        var filledPainter = new RecordPainter();
        RenderScene(filledPainter, BuildFixture(true, true), new Vector2(-100f, -100f), 1280f, 800f);
        File.WriteAllText(Path.Combine(outDir, "panel-filled.json"),
            BuildDocument(filledPainter, 1280f, 800f), new UTF8Encoding(false));

        var smallPainter = new RecordPainter();
        RenderScene(smallPainter, BuildFixture(true, true), new Vector2(-100f, -100f), 900f, 700f);
        File.WriteAllText(Path.Combine(outDir, "panel-small.json"),
            BuildDocument(smallPainter, 900f, 700f), new UTF8Encoding(false));

        if (failures.Count > 0)
        {
            Console.Error.WriteLine("Panel layout check failed:");
            foreach (var failure in failures) Console.Error.WriteLine("  - " + failure);
            return 1;
        }

        Console.WriteLine("Panel layout check passed: 4 sizes, 4 scenes, no overlapping bands or text.");
        Console.WriteLine("Wrote " + Path.Combine(outDir, "panel-*.json"));
        return 0;
    }

    private static PanelRenderResult RenderScene(RecordPainter painter, ShipmentView view, Vector2 mouse,
        float width = 920f, float height = 700f)
    {
        var palette = PanelPalette.Default;
        var layout = PanelLayout.Compute(PanelLayout.PanelRect(width, height, 920f, 700f));
        var renderer = new PanelRenderer(palette, painter);
        return renderer.Draw(view, layout, mouse, width, height);
    }

    private static void CheckHits(PanelRenderResult result, PanelLayout layout, List<string> failures, string tag)
    {
        foreach (var hit in result.Hits)
        {
            if (!Inside(layout.Panel, hit.Rect, 0.6f))
                failures.Add(tag + ": hit " + hit.Id + " " + Describe(hit.Rect) + " escapes the panel");
        }
        if (result.Hits.Count < 10) failures.Add(tag + ": only " + result.Hits.Count + " hit targets collected");
    }

    private static void CheckTooltip(PanelRenderResult result, PanelLayout layout, List<string> failures)
    {
        if (result.TooltipRow == null)
        {
            failures.Add("hover scene: no row was hovered");
            return;
        }
        if (result.TooltipRect.width <= 0f)
        {
            failures.Add("hover scene: tooltip was not placed");
            return;
        }
        if (!Inside(layout.Panel, result.TooltipRect, 0.6f))
            failures.Add("hover scene: tooltip " + Describe(result.TooltipRect) + " escapes the panel");
        if (Overlaps(result.TooltipRect, result.HoveredRowRect, 1f))
            failures.Add("hover scene: tooltip covers the row it describes");
    }

    private static void CheckLayouts(List<string> failures)
    {
        var sizes = new (float W, float H)[]
        {
            (920f, 700f), (1024f, 720f), (900f, 700f), (960f, 732f), (1000f, 760f)
        };

        foreach (var size in sizes)
        {
            var panel = PanelLayout.PanelRect(size.W, size.H, 920f, 700f);
            var l = PanelLayout.Compute(panel);
            var tag = size.W.ToString("0", Inv) + "x" + size.H.ToString("0", Inv);

            if (!Inside(new Rect(0f, 0f, size.W, size.H), l.Panel, 0.5f))
                failures.Add(tag + ": panel is outside the screen");

            var named = new List<KeyValuePair<string, Rect>>();
            l.CollectRects(named);
            foreach (var entry in named)
            {
                if (entry.Key == "Panel") continue;
                if (!Inside(l.Panel, entry.Value, 0.6f))
                    failures.Add(tag + ": " + entry.Key + " " + Describe(entry.Value) + " escapes the panel");
            }

            ExpectNoOverlap(failures, tag, "Header", l.Header, "Left", l.Left);
            ExpectNoOverlap(failures, tag, "Header", l.Header, "Right", l.Right);
            ExpectNoOverlap(failures, tag, "Header", l.Header, "Body", l.Body);
            ExpectNoOverlap(failures, tag, "Left", l.Left, "Right", l.Right);
            ExpectNoOverlap(failures, tag, "ListTitle", l.ListTitle, "ListCard", l.ListCard);
            ExpectNoOverlap(failures, tag, "ListCard", l.ListCard, "Footer", l.Footer);
            ExpectNoOverlap(failures, tag, "RailCard", l.RailCard, "Footer", l.Footer);
            ExpectNoOverlap(failures, tag, "Body", l.Body, "Footer", l.Footer);
            ExpectNoOverlap(failures, tag, "Manifest", l.Manifest, "Cost", l.Cost);
            ExpectNoOverlap(failures, tag, "Cost", l.Cost, "Escort", l.Escort);
            ExpectNoOverlap(failures, tag, "Manifest", l.Manifest, "Escort", l.Escort);
            ExpectNoOverlap(failures, tag, "HeaderTitle", l.HeaderTitle, "HeaderBadge", l.HeaderBadge);
            ExpectNoOverlap(failures, tag, "HeaderSubtitle", l.HeaderSubtitle, "HeaderClose", l.HeaderClose);
            ExpectNoOverlap(failures, tag, "ListTitle", l.ListTitle, "ListCapacity", l.ListCapacity);

            ExpectInside(failures, tag, "ListView", l.ListView, l.ListCard);
            ExpectInside(failures, tag, "ListColumns", l.ListColumns, l.ListCard);
            ExpectInside(failures, tag, "ListHint", l.ListHint, l.ListCard);
            ExpectInside(failures, tag, "ManifestView", l.ManifestView, l.Manifest);
            ExpectInside(failures, tag, "ManifestTitle", l.ManifestTitle, l.Manifest);
            ExpectInside(failures, tag, "ManifestHint", l.ManifestHint, l.Manifest);
            ExpectInside(failures, tag, "EscortTitle", l.EscortTitle, l.Escort);
            ExpectInside(failures, tag, "Manifest", l.Manifest, l.RailCard);
            ExpectInside(failures, tag, "Cost", l.Cost, l.RailCard);
            ExpectInside(failures, tag, "Escort", l.Escort, l.RailCard);

            var buttons = new[]
            {
                ("SelectAll", l.SelectAllButton), ("Clear", l.ClearButton),
                ("Confirm", l.ConfirmButton), ("Close", l.CloseButton)
            };
            for (var i = 0; i < buttons.Length; i++)
            {
                ExpectInside(failures, tag, buttons[i].Item1, buttons[i].Item2, l.Footer);
                for (var j = i + 1; j < buttons.Length; j++)
                    ExpectNoOverlap(failures, tag, buttons[i].Item1, buttons[i].Item2, buttons[j].Item1, buttons[j].Item2);
            }

            for (var i = 0; i < 3; i++)
            {
                var card = l.EscortCardRect(i);
                ExpectInside(failures, tag, "EscortCard" + i, card, l.Escort);
                if (i > 0)
                    ExpectNoOverlap(failures, tag, "EscortCard" + (i - 1), l.EscortCardRect(i - 1), "EscortCard" + i, card);
            }

            // Chart the rail's vertical budget explicitly: the old panel let this go negative and
            // painted the third escort card on top of the alert block.
            var railBottom = l.Manifest.y + l.Manifest.height + PanelLayout.RailGap
                             + PanelLayout.CostHeight + PanelLayout.RailGap + PanelLayout.EscortHeight;
            if (railBottom > l.RailCard.yMax + 0.6f)
                failures.Add(tag + ": rail contents end at " + railBottom.ToString("0", Inv)
                             + " but the card ends at " + l.RailCard.yMax.ToString("0", Inv));
        }
    }

    /// <summary>
    /// Receipt sheet: every band measured from the chip strip and the footer must stay ordered,
    /// and each chip must be tall enough for its three lines (label 16 + value 24 + sub 16 = 64).
    /// </summary>
    private static void CheckReceipt(List<string> failures)
    {
        var sizes = new (float W, float H)[]
        {
            (2560f, 1440f), (1920f, 1080f), (1600f, 900f), (1280f, 800f), (1024f, 768f), (900f, 700f)
        };

        foreach (var size in sizes)
        {
            var r = ReceiptLayout.Compute(size.W, size.H);
            var tag = "receipt " + size.W.ToString("0", Inv) + "x" + size.H.ToString("0", Inv);

            if (!Inside(new Rect(0f, 0f, size.W, size.H), r.Panel, 0.5f))
                failures.Add(tag + ": sheet is outside the screen");

            var chips = new[] { r.ChipOne, r.ChipTwo, r.ChipThree, r.ChipFour };
            for (var i = 0; i < chips.Length; i++)
            {
                ExpectInside(failures, tag, "Chip" + i, chips[i], r.Panel);
                if (chips[i].height + 0.5f < ReceiptLayout.ChipContentBottom)
                    failures.Add(tag + ": Chip" + i + " is " + chips[i].height.ToString("0.0", Inv)
                                 + " high but its three lines need " + ReceiptLayout.ChipContentBottom.ToString("0.0", Inv));
                if (chips[i].width < 120f)
                    failures.Add(tag + ": Chip" + i + " is only " + chips[i].width.ToString("0.0", Inv) + " wide");
                if (i > 0) ExpectNoOverlap(failures, tag, "Chip" + (i - 1), chips[i - 1], "Chip" + i, chips[i]);
            }

            ExpectInside(failures, tag, "HeaderTitle", r.HeaderTitle, r.Panel);
            ExpectInside(failures, tag, "HeaderBadge", r.HeaderBadge, r.Panel);
            ExpectNoOverlap(failures, tag, "HeaderTitle", r.HeaderTitle, "HeaderBadge", r.HeaderBadge);
            ExpectInside(failures, tag, "HeaderClose", r.HeaderClose, r.Panel);
            ExpectNoOverlap(failures, tag, "HeaderSubtitle", r.HeaderSubtitle, "HeaderClose", r.HeaderClose);
            ExpectNoOverlap(failures, tag, "Header", r.Header, "Chip0", chips[0]);

            ExpectInside(failures, tag, "Delivered", r.Delivered, r.Panel);
            ExpectInside(failures, tag, "Lost", r.Lost, r.Panel);
            ExpectNoOverlap(failures, tag, "Delivered", r.Delivered, "Lost", r.Lost);
            ExpectNoOverlap(failures, tag, "Chip0", chips[0], "Delivered", r.Delivered);
            ExpectNoOverlap(failures, tag, "Delivered", r.Delivered, "Headline", r.Headline);
            ExpectNoOverlap(failures, tag, "Headline", r.Headline, "Flavor", r.Flavor);
            ExpectNoOverlap(failures, tag, "Flavor", r.Flavor, "Button", r.Button);
            ExpectInside(failures, tag, "Headline", r.Headline, r.Panel);
            ExpectInside(failures, tag, "Flavor", r.Flavor, r.Panel);
            ExpectInside(failures, tag, "Button", r.Button, r.Panel);

            if (r.ListHeight + 0.5f < ReceiptLayout.ListMinHeight)
                failures.Add(tag + ": list cards are " + r.ListHeight.ToString("0.0", Inv)
                             + " high, below the " + ReceiptLayout.ListMinHeight.ToString("0.0", Inv) + " floor");
            if (ReceiptLayout.ListCapacity(r.ListHeight) < 1 && size.H >= 420f)
                failures.Add(tag + ": list cards fit no row at all");
        }
    }


    private static void CheckTextCollisions(RecordPainter painter, List<string> failures)
    {
        var texts = painter.TextRects();
        foreach (var record in texts)
        {
            if (record.Wrap) continue;
            var needed = ApproxTextWidth(record.Text, record.Size);
            if (needed > record.Rect.width + 1.5f)
            {
                failures.Add("text clipped: \"" + record.Text + "\" needs "
                             + needed.ToString("0", CultureInfo.InvariantCulture) + " but has "
                             + record.Rect.width.ToString("0", CultureInfo.InvariantCulture));
            }
        }
        for (var i = 0; i < texts.Count; i++)
        {
            if (texts[i].Extent.width < 1f || texts[i].Extent.height < 1f) continue;
            for (var j = i + 1; j < texts.Count; j++)
            {
                if (texts[j].Extent.width < 1f || texts[j].Extent.height < 1f) continue;
                if (!Overlaps(texts[i].Extent, texts[j].Extent, 1f)) continue;
                failures.Add("text overlap: \"" + texts[i].Text + "\" " + Describe(texts[i].Extent)
                             + " vs \"" + texts[j].Text + "\" " + Describe(texts[j].Extent));
            }
        }
    }

    private static float ApproxTextWidth(string text, int size)
    {
        var width = 0f;
        foreach (var ch in text) width += ch < 128 ? size * 0.55f : size * 1.0f;
        return width;
    }

    // ------------------------------------------------------------------ fixtures

    private static ShipmentView BuildFixture(bool selectItems, bool scavenger)
    {
        var view = new ShipmentView
        {
            Title = "菜鸭驿站",
            StatusLabel = "营业中",
            StationOpen = true,
            Subtitle = "农场镇　·　送达：地堡马蜂自提点　·　每站每局一次",
            Money = 1749,
            Cash = 256874,
            MaxStacks = 12,
            MaxWeightKg = 25
        };

        view.Rows.Add(Row(1, "土豆", 1, 230, 0.18, 1, false));
        view.Rows.Add(Row(2, "焗豆罐头", 3, 102, 1.17, 1, false));
        view.Rows.Add(Row(3, "焗豆罐头", 3, 102, 1.17, 1, false));
        view.Rows.Add(Row(4, "焗豆罐头", 2, 102, 0.78, 1, false));
        view.Rows.Add(Row(5, "苏打水", 1, 102, 0.37, 1, false));
        view.Rows.Add(Row(6, "瓶装水", 1, 375, 0.5, 1, false));
        view.Rows.Add(Row(7, "瓶装水", 1, 375, 0.5, 1, false));
        view.Rows.Add(Row(8, "瓶装水", 1, 375, 0.5, 1, false));
        view.Rows.Add(Row(9, "瓶装水", 1, 375, 0.5, 1, false));
        view.Rows.Add(Row(10, "瓶装水", 1, 375, 0.5, 1, false));
        view.Rows.Add(Row(11, "瓶装水", 1, 375, 0.5, 1, false));
        view.Rows.Add(Row(12, "止血绷带", 3, 80, 0.15, 2, false));
        view.Rows.Add(Row(13, "五级防弹衣", 1, 5827, 5.88, 7, false));
        view.Rows.Add(Row(14, "狗牌", 1, 35, 0.18, 1, false));
        view.Rows.Add(Row(15, "任务信件", 1, 60, 0.1, 1, true));

        if (selectItems)
        {
            foreach (var row in view.Rows)
            {
                if (row.InstanceId == 1 || row.InstanceId == 2 || row.InstanceId == 13)
                {
                    row.Selected = true;
                    view.Manifest.Add(row);
                    view.SelectedStacks++;
                    view.GoodsValue += row.GoodsValue;
                    view.SelectedWeight += row.WeightKg;
                }
            }
        }

        var goods = view.GoodsValue;
        var baseRate = goods <= 0 ? 0.05 : goods <= 10000 ? 0.08 : 0.12;
        var finalRate = scavenger ? Math.Max(0.02, baseRate * 0.6) : baseRate;
        var fee = goods <= 0 ? 200 : Math.Max(200, Math.Min(5000, (long)Math.Round(goods * 0.10, MidpointRounding.AwayFromZero)));
        var escortMoney = scavenger ? 1000 : 0;
        view.Quote = new FeeQuote
        {
            GoodsValue = goods,
            Fee = fee,
            BaseLossRate = baseRate,
            FinalLossRate = finalRate,
            Escort = scavenger ? EscortOption.Scavenger : EscortOption.None,
            EscortMoney = escortMoney
        };

        view.Escorts.Add(new EscortCardView
        {
            Option = EscortOption.None,
            Title = "不请护送",
            Cost = "免费",
            Effect = "损耗 " + Percent(baseRate),
            Affordable = true,
            Selected = !scavenger
        });
        view.Escorts.Add(new EscortCardView
        {
            Option = EscortOption.Scavenger,
            Title = "拾荒者团",
            Cost = "1,000 + 4 羽毛",
            Effect = "损耗 " + Percent(Math.Max(0.02, baseRate * 0.6)),
            Affordable = true,
            Selected = scavenger,
            Carried = 5,
            Needed = 4
        });
        view.Escorts.Add(new EscortCardView
        {
            Option = EscortOption.Mercenary,
            Title = "雇佣兵团",
            Cost = "2,000 + 4 狗牌",
            Effect = "损耗 " + Percent(Math.Max(0.02, baseRate * 0.3)),
            Affordable = false,
            Selected = false,
            Carried = 1,
            Needed = 4,
            Missing = "随身 1/4"
        });

        view.Summary = "已选 " + view.SelectedStacks + " 件　·　实扣 " + view.Quote.TotalUpfront.ToString("N0", Inv)
                       + "　·　预计总成本 " + view.Quote.ExpectedTotalCost.ToString("N0", Inv);
        if (view.SelectedStacks == 0)
        {
            view.StatusKind = FooterStatusKind.Danger;
            view.Status = "× 请选择要寄送的物品";
            view.CanShip = false;
        }
        else
        {
            view.StatusKind = FooterStatusKind.Warning;
            view.Status = "! 运费高于货值";
            view.CanShip = true;
        }
        return view;
    }

    private static ShipmentRowView Row(int id, string name, int stack, int unit, double weight, int quality, bool blocked)
    {
        return new ShipmentRowView
        {
            InstanceId = id,
            Name = name,
            StackCount = stack,
            UnitValue = unit,
            GoodsValue = (long)unit * stack,
            WeightKg = weight,
            Quality = quality,
            Description = blocked ? "" : name + "：搜刮来的物资，寄回地堡后可以在自提点领取。",
            BlockedReason = blocked ? "任务/钥匙/唯一物品不可寄送" : null
        };
    }

    private static string Percent(double value) => (value * 100.0).ToString("0.#", Inv) + "%";

    private static string Num(float value) => value.ToString("0.###", Inv);

    // ------------------------------------------------------------------ json

    private static string BuildDocument(RecordPainter painter, float width, float height)
    {
        var builder = new StringBuilder();
        var panel = PanelLayout.PanelRect(width, height, 920f, 700f);
        builder.Append("{\"size\":[").Append(Num(width)).Append(',').Append(Num(height)).Append("],\"panel\":[").Append(Num(panel.x)).Append(',').Append(Num(panel.y)).Append(',').Append(Num(panel.width)).Append(',').Append(Num(panel.height)).Append("],\"commands\":");
        builder.Append(painter.BuildJson());
        builder.Append('}');
        return builder.ToString();
    }

    // ------------------------------------------------------------------ assertions

    private static void ExpectNoOverlap(List<string> failures, string tag, string aName, Rect a, string bName, Rect b)
    {
        if (Overlaps(a, b, 0.6f))
            failures.Add(tag + ": " + aName + " " + Describe(a) + " overlaps " + bName + " " + Describe(b));
    }

    private static void ExpectInside(List<string> failures, string tag, string name, Rect inner, Rect outer)
    {
        if (!Inside(outer, inner, 0.6f))
            failures.Add(tag + ": " + name + " " + Describe(inner) + " is not inside " + Describe(outer));
    }

    private static bool Overlaps(Rect a, Rect b, float tolerance) =>
        a.x < b.xMax - tolerance && b.x < a.xMax - tolerance
        && a.y < b.yMax - tolerance && b.y < a.yMax - tolerance;

    private static bool Inside(Rect outer, Rect inner, float tolerance) =>
        inner.x >= outer.x - tolerance && inner.y >= outer.y - tolerance
        && inner.xMax <= outer.xMax + tolerance && inner.yMax <= outer.yMax + tolerance;

    private static string Describe(Rect rect) =>
        "(" + rect.x.ToString("0.#", Inv) + "," + rect.y.ToString("0.#", Inv) + ","
        + rect.width.ToString("0.#", Inv) + "x" + rect.height.ToString("0.#", Inv) + ")";

    private static string ResolveRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "CaiYaStation"))) return directory.FullName;
            directory = directory.Parent;
        }
        return Directory.GetCurrentDirectory();
    }
}

/// <summary>Paint-command recorder used by the headless layout check and the PNG preview.</summary>
internal sealed class RecordPainter : IPanelPainter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly PanelPalette _palette = PanelPalette.Default;
    private readonly List<string> _commands = new List<string>();
    private readonly List<Rect> _clips = new List<Rect>();
    private readonly List<TextRecord> _texts = new List<TextRecord>();

    internal List<TextRecord> TextRects() => _texts;

    internal readonly struct TextRecord
    {
        internal readonly Rect Extent;
        internal readonly Rect Rect;
        internal readonly string Text;
        internal readonly int Size;
        internal readonly bool Wrap;

        internal TextRecord(Rect extent, Rect rect, string text, int size, bool wrap)
        {
            Extent = extent;
            Rect = rect;
            Text = text;
            Size = size;
            Wrap = wrap;
        }
    }

    public void Solid(Rect rect, Color color)
    {
        _commands.Add("{\"t\":\"rect\",\"r\":" + R(rect) + ",\"radius\":0,\"fill\":" + C(color) + Clip() + "}");
    }

    public void Rounded(Rect rect, float radius, Color fill, Color? border, float borderWidth)
    {
        _commands.Add("{\"t\":\"rect\",\"r\":" + R(rect) + ",\"radius\":"
                      + N(radius) + ",\"fill\":" + C(fill) + ",\"stroke\":"
                      + (border.HasValue ? C(border.Value) : "null") + ",\"sw\":" + N(borderWidth) + Clip() + "}");
    }

    public void Text(Rect rect, string text, PaintText role, TextAnchor anchor = TextAnchor.MiddleLeft,
        Color? color = null, bool wrap = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        var spec = PaintStyles.Resolve(role, _palette);
        var extent = Extent(rect, text, spec.Size, anchor);
        if (_clips.Count > 0) extent = Intersect(extent, _clips[_clips.Count - 1]);
        _texts.Add(new TextRecord(extent, rect, text, spec.Size, wrap));
        _commands.Add("{\"t\":\"text\",\"r\":" + R(rect) + ",\"s\":" + S(text) + ",\"size\":" + spec.Size
                      + ",\"bold\":" + (spec.Bold ? "1" : "0") + ",\"color\":" + C(color ?? spec.Color)
                      + ",\"anchor\":" + S(Anchor(anchor)) + ",\"wrap\":" + (wrap ? "1" : "0") + Clip() + "}");
    }

    public void Icon(Rect rect, Sprite? sprite, string fallback, PaintText role, Color fallbackColor)
    {
        var spec = PaintStyles.Resolve(role, _palette);
        var letter = string.IsNullOrEmpty(fallback) ? "?" : fallback.Substring(0, 1);
        _commands.Add("{\"t\":\"icon\",\"r\":" + R(rect) + ",\"s\":" + S(letter) + ",\"size\":"
                      + Math.Max(10, spec.Size) + ",\"color\":" + C(fallbackColor) + Clip() + "}");
    }

    public void BeginClip(Rect rect) => _clips.Add(rect);

    public void EndClip()
    {
        if (_clips.Count > 0) _clips.RemoveAt(_clips.Count - 1);
    }

    public float Measure(string text, float width, PaintText role)
    {
        var spec = PaintStyles.Resolve(role, _palette);
        var used = 0f;
        foreach (var ch in text)
            used += ch < 128 ? spec.Size * 0.55f : spec.Size * 1.0f;
        var lines = Math.Max(1f, MathF.Ceiling(used / MathF.Max(16f, width)));
        return lines * (spec.Size * 1.42f + 2f);
    }

    internal string BuildJson() => "[" + string.Join(",", _commands) + "]";

    private static Rect Extent(Rect rect, string text, int size, TextAnchor anchor)
    {
        var width = Mathf.Min(rect.width, ApproxWidth(text, size));
        var x = anchor switch
        {
            TextAnchor.MiddleRight => rect.xMax - width,
            TextAnchor.UpperRight => rect.xMax - width,
            TextAnchor.MiddleCenter => rect.x + (rect.width - width) * 0.5f,
            TextAnchor.UpperCenter => rect.x + (rect.width - width) * 0.5f,
            _ => rect.x
        };
        return new Rect(x, rect.y, Mathf.Max(1f, width), rect.height);
    }

    private static float ApproxWidth(string text, int size)
    {
        var width = 0f;
        foreach (var ch in text) width += ch < 128 ? size * 0.55f : size * 1.0f;
        return width;
    }

    private static Rect Intersect(Rect a, Rect b)
    {
        var x = Mathf.Max(a.x, b.x);
        var y = Mathf.Max(a.y, b.y);
        var xMax = Mathf.Min(a.xMax, b.xMax);
        var yMax = Mathf.Min(a.yMax, b.yMax);
        return new Rect(x, y, Mathf.Max(0f, xMax - x), Mathf.Max(0f, yMax - y));
    }

    private string Clip()
    {
        if (_clips.Count == 0) return "";
        return ",\"c\":" + R(_clips[_clips.Count - 1]);
    }

    private static string Anchor(TextAnchor anchor) => anchor switch
    {
        TextAnchor.MiddleCenter => "mc",
        TextAnchor.MiddleRight => "mr",
        TextAnchor.UpperLeft => "ul",
        TextAnchor.UpperCenter => "uc",
        TextAnchor.UpperRight => "ur",
        TextAnchor.LowerLeft => "ll",
        _ => "ml"
    };

    private static string R(Rect rect) =>
        "[" + N(rect.x) + "," + N(rect.y) + "," + N(rect.width) + "," + N(rect.height) + "]";

    private static string C(Color color) =>
        "[" + N(color.r) + "," + N(color.g) + "," + N(color.b) + "," + N(color.a) + "]";

    private static string N(float value) => value.ToString("0.###", Inv);

    private static string S(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        builder.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (ch < 32) builder.Append(' ');
                    else builder.Append(ch);
                    break;
            }
        }
        builder.Append('"');
        return builder.ToString();
    }
}
