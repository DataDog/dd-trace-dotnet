using System;

namespace Samples.Probes.TestRuns.Shared
{
    internal class IntentionalDebuggerException : Exception
    {
        public IntentionalDebuggerException(string message)
            : base(message)
        {
        }
    }
}
