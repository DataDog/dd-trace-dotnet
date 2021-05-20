using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Args : IDisposable
    {
        private PWArgs64 args;
        private bool disposed = false;

        public Args(PWArgs64 args)
        {
            this.args = args;
        }

        // NOTE: do not add a finalizer here. Often args will be owned and freed by the native code

        public ArgsType ArgsType
        {
            get { return Encoder.DecodeArgsType(args.Type); }
        }

        public PWArgs64 RawArgs
        {
            get { return args; }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            Native.pw_freeArg(ref args);
        }
    }
}
