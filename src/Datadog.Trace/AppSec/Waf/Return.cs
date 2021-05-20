using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Return : IDisposable
    {
        private PWRet returnHandle;
        private bool disposed;

        public Return(PWRet returnHandle)
        {
            this.returnHandle = returnHandle;
        }

        ~Return()
        {
            Dispose(false);
        }

        public ReturnCode ReturnCode
        {
            get { return Encoder.DecodeReturnCode(returnHandle.Action); }
        }

        public void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            Native.pw_freeReturn(returnHandle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
