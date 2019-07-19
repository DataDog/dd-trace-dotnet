using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Immutables
{
    internal sealed class DatadogImmutableStack<T> : IDatadogImmutableStack<T>
    {
        private static readonly DatadogImmutableStack<T> SEmptyField = new DatadogImmutableStack<T>();
        private readonly T _head;
        private readonly DatadogImmutableStack<T> _tail;

        private DatadogImmutableStack()
        {
        }

        private DatadogImmutableStack(T head, DatadogImmutableStack<T> tail)
        {
            _head = head;
            _tail = tail;
        }

        /// <summary>Gets an empty immutable stack.</summary>
        /// <returns>An empty immutable stack.</returns>
        public static DatadogImmutableStack<T> Empty => DatadogImmutableStack<T>.SEmptyField;

        public bool IsEmpty => _tail == null;

        public DatadogImmutableStack<T> Clear()
        {
            return Empty;
        }

        IDatadogImmutableStack<T> IDatadogImmutableStack<T>.Clear()
        {
            return Clear();
        }

        public T Peek()
        {
            if (IsEmpty)
            {
                throw new InvalidOperationException("Can not peek an empty stack.");
            }

            return _head;
        }

        public DatadogImmutableStack<T> Push(T value)
        {
            return new DatadogImmutableStack<T>(value, this);
        }

        IDatadogImmutableStack<T> IDatadogImmutableStack<T>.Push(T value)
        {
            return Push(value);
        }

        /// <summary>Removes the element at the top of the immutable stack and returns the stack after the removal.</summary>
        /// <returns>A stack; never null.</returns>
        public DatadogImmutableStack<T> Pop()
        {
            if (IsEmpty)
            {
                throw new InvalidOperationException("Can not pop an empty stack.");
            }

            return _tail;
        }

        /// <summary>Removes the specified element from the immutable stack and returns the stack after the removal.</summary>
        /// <param name="value">The value to remove from the stack.</param>
        /// <returns>A stack; never null.</returns>
        public DatadogImmutableStack<T> Pop(out T value)
        {
            value = Peek();
            return Pop();
        }

        IDatadogImmutableStack<T> IDatadogImmutableStack<T>.Pop()
        {
            return Pop();
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            if (!IsEmpty)
            {
                return new EnumeratorObject(this);
            }

            return Enumerable.Empty<T>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)new DatadogImmutableStack<T>.EnumeratorObject(this);
        }

        internal DatadogImmutableStack<T> Reverse()
        {
            var stack1 = Clear();
            for (var stack2 = this; !stack2.IsEmpty; stack2 = stack2.Pop())
            {
                stack1 = stack1.Push(stack2.Peek());
            }

            return stack1;
        }

        public struct Enumerator
        {
            private readonly DatadogImmutableStack<T> _originalStack;
            private DatadogImmutableStack<T> _remainingStack;

            internal Enumerator(DatadogImmutableStack<T> stack)
            {
                if (stack == null)
                {
                    throw new ArgumentNullException(nameof(stack));
                }

                _originalStack = stack;
                _remainingStack = null;
            }

            /// <summary>Gets the element at the current position of the enumerator.</summary>
            /// <returns>The element at the current position of the enumerator.</returns>
            public T Current
            {
                get
                {
                    if (_remainingStack == null || _remainingStack.IsEmpty)
                    {
                        throw new InvalidOperationException();
                    }

                    return _remainingStack.Peek();
                }
            }

            /// <summary>Advances the enumerator to the next element of the immutable stack.</summary>
            /// <returns>true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the end of the stack.</returns>
            public bool MoveNext()
            {
                if (_remainingStack == null)
                {
                    _remainingStack = _originalStack;
                }
                else if (!_remainingStack.IsEmpty)
                {
                    _remainingStack = _remainingStack.Pop();
                }

                return !_remainingStack.IsEmpty;
            }
        }

        private class EnumeratorObject : IEnumerator<T>, IEnumerator, IDisposable
        {
            private readonly DatadogImmutableStack<T> _originalStack;
            private DatadogImmutableStack<T> _remainingStack;
            private bool _disposed;

            internal EnumeratorObject(DatadogImmutableStack<T> stack)
            {
                if (stack == null)
                {
                    throw new ArgumentNullException(nameof(stack));
                }

                _originalStack = stack;
            }

            public T Current
            {
                get
                {
                    ThrowIfDisposed();
                    if (_remainingStack == null || _remainingStack.IsEmpty)
                    {
                        throw new InvalidOperationException();
                    }

                    return _remainingStack.Peek();
                }
            }

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                ThrowIfDisposed();
                if (_remainingStack == null)
                {
                    _remainingStack = _originalStack;
                }
                else if (!_remainingStack.IsEmpty)
                {
                    _remainingStack = _remainingStack.Pop();
                }

                return !_remainingStack.IsEmpty;
            }

            public void Reset()
            {
                ThrowIfDisposed();
                _remainingStack = null;
            }

            public void Dispose()
            {
                _disposed = true;
            }

            private void ThrowIfDisposed()
            {
                if (!_disposed)
                {
                    return;
                }

                throw new InvalidOperationException("This stack has already been disposed.");
            }
        }
    }
}
