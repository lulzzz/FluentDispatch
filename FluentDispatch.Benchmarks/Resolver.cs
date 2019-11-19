using System.Threading;
using System.Threading.Tasks;
using FluentDispatch.Benchmarks.Models;
using FluentDispatch.Models;
using FluentDispatch.Resolvers;

namespace FluentDispatch.Benchmarks
{
    internal sealed class Resolver : Resolver<Message>
    {
        protected override async Task Process(Message item, NodeMetrics nodeMetrics, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }
    }
}