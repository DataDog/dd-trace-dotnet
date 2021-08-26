using System;

namespace Datadog.Util
{
    /// <summary>
    /// Convenience APIs for creating instances of <c>ObserverAdapter{TObservedItem}</c> and <c>StatefulObserverAdapter{TObservedItem, TState}</c>
    /// </summary>
    internal static class ObserverAdapter
    {
        public static ObserverAdapter<TObservedItem> OnNextHandler<TObservedItem>(Action<TObservedItem> onNextHandler)
        {
            return new ObserverAdapter<TObservedItem>(onNextHandler, onErrorHandler: null, onCompletedHandler: null);
        }

        public static StatefulObserverAdapter<TObservedItem, TState> OnNextHandler<TObservedItem, TState>(Action<TObservedItem, TState> onNextHandler)
        {
            return new StatefulObserverAdapter<TObservedItem, TState>(onNextHandler, onErrorHandler: null, onCompletedHandler: null);
        }

        public static StatefulObserverAdapter<TObservedItem, TState> OnNextHandler<TObservedItem, TState>(TState initialState, Action<TObservedItem, TState> onNextHandler)
        {
            return new StatefulObserverAdapter<TObservedItem, TState>(initialState, onNextHandler, onErrorHandler: null, onCompletedHandler: null);
        }

        public static ObserverAdapter<TObservedItem> OnAllHandlers<TObservedItem>(Action<TObservedItem> onNextHandler,
                                                                                  Action<Exception> onErrorHandler,
                                                                                  Action onCompletedHandler)
        {
            return new ObserverAdapter<TObservedItem>(onNextHandler, onErrorHandler, onCompletedHandler);
        }

        public static StatefulObserverAdapter<TObservedItem, TState> OnAllHandlers<TObservedItem, TState>(Action<TObservedItem, TState> onNextHandler,
                                                                                                          Action<Exception, TState> onErrorHandler,
                                                                                                          Action<TState> onCompletedHandler)
        {
            return new StatefulObserverAdapter<TObservedItem, TState>(onNextHandler, onErrorHandler, onCompletedHandler);
        }

        public static StatefulObserverAdapter<TObservedItem, TState> OnAllHandlers<TObservedItem, TState>(TState initialState,
                                                                                                          Action<TObservedItem, TState> onNextHandler,
                                                                                                          Action<Exception, TState> onErrorHandler,
                                                                                                          Action<TState> onCompletedHandler)
        {
            return new StatefulObserverAdapter<TObservedItem, TState>(initialState, onNextHandler, onErrorHandler, onCompletedHandler);
        }
    }

    /// <summary>
    /// This is like <c>AnonymousObserver</c> in <c>System.Reactive</c>, but simpler and to the point.
    /// Allows easily creating observers from delegates nd does not need any dependencies on Reactive.<br />
    /// See also the sister-type <c>StatefulObserverAdapter{TObservedItem, TState}</c> for an equivalent
    /// functionality where some object state needs to be kept.
    /// </summary>
    /// <remarks>Consider using the static class <c>ObserverAdapter</c> that contains convenience methods for
    /// creating instances of this class.</remarks>
    /// <typeparam name="TObservedItem">The type whose instances provides notification information.</typeparam>
    internal sealed class ObserverAdapter<TObservedItem> : IObserver<TObservedItem>
    {
        private readonly Action<TObservedItem> _onNextHandler;
        private readonly Action<Exception> _onErrorHandler;
        private readonly Action _onCompletedHandler;

        public ObserverAdapter(Action<TObservedItem> onNextHandler, Action<Exception> onErrorHandler, Action onCompletedHandler)
        {
            _onNextHandler = onNextHandler;
            _onErrorHandler = onErrorHandler;
            _onCompletedHandler = onCompletedHandler;
        }

        void IObserver<TObservedItem>.OnNext(TObservedItem value)
        {
            Action<TObservedItem> handler = _onNextHandler;
            if (handler != null)
            {
                handler.Invoke(value);
            }
        }

        void IObserver<TObservedItem>.OnError(Exception error)
        {
            Action<Exception> handler = _onErrorHandler;
            if (handler != null)
            {
                handler.Invoke(error);
            }
        }

        void IObserver<TObservedItem>.OnCompleted()
        {
            Action handler = _onCompletedHandler;
            if (handler != null)
            {
                handler.Invoke();
            }
        }
    }

    /// <summary>
    /// This type is very similar to its sister type <c>ObserverAdapter{TObservedItem}</c>, but it has an additional <c>State</c> property that
    /// is made available to all callbacks. This allows for similar plug-and-play patterns without the need for scaffoling code around state keeping.
    /// </summary>
    /// <remarks>Consider using the static class <c>ObserverAdapter</c> that contains convenience methods for
    /// creating instances of this class.</remarks>
    /// <typeparam name="TObservedItem">The type whose instances provides notification information.</typeparam>
    /// <typeparam name="TState">The type of the state kept by instances of this class.</typeparam>
    internal sealed class StatefulObserverAdapter<TObservedItem, TState> : IObserver<TObservedItem>
    {
        private readonly Action<TObservedItem, TState> _onNextHandler;
        private readonly Action<Exception, TState> _onErrorHandler;
        private readonly Action<TState> _onCompletedHandler;
        private TState _state;

        public StatefulObserverAdapter(Action<TObservedItem, TState> onNextHandler, Action<Exception, TState> onErrorHandler, Action<TState> onCompletedHandler)
            : this(default(TState), onNextHandler, onErrorHandler, onCompletedHandler)
        {
        }

        public StatefulObserverAdapter(TState initialSTate, Action<TObservedItem, TState> onNextHandler, Action<Exception, TState> onErrorHandler, Action<TState> onCompletedHandler)
        {
            _state = initialSTate;
            _onNextHandler = onNextHandler;
            _onErrorHandler = onErrorHandler;
            _onCompletedHandler = onCompletedHandler;
        }

        public TState State
        {
            get { return _state; }
            set { _state = value; }
        }

        void IObserver<TObservedItem>.OnNext(TObservedItem value)
        {
            Action<TObservedItem, TState> handler = _onNextHandler;
            if (handler != null)
            {
                handler.Invoke(value, _state);
            }
        }

        void IObserver<TObservedItem>.OnError(Exception error)
        {
            Action<Exception, TState> handler = _onErrorHandler;
            if (handler != null)
            {
                handler.Invoke(error, _state);
            }
        }

        void IObserver<TObservedItem>.OnCompleted()
        {
            Action<TState> handler = _onCompletedHandler;
            if (handler != null)
            {
                handler.Invoke(_state);
            }
        }
    }
}
