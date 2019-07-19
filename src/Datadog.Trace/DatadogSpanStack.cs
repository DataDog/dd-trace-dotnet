using System;
using System.Collections.Immutable;

namespace Datadog.Trace
{
    internal static class DatadogSpanStack
    {
        private static readonly AsyncLocalCompat<StackWrapper> CurrentContextAmbientStorage = new AsyncLocalCompat<StackWrapper>();

        /// <summary>
        /// Gets the current active span.
        /// </summary>
        public static Span Active => CurrentContext.Peek();

        private static ImmutableStack<Span> CurrentContext
        {
            get => CurrentContextAmbientStorage.Get().Value;
            set => CurrentContextAmbientStorage.Set(new StackWrapper { Value = value });
        }

        /// <summary>
        /// Add a span to the current call context stack.
        /// </summary>
        /// <param name="span">The new context. </param>
        /// <returns>A disposable which will clean the stack when this span finishes. </returns>
        public static IDisposable Push(Span span)
        {
            CurrentContext = CurrentContext.Push(span);
            return new PopWhenDisposed();
        }

        private static Span Pop()
        {
            var closingSpan = Active;
            CurrentContext = CurrentContext.Pop();
            return closingSpan;
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

                DatadogSpanStagingArea.QueueSpanForFlush(Pop());

                _disposed = true;
            }
        }

        /// <summary>
        /// Allows this reference to be marshaled across app domains
        /// </summary>
        private sealed class StackWrapper : MarshalByRefObject
        {
            public ImmutableStack<Span> Value { get; set; }
        }
    }
}
