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

        public void Error(StringPair componentName, string message, Exception exception, params object[] dataNamesAndValues)
        {
            InvokeForAllLogSinks((ls) => ls.Error(componentName, message, exception, dataNamesAndValues));
        }

        public void Info(StringPair componentName, string message, params object[] dataNamesAndValues)
        {
            InvokeForAllLogSinks((ls) => ls.Info(componentName, message, dataNamesAndValues));
        }

        public void Debug(StringPair componentName, string message, params object[] dataNamesAndValues)
        {
            InvokeForAllLogSinks((ls) => ls.Debug(componentName, message, dataNamesAndValues));
        }

        private void InvokeForAllLogSinks(Action<ILogSink> action)
        {
            // It is not the business of the multiplexer to process errors. We pass them thorough. However, we do our best to invoke all sinks that worked.
            object errorHolder = null;

            for (int i = 0; i < _logSinks.Length; i++)
            {
                try
                {
                    action(_logSinks[i]);
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
