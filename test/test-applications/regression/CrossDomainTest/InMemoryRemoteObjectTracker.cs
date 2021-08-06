using System.Runtime.Remoting;
using System.Runtime.Remoting.Services;
using System.Threading;
using Datadog.Trace;

namespace CrossDomainTest
{
    internal class InMemoryRemoteObjectTracker : ITrackingHandler
    {
        private readonly string _prefix;
        private readonly CountdownEvent _cde;

        public InMemoryRemoteObjectTracker(CountdownEvent cde, string prefix)
        {
            _cde = cde;
            _prefix = prefix;
        }

        public int DisconnectCount { get; set; }

        public void DisconnectedObject(object obj)
        {
            if (obj is DisposableObjectHandle handle)
            {
                var scope = (Scope)handle.Unwrap();

                if (scope.Span.OperationName.StartsWith(_prefix))
                {
                    DisconnectCount++;
                    _cde.Signal();
                }
            }
        }

        public void MarshaledObject(object obj, ObjRef or)
        {
        }

        public void UnmarshaledObject(object obj, ObjRef or)
        {
        }
    }
}
