using System;

namespace GrandCentralDispatch.Processors.Unary
{
    internal interface IUnaryProcessor<in TInput> : IDisposable
    {
        void Add(TInput item);

        void Add(Func<TInput> item);
    }
}