using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace AICore;

[HarmonyPatch(typeof(Current), nameof(Current.Notify_LoadedSceneChanged))]
[StaticConstructorOnStartup]
public static class Main
{
    private static readonly ConcurrentQueue<Action> actions = new();

    static Main()
    {
        Postfix();
    }

    public static void Postfix()
    {
        if (GenScene.InEntryScene)
        {
            _ = Current.Root_Entry.StartCoroutine(Process());
        }

        if (GenScene.InPlayScene)
        {
            _ = Current.Root_Play.StartCoroutine(Process());
        }
    }

    private static IEnumerator Process()
    {
        while (true)
        {
            yield return null;
            if (actions.TryDequeue(out Action? action))
            {
                action?.Invoke();
            }
        }
    }

    public static async Task Perform(Action action)
    {
        TaskCompletionSource<bool> tcs = new();

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
            if (actions.TryDequeue(out Action? queuedAction))
            {
                queuedAction();
            }

            await Task.Delay(200).ConfigureAwait(false);
        }

        _ = await tcs.Task.ConfigureAwait(false);
    }

    public static async Task<T> Perform<T>(Func<T> action)
    {
        TaskCompletionSource<T> tcs = new();

        actions.Enqueue(() =>
        {
            try
            {
                T? result = action();
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
            if (actions.TryDequeue(out Action? queuedAction))
            {
                queuedAction();
            }

            await Task.Delay(200).ConfigureAwait(false);
        }

        return await tcs.Task.ConfigureAwait(false);
    }
}

public class AICoreMod : Mod
{
    public static Mod? self;
    public static AICoreSettings? Settings;
    public static readonly ReadOnlyCollection<string> expansionList = new(["RimWorldAI Core", "RimWorldAI Items"]);
    public static CancellationTokenSource onQuit = new();
    public static ServerManager Server => _lazyServerManager.Value;
    public static JobClient Client => _lazyClient.Value;
    private static readonly Lazy<JobClient> _lazyClient = new(() => JobClient.Instance);
    private static readonly Lazy<ServerManager> _lazyServerManager =
        new(() => ServerManager.Instance);

    static AICoreMod() { }

    public AICoreMod(ModContentPack content)
        : base(content)
    {
#if DEBUG
        LogTool.Debug("AICoreMod: Constructor called");
#endif

        // Get HW info and set intercepting env vars
        // this must happen before Settings is initialized
        // else consider different check in MainMenu patch
        BootstrapTool.Init();

        self = this;
        Settings = GetSettings<AICoreSettings>();

        // Force a reload of the runtime assembly binding settings
        // idk if it works but its something to experiment with
        // ConfigurationManager.RefreshSection("runtime");

        Harmony harmony = new("net.trojan.rimworld.mod.AICore");
        harmony.PatchAll();

        LongEventHandler.ExecuteWhenFinished(() =>
        {
            if (Settings.IsConfigured)
            {
                BootstrapTool.UpdateRunningState(Settings.AutoUpdateCheck);
                if (BootstrapTool.isConfigured is not null and true)
                {
                    JobClient.UpdateRunningState(Settings.Enabled);
                    ServerManager.UpdateRunningState(Settings.Enabled);
                }
            }
        });

        Application.wantsToQuit += () =>
        {
            onQuit.Cancel();
            BootstrapTool.UpdateRunningState(false);
            JobClient.UpdateRunningState(false);
            ServerManager.UpdateRunningState(false);
            return true;
        };
    }

    public static bool Running => !onQuit.IsCancellationRequested;

    public override void DoSettingsWindowContents(Rect inRect) =>
        Settings!.DoWindowContents(inRect);

    public override string SettingsCategory() => "RimWorldAI";
}
