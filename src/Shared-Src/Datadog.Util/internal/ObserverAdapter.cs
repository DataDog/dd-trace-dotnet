using System;

namespace Datadog.Util
{
    /// <summary>
    /// Convenience APIs for creating instances of <c>ObserverAdapter{T}</c>
    /// </summary>
    internal static class ObserverAdapter
    {
        public static ObserverAdapter<T> OnNextHandler<T>(Action<T> onNextHandler)
        {
            return new ObserverAdapter<T>(onNextHandler, onErrorHandler: null, onCompletedHandler: null);
        }

        public static ObserverAdapter<T> OnAllHandlers<T>(Action<T> onNextHandler, Action<Exception> onErrorHandler, Action onCompletedHandler)
        {
            return new ObserverAdapter<T>(onNextHandler, onErrorHandler, onCompletedHandler);
        }
    }

    /// <summary>
    /// This is like <c>AnonymousObserver</c> in <c>System.Reactive</c>, but simpler and to the point.
    /// Allows easily creating observers from delegates nd does not need any dependencies on Reactive.
    /// Consider using the static class <c>ObserverAdapter</c> that contains convenience methods for
    /// creating instances of this class.
    /// </summary>
    /// <typeparam name="T">The type whose instances provides notification information.</typeparam>
    public class ObserverAdapter<T> : IObserver<T>
    {
        private readonly Action<T> _onNextHandler;
        private readonly Action<Exception> _onErrorHandler;
        private readonly Action _onCompletedHandler;

        public ObserverAdapter(Action<T> onNextHandler, Action<Exception> onErrorHandler, Action onCompletedHandler)
        {
            _onNextHandler = onNextHandler;
            _onErrorHandler = onErrorHandler;
            _onCompletedHandler = onCompletedHandler;
        }

        void IObserver<T>.OnNext(T value)
        {
            Action<T> handler = _onNextHandler;
            if (handler != null)
            {
                handler.Invoke(value);
            }
        }

        void IObserver<T>.OnError(Exception error)
        {
            Action<Exception> handler = _onErrorHandler;
            if (handler != null)
            {
                handler.Invoke(error);
            }
        }

        void IObserver<T>.OnCompleted()
        {
            Action handler = _onCompletedHandler;
            if (handler != null)
            {
                handler.Invoke();
            }
        }
    }
}
