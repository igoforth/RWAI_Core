namespace AICore;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;

//   public static HelloReply Greet(string greeting)
//   {
//     const int Port = 30051;

//     Server server = new Server
//     {
//       Services = { Greeter.BindService(new GreeterImpl()) },
//       Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
//     };
//     server.Start();

//     Channel channel = new Channel("127.0.0.1:30051", ChannelCredentials.Insecure);

//     var client = new Greeter.GreeterClient(channel);

//     var reply = client.SayHello(new HelloRequest { Name = greeting });

//     channel.ShutdownAsync().Wait();

//     server.ShutdownAsync().Wait();

//     return reply;
//   }

//   class GreeterImpl : Greeter.GreeterBase
//   {
//     // Server side handler of the SayHello RPC
//     public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
//     {
//       return Task.FromResult(new HelloReply { Message = "Hello " + request.Name });
//     }
//   }

public class JobClient
{
    private JobManager.JobManagerClient _client;
    private static Channel _channel;
    private static readonly ConcurrentBag<JobTask> _taskList = new ConcurrentBag<JobTask>();
    private static readonly Lazy<JobClient> _instance = new Lazy<JobClient>(() => new JobClient());

    private JobClient() { }

    public static JobClient Instance => _instance.Value;

    public void Start(string ip, int port)
    {
        _channel = new Channel($"{ip}:{port}", ChannelCredentials.Insecure);
        _client = new JobManager.JobManagerClient(_channel);
    }

    public void AddJob(JobRequest jobRequest, Action<JobResponse> callback)
    {
        try
        {
            var jobCall = _client.JobServiceAsync(jobRequest);
            var jobTask = new JobTask(jobCall, callback);
            _taskList.Add(jobTask);
        }
        catch (Exception ex)
        {
            LogTool.Error($"Error adding job: {ex.Message}");
        }
    }

    public async Task ProcessJobsAsync()
    {
        while (!_taskList.IsEmpty)
        {
            var jobTasks = _taskList.ToArray();
            var completedTask = await Task.WhenAny(jobTasks.Select(t => t.AsyncCall.ResponseAsync));
            var completedJobTask = jobTasks.First(t => t.AsyncCall.ResponseAsync == completedTask);

            try
            {
                var jobResponse = await completedJobTask.AsyncCall.ResponseAsync;
                completedJobTask.Callback(jobResponse);
            }
            catch (Exception ex)
            {
                LogTool.Error($"Error processing job: {ex.Message}");
            }
            finally
            {
                _taskList.TryTake(out var _); // Remove the completed task
            }
        }
    }
}

public class JobTask
{
    public AsyncUnaryCall<JobResponse> AsyncCall { get; }
    public Action<JobResponse> Callback { get; }

    public JobTask(AsyncUnaryCall<JobResponse> asyncCall, Action<JobResponse> callback)
    {
        AsyncCall = asyncCall;
        Callback = callback;
    }
}
