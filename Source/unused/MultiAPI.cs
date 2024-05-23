using static HarmonyLib.AccessTools;

namespace AICore;

using System.Reflection;

public static class MultiAPI
{
    public delegate string TextAreaScrollableDelegate(
        Rect rect,
        string text,
        ref Vector2 scrollbarPosition,
        bool readOnly
    );
    static readonly MethodInfo mTextAreaScrollable =
        Method("Verse.Widgets:TextAreaScrollable") ?? Method("LudeonTK.DevGUI:TextAreaScrollable");
    public static TextAreaScrollableDelegate TextAreaScrollable =
        MethodDelegate<TextAreaScrollableDelegate>(mTextAreaScrollable);
}
