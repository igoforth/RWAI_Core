using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICore;

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

    private string Parse(string input)
    {
        return currentLine += input;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            LogTool.RemoveSink(this);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~UISink()
    {
        Dispose(false);
    }
}
