#if NET45
using System;
using System.Runtime.Remoting;

namespace Datadog.Trace
{
    // Create a wrapper around ObjectHandle to enable a Sponsor for
    // objects stored in the CallContext until the host AppDomain
    // no longer needs them.
    // This issue was raised in the Serilog library here: https://github.com/serilog/serilog/issues/987
    // This solution is a combination of the following items:
    //   1) https://github.com/serilog/serilog/pull/992
    //   2) https://github.com/serilog/serilog/issues/835#issuecomment-242944461
    internal sealed class DisposableObjectHandle : ObjectHandle, IDisposable
    {
        private bool _disposed;

        public DisposableObjectHandle(object o)
            : base(o)
        {
        }

        public override sealed object InitializeLifetimeService()
        {
            // For net45 where AsyncLocal is not a built-in Framework class, we must propgate the ambient context using LogicalCallContext.
            // One consequence of this is that we invoke the System.Runtime.Remoting infrastructure, and if a remote method call or cross-AppDomain
            // method call is made, objects stored inside the LogicalCallContext are transmitted to the other side (aka the "server").
            //
            // For distributed garbage collection, the "server" uses leases / sponsors to keep track of what objects are needed by "clients"
            // (our original AppDomain) and should not be garbage collected. If we do not refresh the lease on objects stored inside the LogicalCallContext,
            // the "server" will garbage collect them and disconnect them, meaning any accesses of the object inside the LogicalCallContext will throw a
            // RemotingException.
            //
            // This solves the problem by returning null so that the lease on the object never expires. To make sure the object doesn't live forever
            // on the "server", we'll use the Disposable pattern to disconnect the object when we no longer need it.
            //
            // This approach is preferred over previous implementations because this InitializeLifetimeService() method will be invoked on the "server"
            // which may be an AppDomain with locked-down permissions, which means this object will not be able to call into SecurityCritical methods like
            // the base.InitializeLifetimeService() method (which will throw a SecurityException) and keep itself alive.
            //
            // See this link for more info on remoting leases / sponsors: https://docs.microsoft.com/en-us/archive/msdn-magazine/2003/december/managing-remote-net-objects-with-leasing-and-sponsorship
            return null;
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
                RemotingServices.Disconnect(this);
            }

            _disposed = true;
        }
    }
}
#endif
