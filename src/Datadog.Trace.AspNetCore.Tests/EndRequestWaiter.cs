using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.DiagnosticAdapter;

namespace Datadog.Trace.AspNetCore.Tests
{
    public class EndRequestWaiter : IDisposable
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        public EndRequestWaiter()
        {
            DiagnosticListener.AllListeners.Subscribe(x =>
            {
                if (x.Name == "Microsoft.AspNetCore")
                {
                    _subscriptions.Add(x.SubscribeWithAdapter(this));
                }
            });
        }

        [DiagnosticName("Microsoft.AspNetCore.Hosting.EndRequest")]
        public void OnHttpRequestInStop()
        {
            _resetEvent.Set();
        }

        [DiagnosticName("Microsoft.AspNetCore.Hosting.UnhandledException")]
        public void OnUnhandledException()
        {
            _resetEvent.Set();
        }

        public void Wait()
        {
            _resetEvent.WaitOne();
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }
    }
}
