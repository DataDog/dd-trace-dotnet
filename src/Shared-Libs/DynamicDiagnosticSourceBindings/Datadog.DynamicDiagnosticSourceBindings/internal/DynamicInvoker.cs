using System;
using System.Threading;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DynamicInvoker
    {
#region Static API
        private static DynamicInvoker s_currentInvoker = null;

        public static DynamicInvoker Current
        {
            get
            {
                DynamicInvoker invoker = Volatile.Read(ref s_currentInvoker);
                if (invoker == null)
                {
                    if (!DynamicLoader.EnsureInitialized())
                    {
                        throw new InvalidOperationException($"Cannot obtain a {nameof(Current)} {nameof(DynamicInvoker)}:"
                                                          + $" The {nameof(DynamicLoader)} cannot initialize.");
                    }

                    invoker = Volatile.Read(ref s_currentInvoker);
                    if (invoker == null)
                    {
                        throw new InvalidOperationException($"Cannot obtain a {nameof(Current)} {nameof(DynamicInvoker)}:"
                                                          + $" The {nameof(DynamicLoader)} was initialized, but the invoker is still null.");
                    }
                }

                return invoker;
            }

            internal set
            {
                DynamicInvoker prevInvoker = Interlocked.Exchange(ref s_currentInvoker, value);

                if (prevInvoker != null && !Object.ReferenceEquals(prevInvoker, value))
                {
                    prevInvoker.Invalidate();
                }
            }
        }
#endregion Static API

        private readonly DynamicInvoker_DiagnosticSource _diagnosticSourceInvoker;
        private readonly DynamicInvoker_DiagnosticListener _diagnosticListenerInvoker;

        public DynamicInvoker(Type diagnosticSourceType, Type diagnosticListenerType)
        {
            _diagnosticSourceInvoker = new DynamicInvoker_DiagnosticSource(diagnosticSourceType);
            _diagnosticListenerInvoker = new DynamicInvoker_DiagnosticListener(diagnosticListenerType);
        }

        public DynamicInvoker_DiagnosticSource DiagnosticSource
        {
            get { return _diagnosticSourceInvoker; }
        }

        public DynamicInvoker_DiagnosticListener DiagnosticListener
        {
            get { return _diagnosticListenerInvoker; }
        }

        private void Invalidate()
        {
            _diagnosticSourceInvoker.Handle.Invalidate();
            _diagnosticListenerInvoker.Handle.Invalidate();
        }
    }
}
