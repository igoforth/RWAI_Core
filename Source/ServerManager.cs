// asynchronous
// manages the python process (which manages the AI Server)
// sends gRPC to ai server to get generation
// updates the server status
//
namespace AICore;

public static class ServerManager
{
    public enum ServerStatus
    {
        Online,

        Starting,

        Error,

        Offline
    }

    // private static Process process;
    public static bool serverRunning = false;
    public static string serverStatus = "AI Server " + ServerStatus.Offline;

    // update server status
    public static void UpdateServerStatus(ServerStatus status)
    {
        serverStatus = "AI Server " + status.ToString();
    }

    // static void Start()
    // {
    //     StartProcess();
    // }

    // static void StartProcess()
    // {
    //     process = new Process();
    //     process.StartInfo.FileName = "yourprogram.exe";
    //     process.StartInfo.UseShellExecute = false;
    //     process.StartInfo.RedirectStandardOutput = true;
    //     process.StartInfo.RedirectStandardError = true;
    //     process.StartInfo.CreateNoWindow = true;
    //     process.Start();
    //     serverRunning = true;

    //     // Start reading the output asynchronously
    //     Task.Run(() => ReadOutputAsync());
    // }

    // public static void ReadOutputAsync()
    // {
    //     Tools.SafeAsync(async () =>
    //     {
    //         //FileLog.Log($"{persona?.name ?? "null"} ai request: {phrases.Join(p => p.text, "|")}");
    //         spokenText = await persona.ai.Evaluate(persona, phrases);
    //         //FileLog.Log($"{persona?.name ?? "null"} ai reponse: {spokenText}");
    //         if (spokenText == null || spokenText == "")
    //         {
    //             doneCallback();
    //             completed = true;
    //             return;
    //         }
    //         audioClip = await TTS.AudioClipFromAzure(
    //             persona,
    //             $"{TTS.APIURL}/v1",
    //             spokenText,
    //             errorCallback
    //         );
    //         doneCallback();
    //         completed = true;
    //     });
    //     // Asynchronously read the standard output of the spawned process.
    //     string stdout = await process.StandardOutput.ReadToEndAsync();
    //     string stderr = await process.StandardError.ReadToEndAsync();
    // }

    // static void OnDestroy()
    // {
    //     if (!process.HasExited)
    //         process.Kill();
    // }
}
