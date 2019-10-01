using System;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Processors.Dual
{
    internal interface IDualProcessor<TInput1, TInput2> : IDisposable
    {
        void Add(LinkedItem<TInput1> item1);

        void Add(LinkedItem<TInput2> item2);

        void Add(LinkedFuncItem<TInput1> item1);

        void Add(LinkedFuncItem<TInput2> item2);
    }
}