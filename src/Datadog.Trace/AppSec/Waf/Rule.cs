using System;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Rule : IDisposable
    {
        private IntPtr ruleHandle;
        private bool disposed;

        public Rule(IntPtr ruleHandle)
        {
            this.ruleHandle = ruleHandle;
        }

        ~Rule()
        {
            Dispose(false);
        }

        public IntPtr Handle
        {
            get { return ruleHandle; }
        }

        public void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            Native.pw_clearRuleH(ruleHandle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
