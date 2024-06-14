using UnityEngine;
using Verse;

namespace AICore;

// using static AICore.Dialog_Help;

public static class UX
{
    public const float ButtonHeight = 24f;

    #region Listing_Standard extensions
    /*public static void SmallLabel(this Listing_Standard list, string text, string tooltip = null)
    {
        var rect = list.GetRect(20f);
        var anchor = Text.Anchor;
        var font = Text.Font;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(rect, text);
        Text.Anchor = anchor;
        Text.Font = font;
        if (tooltip != null)
            TooltipHandler.TipRegion(rect, tooltip);
    }*/

    public static void Label(
        this Listing_Standard list,
        string hexColor,
        string textLeft,
        string textRight = "",
        string? tooltip = null,
        float gap = 6f
    )
    {
        Vector2 size = Text.CalcSize(textLeft);
        Rect rect = list.GetRect(size.y);
        if (tooltip != null)
            TooltipHandler.TipRegion(rect, tooltip);

        TextAnchor anchor = Text.Anchor;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(
            rect.LeftPartPixels(size.x),
            new TaggedString($"<color=#{hexColor}>{textLeft}</color>")
        );
        size = Text.CalcSize(textRight);
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(rect.RightPartPixels(size.x), textRight);
        Text.Anchor = anchor;
        if (gap > 0)
            list.Gap(gap);
    }

    // public static void TextField(
    //     this Listing_Standard list,
    //     ref string text,
    //     string label = null,
    //     bool isPassword = false,
    //     Action resetCallback = null,
    //     HelpType helpType = default,
    //     DialogSize dialogSize = DialogSize.Large
    // )
    // {
    //     var rect = list.GetRect(20f);
    //     if (label != null)
    //     {
    //         var anchor = Text.Anchor;
    //         var font = Text.Font;
    //         Text.Font = GameFont.Tiny;
    //         Text.Anchor = TextAnchor.UpperLeft;
    //         Widgets.Label(rect, label);
    //         Text.Anchor = anchor;
    //         Text.Font = font;

    //         if (Widgets.ButtonText(rect.RightPartPixels(24), "?"))
    //             Dialog_Help.Show(helpType, dialogSize);
    //     }
    //     if (isPassword && text != "")
    //     {
    //         if (list.ButtonText("Clear"))
    //             Find.WindowStack.Add(
    //                 Dialog_MessageBox.CreateConfirmation(
    //                     "Do you want to reset the key?",
    //                     resetCallback
    //                 )
    //             );
    //     }
    //     else
    //         text = list.TextEntry(text);
    // }

    public static void Slider(
        this Listing_Standard list,
        ref int value,
        int min,
        int max,
        Func<int, string> label,
        int logarithmic = 1,
        string? tooltip = null,
        GameFont? gameFont = null
    )
    {
        float input = logarithmic != 1 ? Mathf.Log(value, logarithmic) : value;
        float min2 = logarithmic != 1 ? Mathf.Log(min, logarithmic) : min;
        float max2 = logarithmic != 1 ? Mathf.Log(max, logarithmic) : max;
        HorizontalSlider(
            list.GetRect(22f),
            ref input,
            min2,
            max2,
            label == null
                ? null
                : label(
                    Mathf.FloorToInt(
                        (logarithmic != 1 ? Mathf.Pow(logarithmic, input) : input) + 0.001f
                    )
                ),
            1f,
            tooltip,
            gameFont
        );
        value = Mathf.FloorToInt(
            (logarithmic != 1 ? Mathf.Pow(logarithmic, input) : input) + 0.001f
        );
        list.Gap(2f);
    }

    public static void Slider(
        this Listing_Standard list,
        ref float value,
        float min,
        float max,
        Func<float, string> label,
        float roundTo = -1f,
        int logarithmic = 1,
        string? tooltip = null
    )
    {
        float input = logarithmic != 1 ? Mathf.Log(value, logarithmic) : value;
        float min2 = logarithmic != 1 ? Mathf.Log(min, logarithmic) : min;
        float max2 = logarithmic != 1 ? Mathf.Log(max, logarithmic) : max;
        HorizontalSlider(
            list.GetRect(22f),
            ref input,
            min2,
            max2,
            label == null ? null : label(logarithmic != 1 ? Mathf.Pow(logarithmic, input) : input),
            -1f,
            tooltip
        );
        value = Mathf.Max(
            min,
            Mathf.Min(max, logarithmic != 1 ? Mathf.Pow(logarithmic, input) : input)
        );
        if (roundTo > 0f)
        {
            value = Mathf.RoundToInt(value / roundTo) * roundTo;
        }

        list.Gap(2f);
    }
    #endregion Listing_Standard extensions

    #region Other UX elements & primitive extensions
    public static void HorizontalSlider(
        Rect rect,
        ref float value,
        float leftValue,
        float rightValue,
        string? label,
        float roundTo = -1f,
        string? tooltip = null,
        GameFont? gameFont = null
    )
    {
        float first = rect.width / 2.5f;
        float second = rect.width - first;

        TextAnchor anchor = Text.Anchor;
        GameFont font = Text.Font;
        Text.Font = gameFont ?? GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperLeft;
        Rect rect2 = rect.LeftPartPixels(first);
        rect2.y -= 2f;
        Widgets.Label(rect2, label);
        Text.Anchor = anchor;
        Text.Font = font;

        value = GUI.HorizontalSlider(rect.RightPartPixels(second), value, leftValue, rightValue);
        if (roundTo > 0f)
        {
            value = Mathf.RoundToInt(value / roundTo) * roundTo;
        }

        if (tooltip != null)
        {
            TooltipHandler.TipRegion(rect, tooltip);
        }
    }

    public static string TextAreaScrollable(Rect rect, string text, ref Vector2 scrollbarPosition)
    {
        Rect rect2 =
            new(
                0f,
                0f,
                rect.width - 16f,
                Mathf.Max(Text.CalcHeight(text, rect.width) + 10f, rect.height)
            );
        Widgets.BeginScrollView(rect, ref scrollbarPosition, rect2, true);
        GUIStyle style = Text.textAreaStyles[1];
        style.padding = new RectOffset(8, 8, 8, 8);
        style.active.background = Texture2D.blackTexture;
        style.active.textColor = Color.white;
        string result = GUI.TextArea(rect2, text, style);
        Widgets.EndScrollView();
        return result;
    }

    public static string MyFloat(this int value, int decimals, string? unit = null) =>
        value.ToString("F" + decimals) + (unit == null ? "" : $" {unit}");

    public static int Milliseconds(this float f) => Mathf.FloorToInt(f * 1000);

    public static string ToPercentage(this float value, bool addPlus = true)
    {
        float percentageValue = value * 100;
        return addPlus ? $"{percentageValue:+0.##;-0.##;0}%" : $"{percentageValue:0.##;-0.##;0}%";
    }
    #endregion Other UX elements & primitive extensions
}
