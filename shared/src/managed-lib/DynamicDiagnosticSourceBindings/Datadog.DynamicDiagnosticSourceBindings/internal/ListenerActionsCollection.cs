using Datadog.Util;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal abstract class ListenerActionsCollection<TAction, TSource>
    {
        protected sealed class Subscription : IDisposable
        {
            private readonly ListenerActionsCollection<TAction, TSource> _ownerCollection;
            public TAction Action { get; }
            public object State { get; }

            public Subscription(TAction action, object state, ListenerActionsCollection<TAction, TSource> ownerCollection)
            {
                Action = action;
                State = state;
                _ownerCollection = ownerCollection;
            }

            public void Dispose()
            {
                _ownerCollection.UnubscribeListener(this);
            }
        }

        private IReadOnlyList<Subscription> _subscriptions = new Subscription[0];
        private readonly string _logComponentMoniker;

        public ListenerActionsCollection(string logComponentMoniker)
        {
            _logComponentMoniker = logComponentMoniker ?? this.GetType().Name;
        }

        protected abstract void InvokeSubscription(Subscription subscription, TSource source);
        protected abstract bool GetMustImmediatelyInvokeNewSubscription(Subscription subscription, out TSource source);

        public int Count
        {
            get
            {
                IReadOnlyList<Subscription> subscriptions = _subscriptions;
                return subscriptions.Count;
            }
        }

        public IDisposable SubscribeListener(TAction action, object state)
        {
            Validate.NotNull(action, nameof(action));

            var subscription = new Subscription(action, state, this);
            AddSubscriptionToList(subscription);

            try
            {
                if (GetMustImmediatelyInvokeNewSubscription(subscription, out TSource source))
                {
                    InvokeOne(subscription, source);
                }
            }
            catch (Exception ex)
            {
                Log.Error(_logComponentMoniker, ex);
            }

            return subscription;
        }

        public bool UnubscribeListener(IDisposable subscription)
        {
            if (subscription != null && subscription is Subscription actionSubscription)
            {
                return RemoveSubscriptionFromList(actionSubscription);
            }
            else
            {
                return false;
            }
        }

        public void InvokeAll(TSource source)
        {
            IReadOnlyList<Subscription> subscriptions = _subscriptions;
            foreach (Subscription subscription in subscriptions)
            {
                InvokeOne(subscription, source);
            }
        }

        public void InvokeAndClearAll(TSource source)
        {
            IReadOnlyList<Subscription> subscriptions = Interlocked.Exchange(ref _subscriptions, new Subscription[0]);
            foreach (Subscription subscription in subscriptions)
            {
                InvokeOne(subscription, source);
            }
        }

        private void InvokeOne(Subscription subscription, TSource source)
        {
            if (subscription != null)
            {
                try
                {
                    InvokeSubscription(subscription, source);
                }
                catch (Exception ex)
                {
                    Log.Error(_logComponentMoniker, ex);
                }
            }
        }

        private void AddSubscriptionToList(Subscription subscription)
        {
            IReadOnlyList<Subscription> currentList = _subscriptions;
            IReadOnlyList<Subscription> newList = CopyAndAdd(currentList, subscription);
            while (currentList != Interlocked.CompareExchange(ref _subscriptions, newList, currentList))
            {
                currentList = _subscriptions;
                newList = CopyAndAdd(currentList, subscription);
            }
        }

        private bool RemoveSubscriptionFromList(Subscription subscription)
        {
            IReadOnlyList<Subscription> currentList = _subscriptions;
            IReadOnlyList<Subscription> newList = CopyAndRemove(currentList, subscription, out bool removed);
            while (currentList != Interlocked.CompareExchange(ref _subscriptions, newList, currentList))
            {
                currentList = _subscriptions;
                newList = CopyAndRemove(currentList, subscription, out removed);
            }

            return removed;
        }

        private static IReadOnlyList<T> CopyAndAdd<T>(IReadOnlyList<T> list, T item)
        {
            var newList = new List<T>(capacity: list.Count + 1);
            newList.Add(item);

            foreach (T element in list)
            {
                newList.Add(element);
            }

            return newList;
        }

        private static IReadOnlyList<T> CopyAndRemove<T>(IReadOnlyList<T> list, T item, out bool removed)
        {
            removed = false;
            var newList = new List<T>(capacity: list.Count);

            foreach (T element in list)
            {
                if (element.Equals(item))
                {
                    removed = true;
                }
                {
                    newList.Add(element);
                }
            }

            return newList;
        }
    }
}
