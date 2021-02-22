using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Datadog.Logging.Composition
{
    /// <summary>
    /// Collects data from a Log-sources and sends it to many Log Sinks.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0007:Use implicit type", Justification = "Worst piece of advise Style tools ever gave.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0028:Simplify collection initialization", Justification = "Rule does not add redability")]
    internal class AggregatedLogSink : ILogSink
    {
        private readonly ILogSink[] _logSinks;

        public AggregatedLogSink(IEnumerable<ILogSink> logSinks)
        {
            if (logSinks == null)
            {
                _logSinks = new ILogSink[0];
            }
            else
            {
                // ToArray without a Linq dependency (also clean null entries):
                var sinks = new List<ILogSink>();
                foreach (ILogSink sink in logSinks)
                {
                    if (sink != null)
                    {
                        sinks.Add(sink);
                    }
                }

                _logSinks = new ILogSink[sinks.Count];
                for (int i = 0; i < sinks.Count; i++)
                {
                    _logSinks[i] = sinks[i];
                }
            }
        }

        public bool TryLogError(LoggingComponentName componentName, string message, Exception exception, params object[] dataNamesAndValues)
        {
            InvokeForAllLogSinks((ls) => ls.TryLogError(componentName, message, exception, dataNamesAndValues), out bool allSucceeded);
            return allSucceeded;
        }

        public bool TryLogInfo(LoggingComponentName componentName, string message, params object[] dataNamesAndValues)
        {
            InvokeForAllLogSinks((ls) => ls.TryLogInfo(componentName, message, dataNamesAndValues), out bool allSucceeded);
            return allSucceeded;
        }

        public bool TryLogDebug(LoggingComponentName componentName, string message, params object[] dataNamesAndValues)
        {
            InvokeForAllLogSinks((ls) => ls.TryLogDebug(componentName, message, dataNamesAndValues), out bool allSucceeded);
            return allSucceeded;
        }

        /// <summary>
        /// Exceptions thrown by any sink will be passed through. However, we will first try to invoke all sinks.
        /// <c>allSucceeded</c> indicates whether all sinks the did NOT throw have succeeded.
        /// </summary>
        /// <param name="sinkFunction">log sink action to execute</param>
        /// <param name="allSucceeded">indicates whether all sinks the did NOT throw have succeeded.</param>
        private void InvokeForAllLogSinks(Func<ILogSink, bool> sinkFunction, out bool allSucceeded)
        {
            // It is not the business of the multiplexer to process errors. We pass them thorough. However, we do our best to invoke all sinks that worked.
            object errorHolder = null;
            allSucceeded = true;
            for (int i = 0; i < _logSinks.Length; i++)
            {
                try
                {
                    bool thisSinkResult = sinkFunction(_logSinks[i]);
                    allSucceeded = allSucceeded && thisSinkResult;
                }
                catch (Exception ex)
                {
                    if (errorHolder == null)
                    {
                        errorHolder = ex;
                    }
                    else if (errorHolder is Exception prevError)
                    {
                        var errorList = new List<Exception>();
                        errorList.Add(prevError);
                        errorList.Add(ex);
                        errorHolder = errorList;
                    }
                    else
                    {
                        List<Exception> errorList = (List<Exception>)errorHolder;
                        errorList.Add(ex);
                    }
                }
            }

            if (errorHolder != null)
            {
                if (errorHolder is Exception singleError)
                {
                    ExceptionDispatchInfo.Capture(singleError).Throw();
                }
                else
                {
                    List<Exception> errorList = (List<Exception>)errorHolder;
                    throw new AggregateException("Two or more Log sinks threw exceptions.", errorList);
                }
            }
        }
    }
}
