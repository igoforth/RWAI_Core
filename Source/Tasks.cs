namespace AICore;

// updates the ServerStatus
//
public static class UpdateServerStatus
{
    public static ServerManager.ServerStatus serverStatusEnum =
        ServerManager.currentServerStatusEnum;
    public static string serverStatus = ServerManager.currentServerStatus;

    public static void Task()
    {
        serverStatusEnum = ServerManager.currentServerStatusEnum;
        serverStatus = ServerManager.currentServerStatus;
    }
}

// monitor jobs for Server
//
public static class MonitorJobStatus
{
    public static void Task()
    {
        Tools.SafeAsync(AICoreMod.Client.MonitorJobsAsync);
    }
}