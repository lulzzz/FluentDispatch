using System;

namespace FluentDispatch.Exceptions
{
    internal class FluentDispatchException : Exception
    {
        public FluentDispatchException()
        {
        }

        public FluentDispatchException(string message)
            : base(message)
        {
        }

        public FluentDispatchException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}