global using System;
global using RimWorld;
global using UnityEngine;
global using Verse;
using HarmonyLib;

namespace AICore;

using System.Collections;
using System.Collections.Concurrent;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;

[HarmonyPatch(typeof(Current), nameof(Current.Notify_LoadedSceneChanged))]
[StaticConstructorOnStartup]
public static class Main
{
    static readonly ConcurrentQueue<Action> actions = new();

    static Main()
    {
        Postfix();
    }

    public static void Postfix()
    {
        if (GenScene.InEntryScene)
            _ = Current.Root_Entry.StartCoroutine(Process());
        if (GenScene.InPlayScene)
            _ = Current.Root_Play.StartCoroutine(Process());
    }

    static IEnumerator Process()
    {
        while (true)
        {
            yield return null;
            if (actions.TryDequeue(out var action) == false)
                continue;
            action();
        }
    }

    public static async Task Perform(Action action)
    {
        var working = true;
        actions.Enqueue(() =>
        {
            action();
            working = false;
        });
        while (working)
            await Task.Delay(200);
    }

    public static async Task<T> Perform<T>(Func<T> action)
    {
        T result = default;
        var working = true;
        actions.Enqueue(() =>
        {
            result = action();
            working = false;
        });
        while (working)
            await Task.Delay(200);
        return result;
    }
}

public class AICoreMod : Mod
{
    public static CancellationTokenSource onQuit = new();
    public static AICoreSettings Settings;
    public static ServerManager Server = ServerManager.Instance;
    public static Mod self;

    public AICoreMod(ModContentPack content)
        : base(content)
    {
        self = this;
        Settings = GetSettings<AICoreSettings>();

        // Force a reload of the runtime assembly binding settings
        // idk if it works but its something to experiment with
        ConfigurationManager.RefreshSection("runtime");

        // Could be used someday if I can get it working?
        // TypeResolver.Initialize();

        var harmony = new Harmony("net.trojan.rimworld.mod.AICore");
        harmony.PatchAll();

        LongEventHandler.ExecuteWhenFinished(() =>
        {
            // This performs any necessary setup when the game is loaded

            // this will exit early if:
            // already configured
            // user setting
            // no internet
            BootstrapTool.Run();

            if (Settings.IsConfigured)
            {
                // This is the main entry point for the mod

                // this configures server with any runtime information
                // which doesn't depend on bootstrap status
                // Server = ServerManager.Instance;
            }
        });

        Application.wantsToQuit += () =>
        {
            onQuit.Cancel();
            return true;
        };
    }

    public static bool Running => onQuit.IsCancellationRequested == false;

    public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);

    public override string SettingsCategory() => "AI Core";
}
