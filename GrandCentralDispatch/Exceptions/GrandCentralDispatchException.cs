using System;

namespace GrandCentralDispatch.Exceptions
{
    internal class GrandCentralDispatchException : Exception
    {
        public GrandCentralDispatchException()
        {
        }

        public GrandCentralDispatchException(string message)
            : base(message)
        {
        }

        public GrandCentralDispatchException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}