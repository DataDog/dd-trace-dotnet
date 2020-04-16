using System;
using System.Collections;
using System.Collections.Generic;

namespace Datadog.RuntimeMetrics
{
    internal class ObserverCollection<T> : IObservable<T>, IObserver<T>, IEnumerable<IObserver<T>>
    {
        private readonly ICollection<IObserver<T>> _observers;

        public ObserverCollection() : this(new List<IObserver<T>>())
        {
        }

        public ObserverCollection(ICollection<IObserver<T>> observers)
        {
            _observers = observers ?? throw new ArgumentNullException(nameof(observers));
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            _observers.Add(observer);
            return new MetricsUnsubscriber(_observers, observer);
        }

        public void OnCompleted()
        {
            foreach (IObserver<T> observer in _observers)
            {
                observer.OnCompleted();
            }
        }

        public void OnError(Exception error)
        {
            foreach (IObserver<T> observer in _observers)
            {
                observer.OnError(error);
            }
        }

        public void OnNext(T value)
        {
            foreach (IObserver<T> observer in _observers)
            {
                observer.OnNext(value);
            }
        }

        private class MetricsUnsubscriber : IDisposable
        {
            private readonly ICollection<IObserver<T>> _observers;
            private readonly IObserver<T> _observer;

            public MetricsUnsubscriber(ICollection<IObserver<T>> observers, IObserver<T> observer)
            {
                _observers = observers ?? throw new ArgumentNullException(nameof(observers));
                _observer = observer ?? throw new ArgumentNullException(nameof(observer));
            }

            public void Dispose()
            {
                _observers.Remove(_observer);
            }
        }

        IEnumerator<IObserver<T>> IEnumerable<IObserver<T>>.GetEnumerator()
        {
            return _observers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _observers.GetEnumerator();
        }
    }
}
