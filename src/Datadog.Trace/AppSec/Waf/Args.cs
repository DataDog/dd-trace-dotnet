// <copyright file="Args.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Args : IDisposable
    {
        private PWArgs args;
        private bool disposed = false;

        public Args(PWArgs args)
        {
            this.args = args;
        }

        // NOTE: do not add a finalizer here. Often args will be owned and freed by the native code

        public ArgsType ArgsType
        {
            get { return Encoder.DecodeArgsType(args.Type); }
        }

        public PWArgs RawArgs
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
