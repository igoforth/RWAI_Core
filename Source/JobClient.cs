using System.Collections.Concurrent;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Steamworks;
using UnityEngine;
using Verse;
using Verse.Steam;

namespace AICore;

public class JobClient : IDisposable
{
    private Lazy<JobManager.JobManagerClient>? _lazyClient;
    private Channel? _channel;
    private static int jobCounter;
    private static readonly ConcurrentBag<JobTask> _taskList = [];
    private static readonly Lazy<JobClient> _instance = new(() => new JobClient());
    private static readonly SemaphoreSlim _monitorActive = new(1, 1);
    private bool _disposed;

    private JobClient() { }

    public static JobClient Instance => _instance.Value;

    public void Start(string ip, int port)
    {
        string address = $"{ip}:{port}";
        // var channelOptions = new List<ChannelOption>
        // {
        //     new(ChannelOptions.MaxSendMessageLength, int.MaxValue),
        //     new(ChannelOptions.MaxReceiveMessageLength, int.MaxValue),
        //     new("grpc.keepalive_time_ms", 10000), // 10 seconds
        //     new("grpc.keepalive_timeout_ms", 5000), // 5 seconds
        //     new("grpc.keepalive_permit_without_calls", 1), // Keep alive even if there are no active calls
        //     new("grpc.http2.min_time_between_pings_ms", 10000), // Minimum time between pings
        //     new("grpc.http2.max_pings_without_data", 0), // Unlimited pings
        //     new("grpc.http2.max_ping_strikes", 0) // Unlimited ping failures
        // };

        // _channel = new Channel(address, ChannelCredentials.Insecure, channelOptions);
        _channel = new Channel(address, ChannelCredentials.Insecure);
        _lazyClient = new Lazy<JobManager.JobManagerClient>(
            () => new JobManager.JobManagerClient(_channel)
        );
    }

    public void AddJob(JobRequest jobRequest, Func<JobResponse, Task> callback)
    {
        if (_channel == null || _lazyClient == null) return;
        if (jobRequest == null) return;

        if (jobRequest.JobId == 0)
        {
            jobRequest.JobId = (uint)jobCounter;
            Interlocked.Increment(ref jobCounter);
        }

        jobRequest.Time = Timestamp.FromDateTime(DateTime.UtcNow);
        jobRequest.Language = GetLanguage();

        try
        {
            CallOptions callOptions = new(deadline: DateTime.UtcNow.AddMinutes(30));
            AsyncUnaryCall<JobResponse> jobCall = _lazyClient.Value.JobServiceAsync(
                jobRequest,
                callOptions
            );
            JobTask jobTask = new(jobCall, callback);
            _taskList.Add(jobTask);
#if DEBUG
            LogTool.Debug("JobClient sent job!");
#endif
        }
        catch (Exception ex)
        {
            LogTool.Error($"Error adding job: {ex.Message}");
        }
    }

    public async Task MonitorJobsAsync()
    {
        if (_channel == null || _lazyClient == null) return;
        if (_monitorActive.CurrentCount == 0) return;

        await _monitorActive.WaitAsync().ConfigureAwait(false);
        try
        {
            while (!_taskList.IsEmpty)
            {
                JobTask[] jobTasks = [.. _taskList];
                Task<JobResponse> completedTask = await Task.WhenAny(
                        jobTasks.Select(t => t.AsyncCall.ResponseAsync)
                    )
                    .ConfigureAwait(false);
                JobTask completedJobTask = jobTasks.FirstOrDefault(t =>
                    t.AsyncCall.ResponseAsync == completedTask
                );

                if (completedJobTask == null)
                {
                    continue;
                }

                try
                {
                    JobResponse jobResponse =
                        await completedJobTask.AsyncCall.ResponseAsync.ConfigureAwait(false);
                    await completedJobTask.Callback(jobResponse).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogTool.Error($"Error processing job: {ex.Message}");
                }
                finally
                {
                    _ = _taskList.TryTake(out _); // Remove the completed task
                }
            }
        }
        finally
        {
            _ = _monitorActive.Release();
        }
    }

    private static SupportedLanguage GetLanguage()
    {
        return LanguageDatabase.activeLanguage?.folderName is string folderLang
            && LanguageMapping.LanguageMap.TryGetValue(
                folderLang,
                out SupportedLanguage mappedLang1
            )
            ? mappedLang1
            : SteamManager.Initialized
            && SteamApps.GetCurrentGameLanguage().CapitalizeFirst() is string steamLang
            && LanguageMapping.LanguageMap.TryGetValue(steamLang, out SupportedLanguage mappedLang2)
            ? mappedLang2
            : Application.systemLanguage.ToStringSafe() is string appLang
            && LanguageMapping.LanguageMap.TryGetValue(appLang, out SupportedLanguage mappedLang3)
                ? mappedLang3
                : SupportedLanguage.English;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _channel?.ShutdownAsync().Wait();
            _channel = null;
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~JobClient()
    {
        Dispose(false);
    }
}

public class JobTask(AsyncUnaryCall<JobResponse> asyncCall, Func<JobResponse, Task> callback)
{
    public AsyncUnaryCall<JobResponse> AsyncCall { get; } = asyncCall;
    public Func<JobResponse, Task> Callback { get; } = callback;
}
