namespace AICore;

using System.Collections.Generic;
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
    private const string target = "127.0.0.1:50051";
    private static readonly Stack<JobRequest> _workItemStack = new Stack<JobRequest>();
    private JobManager.JobManagerClient _client;
    private static Channel _channel;

    public void Start()
    {
        _channel = new Channel(target, ChannelCredentials.Insecure);
        _client = new JobManager.JobManagerClient(_channel);
    }

    public static void AddJob(JobRequest jobRequest)
    {
        _workItemStack.Push(jobRequest);
    }

    public async Task ProcessJobsAsync()
    {
        while (_workItemStack.Count > 0)
        {
            var jobRequest = _workItemStack.Pop();
            var jobResponse = await SendJobAsync(jobRequest);
            HandleResponse(jobResponse);
        }
    }

    private static void HandleResponse(JobResponse response)
    {
        LogTool.Message(
            $"Job ID: {response.JobId}, Job Type: {response.JobType}, Duration: {response.Duration}"
        );
        // Process the response payload as needed
    }

    private async Task<JobResponse> SendJobAsync(JobRequest value)
    {
        return await _client.JobServiceAsync(value);
    }
}
