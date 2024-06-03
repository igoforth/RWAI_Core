using TMPro;
using UnityEngine.UI;

namespace AICore;

using System.Collections.Generic;

public class UISink : ISink
{
    private static UISink? instance;
    private readonly List<string> logEntries = [];
    private TextMeshProUGUI? logTextUI;
    private ScrollRect? scrollRect;
    private string currentLine = "";
    public string Name => "UISink";

    // singleton pattern
    public static UISink Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new UISink();
                LogTool.AddSink(instance);
            }
            return instance;
        }
    }

    public void Initialize(TextMeshProUGUI textUI, ScrollRect scrollRect)
    {
        logTextUI = textUI;
        this.scrollRect = scrollRect;
    }

    public void LogMessage(string message, int level)
    {
        message = Parse(message);
        logEntries.Add(message);
        if (logEntries.Count > 100) // Limit the number of entries to prevent overflow
        {
            logEntries.RemoveAt(0);
        }
        UpdateUIText(message);
    }

    private void UpdateUIText(string message)
    {
        if (logTextUI != null && scrollRect != null)
        {
            logTextUI.text = string.Join("\n", logEntries);
            logTextUI.text = message;
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f; // Scroll to bottom
            Canvas.ForceUpdateCanvases();
        }
    }

    public void Write(string formattedLogMessage, int level)
    {
        LogMessage(formattedLogMessage, level);
    }

    public void Dispose()
    {
        LogTool.RemoveSink(this); // Remove from LogTool's sinks
    }

    private string Parse(string input)
    {
        // Append to current line
        currentLine += input;
        return currentLine;
    }
}
