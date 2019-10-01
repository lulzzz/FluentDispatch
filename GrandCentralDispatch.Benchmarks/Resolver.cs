using System.Threading;
using System.Threading.Tasks;
using GrandCentralDispatch.Benchmarks.Models;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Resolvers;

namespace GrandCentralDispatch.Benchmarks
{
    internal sealed class Resolver : Resolver<Message>
    {
        protected override async Task Process(Message item, NodeMetrics nodeMetrics, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }
    }
}