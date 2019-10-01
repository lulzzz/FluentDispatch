using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Quartz;

namespace GrandCentralDispatch.Sample.Remote.Cluster.Jobs
{
    [DisallowConcurrentExecution]
    public class ApiJob : IJob
    {
        private readonly IWebHost _host;

        public ApiJob(IWebHost host)
        {
            _host = host;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await _host.RunAsync(context.CancellationToken);
        }
    }
}