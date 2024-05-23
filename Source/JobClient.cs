extern alias extensions;

namespace AICore;

using System.Collections.Generic;
using System.Net;
using Grpc.Core;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Lite;

// public class JobClient : IJobManager
// {
//     public static CallInvoker invoke;
//     private static readonly IPEndPoint _endPoint = new IPEndPoint(
//         IPAddress.Parse("127.0.0.1"),
//         50051
//     );
//     private static readonly Stack<JobRequest> _workItemStack = new Stack<JobRequest>();
//     private static LiteChannel _channel;
//     private static CallInvoker _invoker;

//     // private static JobManager.JobManagerClient client;

//     public static async void Start()
//     {
//         _channel = await ConnectionFactory.ConnectSocket(_endPoint).AsFrames().CreateChannelAsync();
//         _invoker = _channel.CreateCallInvoker();
//     }

//     public static void AddJob(JobRequest jobRequest)
//     {
//         _workItemStack.Push(jobRequest);
//     }

//     // public static async Task ProcessJobsAsync()
//     // {
//     //     while (_workItemStack.Count > 0)
//     //     {
//     //         var jobRequest = _workItemStack.Pop();
//     //         var jobResponse = await SendJobAsync(jobRequest);
//     //         HandleResponse(jobResponse);
//     //     }
//     // }

//     private static void HandleResponse(JobResponse response)
//     {
//         // LogTool.Message(
//         //     $"Job ID: {response.JobId}, Job Type: {response.JobType}, Duration: {response.Duration}"
//         // );
//         // Process the response payload as needed
//     }

//     public async extensions::System.Threading.Tasks.ValueTask<JobResponse> JobServiceAsync(
//         JobRequest value,
//         CallContext context = default
//     )
//     {
//         return await _invoker.AsyncUnaryCall(JobResponse, value, context);
//     }
// }
