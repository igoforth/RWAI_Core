using System.Collections.Concurrent;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Verse;

namespace AICore;

public class JobClient : IDisposable
{
    private Lazy<JobManager.JobManagerClient>? _lazyClient;
    private Channel? _channel;
    private string _address = "127.0.0.1:50051";
    private static int jobCounter;
    private static readonly ConcurrentBag<JobTask> _taskList = [];
    private static readonly Lazy<JobClient> _instance = new(() => new JobClient());
    private static readonly SemaphoreSlim _monitorActive = new(1, 1);
    private bool _disposed;

    private JobClient() { }

    public static JobClient Instance => _instance.Value;

    public void Start(string ip, int port)
    {
        UpdateRunningState(true, $"{ip}:{port}");
    }

    public void UpdateRunningState(bool enabled, string? newAddress = null)
    {
        _address = newAddress ?? _address;
        Tools.SafeAsync(async () =>
        {
            await UpdateRunningStateAsync(enabled).ConfigureAwait(false);
        });
    }

    public async Task UpdateRunningStateAsync(bool enabled)
    {
        // Lock to ensure single operation at a time
        await _monitorActive.WaitAsync().ConfigureAwait(false);
        try
        {
            if (enabled)
            {
                // Ensure the channel is ready or try to reconnect
                if (_channel == null || _channel.State == ChannelState.Shutdown || _channel.State == ChannelState.TransientFailure)
                {
#if DEBUG
                    LogTool.Debug("Checking channel state and reconnecting if necessary...");
#endif
                    await ReinitializeChannelAsync().ConfigureAwait(false);
                }
                if (_lazyClient == null || !_lazyClient.IsValueCreated)
                {
                    _lazyClient = new Lazy<JobManager.JobManagerClient>(() => new JobManager.JobManagerClient(_channel));
                }
            }
            else
            {
                // Should not run, so ensure resources are gracefully shutdown
                if (_channel != null)
                {
                    await ShutdownChannelAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _monitorActive.Release();
        }
    }

    private async Task ReinitializeChannelAsync()
    {
        if (_channel != null && _address != null)
            await _channel.ShutdownAsync().ConfigureAwait(false);
        _channel = new Channel(_address, ChannelCredentials.Insecure);
    }

    private async Task ShutdownChannelAsync()
    {
        if (_channel != null)
            await _channel.ShutdownAsync().ConfigureAwait(false);
        _channel = null;
#if DEBUG
        LogTool.Debug("Channel has been shutdown.");
#endif
    }

    public void AddJob(JobRequest jobRequest, Func<JobResponse, Task> callback)
    {
        if (_channel == null || _lazyClient == null)
            return;
        if (jobRequest == null)
            return;

        if (jobRequest.JobId == 0)
        {
            jobRequest.JobId = (uint)jobCounter;
            Interlocked.Increment(ref jobCounter);
        }

        jobRequest.Time = Timestamp.FromDateTime(DateTime.UtcNow);
        jobRequest.Language = LanguageMapping.GetLanguage();

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
        if (_channel == null || _lazyClient == null)
            return;
        if (_monitorActive.CurrentCount == 0)
            return;

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

                if (completedJobTask == null) continue;

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
