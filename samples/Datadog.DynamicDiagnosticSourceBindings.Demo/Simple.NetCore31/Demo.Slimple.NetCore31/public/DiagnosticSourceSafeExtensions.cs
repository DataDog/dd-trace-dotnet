using System;
using System.Collections.Generic;
using Datadog.DynamicDiagnosticSourceBindings;

namespace Demo.Slimple.NetCore31
{
    /// <summary>
    /// One of several possible patterns to protect from exceptions thrown by the DS stub APIs.
    /// Other demos show other possble approaches.
    /// </summary>
    public static class DiagnosticSourceSafeExtensions
    {
        private static bool s_isLogExceptionsEnabled = true;
        private static string s_logComponentMoniker = null;

        public static class Configure
        {
            public static bool IsLogExceptionsEnabled
            {
                get { return s_isLogExceptionsEnabled; }
                set { s_isLogExceptionsEnabled = value; }
            }

            public static string LogComponentMoniker
            {
                get { return s_logComponentMoniker; }
                set { s_logComponentMoniker = value; }
            }
        }

        public static bool CreateNewSourceSafe(string diagnosticSourceName, out DiagnosticSourceStub result, out Exception error)
        {
            try
            {
                error = null;
                result = DiagnosticListening.CreateNewSource(diagnosticSourceName);
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                if (s_isLogExceptionsEnabled)
                {

                    Log.Error(s_logComponentMoniker, ex);
                }

                result = DiagnosticSourceStub.NoOpStub;
                return false;
            }
        }

        public static bool SubscribeToAllSourcesSafe(IObserver<DiagnosticListenerStub> diagnosticSourcesObserver, out IDisposable result, out Exception error)
        {
            try
            {
                error = null;
                result = DiagnosticListening.SubscribeToAllSources(diagnosticSourcesObserver);
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                if (s_isLogExceptionsEnabled)
                {
                    Log.Error(s_logComponentMoniker, ex);
                }

                result = null;
                return false;
            }
        }

        public static bool IsEnabledSafe(this DiagnosticSourceStub diagnosticSource, string eventName, out bool result, out Exception error)
        {
            try
            {
                error = null;
                result = diagnosticSource.IsEnabled(eventName);
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                if (s_isLogExceptionsEnabled)
                {
                    Log.Error(s_logComponentMoniker, ex);
                }

                result = false;
                return false;
            }
        }

        public static bool IsEnabledSafe(this DiagnosticSourceStub diagnosticSource, string eventName, object arg1, out bool result, out Exception error)
        {
            try
            {
                error = null;
                result = diagnosticSource.IsEnabled(eventName, arg1);
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                if (s_isLogExceptionsEnabled)
                {
                    Log.Error(s_logComponentMoniker, ex);
                }

                result = false;
                return false;
            }
        }

        public static bool IsEnabledSafe(this DiagnosticSourceStub diagnosticSource, string eventName, object arg1, object arg2, out bool result, out Exception error)
        {
            try
            {
                error = null;
                result = diagnosticSource.IsEnabled(eventName, arg1, arg2);
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                if (s_isLogExceptionsEnabled)
                {
                    Log.Error(s_logComponentMoniker, ex);
                }

                result = false;
                return false;
            }
        }

        public static bool WriteSafe(this DiagnosticSourceStub diagnosticSource, string eventName, object payloadValue, out Exception error)
        {
            try
            {
                error = null;
                diagnosticSource.Write(eventName, payloadValue);
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                if (s_isLogExceptionsEnabled)
                {
                    Log.Error(s_logComponentMoniker, ex);
                }

                return false;
            }
        }

        public static bool GetNameSafe(this DiagnosticListenerStub diagnosticListener, out string result, out Exception error)
        {
            try
            {
                error = null;
                result = diagnosticListener.Name;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                if (s_isLogExceptionsEnabled)
                {
                    Log.Error(s_logComponentMoniker, ex);
                }

                result = null;
                return false;
            }
        }

        public static bool SubscribeToEventsSafe(this DiagnosticListenerStub diagnosticListener,
                                                        IObserver<KeyValuePair<string, object>> eventObserver,
                                                        Func<string, object, object, bool> isEventEnabledFilter,
                                                        out IDisposable result,
                                                        out Exception error)
        {
            try
            {
                error = null;
                result = diagnosticListener.SubscribeToEvents(eventObserver, isEventEnabledFilter);
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                if (s_isLogExceptionsEnabled)
                {
                    Log.Error(s_logComponentMoniker, ex);
                }

                result = null;
                return false;
            }
        }
    }
}
