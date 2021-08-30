using System;
using System.Collections.Generic;

namespace Datadog.Util
{
    /// <summary>
    /// An exception aggregator for zero-or-more exceptions. See suggested usage pattern below.
    /// * If NO exceptions are added:
    ///    - No allocations are made.
    ///    - <c>TryGetAggregatedException(..)</c> returns no exceptions.
    ///    - <c>ThrowIfNotEmpty(..)</c> does not throw.
    /// * If ONE exception is added:
    ///    - One additional allocations are made (for the <c>ExceptionDispatchInfo</c>).
    ///    - <c>TryGetAggregatedException(..)</c> returns the added exception instance.
    ///    - <c>ThrowIfNotEmpty(..)</c> re-throws that particular exception preserving all stack traced (uses <c>ExceptionDispatchInfo</c>).
    /// * If TWO or MORE exceptions are added:
    ///    - Two additional allocation are made (for a <c>List{Exception}</c> and for an <c>AggregateException</c> that encapsulates that list).
    ///    - <c>TryGetAggregatedException(..)</c> returns a new <c>AggregateException</c> instance which contains all the added exceptions.
    ///    - <c>ThrowIfNotEmpty(..)</c> throws a new <c>AggregateException</c> instance which contains all the added exceptions.
    ///    
    /// Suggested usage pattern:
    /// <code>
    ///     class Example
    ///     {
    ///         void DoStuff()
    ///         {
    ///         var errors = new ExceptionAggregator();
    ///     
    ///         try
    ///             {
    ///                 // ...
    ///             }
    ///             catch (Exception ex)
    ///             {
    ///                 errors.Add(ex);
    ///             }
    ///     
    ///             IEnumerable<object> items = null;
    ///             foreach (object item in items)
    ///             {
    ///                 try
    ///                 {
    ///                     // process item
    ///                 }
    ///                 catch (Exception ex)
    ///                 {
    ///                     errors.Add(ex);
    ///                 }
    ///             }
    ///     
    ///             errors.ThrowIfNotEmpty();
    ///         }
    ///     }
    /// </code>
    /// </summary>
    /// <remarks>This type is a <c>ref struct</c> rather than a simple <c>struct</c> because it is a non-immutable value type.
    /// Being a <c>ref struct</c> prevents it from being unintentionally boxed (such a boxed copy may be torn-modified).</remarks>
    internal ref struct ExceptionAggregator
    {
        private object _aggregator;

        public bool IsEmpty
        {
            get { return (_aggregator == null); }
        }

        public int Count
        {
            get
            {
                if (_aggregator == null)
                {
                    return 0;
                }
                else if (_aggregator is Exception)
                {
                    return 1;
                }
                else
                {
                    List<Exception> prevList = (List<Exception>) _aggregator;
                    return prevList.Count;
                }
            }
        }

        public void Add(Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            if (_aggregator == null)
            {
                _aggregator = exception;
                return;
            }

            if (_aggregator is Exception prevException)
            {
                var aggr = new List<Exception>();
                aggr.Add(prevException);
                aggr.Add(exception);
                _aggregator = aggr;
                return;
            }

            List<Exception> prevList = (List<Exception>) _aggregator;
            prevList.Add(exception);
        }

        public bool TryGetAggregatedException(out Exception exception)
        {
            return TryGetAggregatedException(aggregateExceptionMessage: null, out exception);
        }

        public bool TryGetAggregatedException(string aggregateExceptionMessage, out Exception exception)
        {
            if (_aggregator == null)
            {
                exception = null;
                return false;
            }

            if (_aggregator is Exception prevException)
            {
                exception = prevException;
                return true;
            }

            List<Exception> prevList = (List<Exception>) _aggregator;
            exception = (aggregateExceptionMessage == null)
                                ? new AggregateException(prevList)
                                : new AggregateException(aggregateExceptionMessage, prevList);
            return true;
        }

        public void ThrowIfNotEmpty()
        {
            ThrowIfNotEmpty(aggregateExceptionMessage: null);
        }

        public void ThrowIfNotEmpty(string aggregateExceptionMessage)
        {
            if (_aggregator == null)
            {
                return;
            }

            if (_aggregator is Exception prevException)
            {
                prevException.Rethrow();
            }

            List<Exception> prevList = (List<Exception>) _aggregator;
            AggregateException aggEx = (aggregateExceptionMessage == null)
                                            ? new AggregateException(prevList)
                                            : new AggregateException(aggregateExceptionMessage, prevList);
            throw aggEx;
        }
    }
}
