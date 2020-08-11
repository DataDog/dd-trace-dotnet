#if NET45
using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;

namespace Datadog.Trace
{
    // Create a wrapper around ObjectHandle to enable a Sponsor for
    // objects stored in the CallContext until the host AppDomain
    // no longer needs them.
    // This issue was raised in the Serilog library here: https://github.com/serilog/serilog/issues/987
    // This solution was borrowed from the corresponding fix in the following PR: https://github.com/serilog/serilog/pull/992
    internal sealed class DisposableObjectHandle : ObjectHandle, IDisposable
    {
        private static readonly ISponsor LifeTimeSponsor = new ClientSponsor();

        private bool _disposed;

        public DisposableObjectHandle(object o)
            : base(o)
        {
        }

        public override object InitializeLifetimeService()
        {
            var lease = (ILease)base.InitializeLifetimeService();
            lease?.Register(LifeTimeSponsor);
            return lease;
        }

        public void Dispose() => Dispose(true);

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (GetLifetimeService() is ILease lease)
                {
                    lease.Unregister(LifeTimeSponsor);
                }
            }

            _disposed = true;
        }
    }
}
#endif
