using System;
using System.Collections.Generic;

namespace Datadog.Util
{
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
