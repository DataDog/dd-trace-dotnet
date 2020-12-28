using System;

namespace Datadog.Trace.ClrProfiler.CallTarget
{
    internal class CallTargetInvokerException : Exception
    {
        public CallTargetInvokerException(Exception innerException)
            : base(innerException.Message, innerException)
        {
        }
    }
}
