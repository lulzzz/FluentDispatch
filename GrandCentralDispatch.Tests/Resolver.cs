using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Resolvers;
using GrandCentralDispatch.Tests.Models;

namespace GrandCentralDispatch.Tests
{
    internal sealed class Resolver : Resolver<Message>
    {
        private readonly ConcurrentBag<string> _bodies;

        public Resolver(ConcurrentBag<string> bodies)
        {
            _bodies = bodies;
        }

        protected override Task Process(Message message, NodeMetrics nodeMetrics, CancellationToken cancellationToken)
        {
            _bodies.Add(message.Body);
            return Task.CompletedTask;
        }
    }
}