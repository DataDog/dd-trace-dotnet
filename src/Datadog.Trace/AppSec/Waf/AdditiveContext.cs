using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    internal class AdditiveContext : IAdditiveContext
    {
        private readonly IntPtr contextHandle;
        private bool disposed = false;

        public AdditiveContext(IntPtr contextHandle)
        {
            this.contextHandle = contextHandle;
        }

        ~AdditiveContext()
        {
            Dispose(false);
        }

        public Return Run(IReadOnlyDictionary<string, object> args)
        {
            // do not add a using or call dispose in some other way here
            // when passing args to pw_runAdditive it will take ownership and free them
            var pwArgs = Encoder.Encode(args);

            var retNative = Native.pw_runAdditive(contextHandle, pwArgs.RawArgs, 1000000);
            var ret = new Return(retNative);

            // in these two specific case we need to explicitly free the pwArgs
            if (ret.ReturnCode == ReturnCode.ErrorTimeout || ret.ReturnCode == ReturnCode.ErrorInvalidCall)
            {
                pwArgs.Dispose();
            }

            return ret;
        }

        public void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            Native.pw_clearAdditive(contextHandle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
