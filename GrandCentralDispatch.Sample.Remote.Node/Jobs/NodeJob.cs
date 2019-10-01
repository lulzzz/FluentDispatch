using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Quartz;
using Serilog;
using GrandCentralDispatch.Sample.Remote.Node.Helpers;

namespace GrandCentralDispatch.Sample.Remote.Node.Jobs
{
    [DisallowConcurrentExecution]
    public class NodeJob : IJob
    {
        private readonly IHost _host;
        private readonly ILogger _logger;

        public NodeJob(IHost host, ILogger logger)
        {
            _host = host;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            GrpcEnvironment.SetLogger(new Logger(_logger));
            await _host.StartAsync(context.CancellationToken);
        }
    }
}