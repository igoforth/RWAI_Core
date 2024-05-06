using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AICore;

public class Dialog_Help : Window
{
    private readonly HelpType helpType;
    private Vector2 dialogSize = new(640f, 520f);

    private static readonly Dictionary<DialogSize, Vector2> dialogSizes =
        new()
        {
            { DialogSize.Small, new Vector2(320f, 260f) },
            { DialogSize.Medium, new Vector2(500f, 400f) },
            { DialogSize.Large, new Vector2(640f, 520f) }
        };

    private static readonly Dictionary<HelpType, string> helpTexts =
        new()
        {
            // Mod Help
            { HelpType.Default, helpText },
            { HelpType.ModHelp, @"This mod utilizes two types of APIs" },
            // Remote APIs
            { HelpType.OpenAI, @"To enable ChatGPT functionality from OpenAI" },
            { HelpType.OpenRouter, @"OpenRouter.ai is an aggregate for chat models" },
            //{ HelpType.Cohere,"" },
            { HelpType.TogetherAI, @"Together.ai is an aggregate for chat models" },
            { HelpType.OtherExternal, "" },
            // Local APIs
            { HelpType.Ollama, "" },
            { HelpType.LocalAI, "" },
            { HelpType.OtherLocal, "" },
            // Voice Services
            {
                HelpType.Azure,
                @"This mod uses Microsoft Azure's Cognitive Services to enable TTS."
            },
            // Other
            { HelpType.BaseUrl, "The Base URL is the core web address for an AI API provider." },
            { HelpType.ModelId, "" },
            { HelpType.SecondaryModelId, "" },
        };

    Vector2 scrollPosition;

    const string helpText = @"This mod utilizes two external APIs";

    public override Vector2 InitialSize => dialogSize;

    public Dialog_Help(HelpType helpType, DialogSize dialogSize)
    {
        doCloseX = true;
        forcePause = true;
        absorbInputAroundWindow = true;
        onlyOneOfTypeAllowed = true;
        closeOnAccept = true;
        closeOnCancel = true;
        this.helpType = helpType;
        this.dialogSize = dialogSizes[dialogSize];
    }

    public static void Show(HelpType helpType, DialogSize dialogSize = DialogSize.Large) =>
        Find.WindowStack?.Add(new Dialog_Help(helpType, dialogSize));

    public override void DoWindowContents(Rect inRect)
    {
        var y = inRect.y;

        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(0f, y, inRect.width, 42f), "RimGPT Help");
        y += 42f;

        var textRect = new Rect(inRect.x, y, inRect.width, inRect.height - y);
        _ = MultiAPI.TextAreaScrollable(textRect, helpTexts[helpType], ref scrollPosition, false);
    }

    public enum HelpType
    {
        // Mod Help
        Default,
        ModHelp,

        // Remote APIs
        OpenAI,
        OpenRouter,

        //Cohere,
        TogetherAI,
        OtherExternal,

        // Local APIs
        Ollama,
        LocalAI,
        OtherLocal,

        // Voice Services
        Azure,

        // Other
        BaseUrl,
        ModelId,
        SecondaryModelId,
    }

    public enum DialogSize
    {
        Small,
        Medium,
        Large
    }
}
