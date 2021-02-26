using System;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal static class DynamicInvoker
    {
        private static DynamicInvoker_DiagnosticListener s_diagnosticListenerInvoker = null;
        private static DynamicInvoker_DiagnosticSource s_diagnosticSourceInvoker = null;

        public static DynamicInvoker_DiagnosticListener DiagnosticListener
        {
            get
            {
                return s_diagnosticListenerInvoker;
            }

            internal set
            {
                s_diagnosticListenerInvoker = value;
            }
        }

        public static DynamicInvoker_DiagnosticSource DiagnosticSource
        {
            get
            {
                return s_diagnosticSourceInvoker;
            }

            internal set
            {
                s_diagnosticSourceInvoker = value;
            }
        }
    }
}
