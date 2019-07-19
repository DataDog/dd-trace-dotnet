using System;
using Datadog.Trace.Immutables;

namespace Datadog.Trace
{
    internal static class DatadogScopeStack
    {
        private static readonly AsyncLocalCompat<StackWrapper> CurrentContextAmbientStorage = new AsyncLocalCompat<StackWrapper>();

        public static event EventHandler<SpanEventArgs> SpanActivated;

        public static event EventHandler<SpanEventArgs> SpanClosed;

        public static event EventHandler<SpanEventArgs> TraceEnded;

        /// <summary>
        /// Gets the current active span.
        /// </summary>
        public static Scope Active => CurrentContext.Peek();

        private static DatadogImmutableStack<Scope> CurrentContext
        {
            get => CurrentContextAmbientStorage.Get()?.Value;
            set => CurrentContextAmbientStorage.Set(new StackWrapper { Value = value });
        }

        /// <summary>
        /// Add a span to the current call context stack.
        /// </summary>
        /// <param name="scope">The new context. </param>
        /// <returns>A disposable which will clean the stack when this span finishes. </returns>
        public static IDisposable Push(Scope scope)
        {
            if (CurrentContext == null)
            {
                CurrentContext = DatadogImmutableStack<Scope>.Empty;
            }

            CurrentContext = CurrentContext.Push(scope);

            SpanActivated?.Invoke(null, new SpanEventArgs(scope.Span));

            return new PopWhenDisposed();
        }

        private static Scope Pop()
        {
            var closingScope = Active;
            CurrentContext = CurrentContext.Pop();

            SpanClosed?.Invoke(null, new SpanEventArgs(closingScope.Span));

            if (closingScope.Parent == null)
            {
                TraceEnded?.Invoke(null, new SpanEventArgs(closingScope.Span));
            }

            return closingScope;
        }

        private sealed class PopWhenDisposed : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                DatadogSpanStagingArea.QueueSpanForFlush(Pop().Span);

                _disposed = true;
            }
        }

        /// <summary>
        /// Allows this reference to be marshaled across app domains
        /// </summary>
        private sealed class StackWrapper : MarshalByRefObject
        {
            public DatadogImmutableStack<Scope> Value { get; set; } = DatadogImmutableStack<Scope>.Empty;
        }
    }
}
