using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FluentDispatch.Models;
using FluentDispatch.Resolvers;
using FluentDispatch.Tests.Models;

namespace FluentDispatch.Tests
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