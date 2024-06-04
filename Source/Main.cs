using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace AICore;

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
            if (actions.TryDequeue(out var action))
                action?.Invoke();
        }
    }

    public static async Task Perform(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();

        actions.Enqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        // Process the actions in the queue
        while (!tcs.Task.IsCompleted)
        {
            if (actions.TryDequeue(out var queuedAction))
                queuedAction();

            await Task.Delay(200);
        }

        await tcs.Task;
    }

    public static async Task<T> Perform<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>();

        actions.Enqueue(() =>
        {
            try
            {
                var result = action();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        // Process the actions in the queue
        while (!tcs.Task.IsCompleted)
        {
            if (actions.TryDequeue(out var queuedAction))
                queuedAction();

            await Task.Delay(200);
        }

        return await tcs.Task;
    }
}

public class AICoreMod : Mod
{
    public static CancellationTokenSource onQuit = new();
    public static AICoreSettings? Settings;
    private static readonly Lazy<ServerManager> _lazyServerManager =
        new(() => ServerManager.Instance);
    public static ServerManager Server => _lazyServerManager.Value;
    private static readonly Lazy<JobClient> _lazyClient = new(() => JobClient.Instance);
    public static JobClient Client => _lazyClient.Value;
    public static Mod? self;

    static AICoreMod() { }

    public AICoreMod(ModContentPack content)
        : base(content)
    {
#if DEBUG
        LogTool.Debug("AICoreMod: Constructor called");
#endif

        self = this;
        Settings = GetSettings<AICoreSettings>();

        // Get HW info and set intercepting env vars
        // BootstrapTool.preInit();

        // Force a reload of the runtime assembly binding settings
        // idk if it works but its something to experiment with
        // ConfigurationManager.RefreshSection("runtime");

        var harmony = new Harmony("net.trojan.rimworld.mod.AICore");
        harmony.PatchAll();

        LongEventHandler.ExecuteWhenFinished(() =>
        {
            // this will exit early if:
            // already configured
            // user setting
            // no internet
            BootstrapTool.Run();

            if (Settings.IsConfigured)
            {
                // Should only run after BootstrapTool.Run() finishes
                // if not, implement connection manager in Client
                Client.Start("127.0.0.1", 50051);
            }
        });

        Application.wantsToQuit += () =>
        {
            onQuit.Cancel();
            return true;
        };
    }

    public static bool Running => onQuit.IsCancellationRequested == false;

    public override void DoSettingsWindowContents(Rect inRect) =>
        Settings?.DoWindowContents(inRect);

    public override string SettingsCategory() => "AI Core";
}
