using System;
using System.Runtime.CompilerServices;
using Datadog.Util;

namespace OpenTelemetry.DynamicActivityBinding
{
    /// <summary>
    /// Vendors using this library can change the implementaton of the APIs in this class to plug in whatevcer loging solution they wish.
    /// We will avoid creating a complex logging abstraction or taking dependencies on ILogger for now.
    /// </summary>
    public static class Log
    {
        private static class Default
        {
            private const string TimestampFormat = "yy-MM-dd, HH:mm:ss.fff";

            public static void ErrorMessage(string message)
            {
                Console.WriteLine();
                Console.WriteLine($"[{DateTimeOffset.Now.ToString(TimestampFormat)} | ERROR] {Format.SpellIfNull(message)}");
            }

            public static void ErrorException(Exception exception)
            {
                Log.Error(exception?.ToString());
            }

            public static void Info(string message)
            {
                Console.WriteLine();
                Console.WriteLine($"[{DateTimeOffset.Now.ToString(TimestampFormat)} | INFO]  {Format.SpellIfNull(message)}");
            }

            public static void Debug(string message)
            {
                Console.WriteLine();
                Console.WriteLine($"[{DateTimeOffset.Now.ToString(TimestampFormat)} | DEBUG] {Format.SpellIfNull(message)}");
            }
        }  // class Default

        public static class Configure
        {
            public static void Error(Action<string> logEventHandler)
            {
                s_errorMessageLogEventHandler = logEventHandler;
            }

            public static void Error(Action<Exception> logEventHandler)
            {
                s_errorExceptionLogEventHandler = logEventHandler;
            }

            public static void Info(Action<string> logEventHandler)
            {
                s_infoLogEventHandler = logEventHandler;
            }

            public static void Debug(Action<string> logEventHandler)
            {
                s_debugLogEventHandler = logEventHandler;
            }
        }

        private static Action<string> s_errorMessageLogEventHandler = Default.ErrorMessage;
        private static Action<Exception> s_errorExceptionLogEventHandler = Default.ErrorException;
        private static Action<string> s_infoLogEventHandler = Default.Info;
        private static Action<string> s_debugLogEventHandler = Default.Debug;

        /// <summary>
        /// Logs an error.
        /// These need to be persisted well, so that the info is available for support cases.
        /// </summary>
        /// <param name="message"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(string message)
        {
            Action<string> logEventHandler = s_errorMessageLogEventHandler;
            if (logEventHandler != null)
            {
                logEventHandler(message);
            }
        }


        /// <summary>
        /// Logs an error.
        /// These need to be persisted well, so that the info is available for support cases.
        /// </summary>
        /// <param name="exception"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(Exception exception)
        {
            Action<Exception> logEventHandler = s_errorExceptionLogEventHandler;
            if (logEventHandler != null)
            {
                logEventHandler(exception);
            }
        }

        /// <summary>
        /// Logs an important info message.
        /// These need to be persisted well, so that the info is available for support cases.
        /// </summary>
        /// <param name="message"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Info(string message)
        {
            Action<string> logEventHandler = s_infoLogEventHandler;
            if (logEventHandler != null)
            {
                logEventHandler(message);
            }
        }

        /// <summary>
        /// Logs a non-critical info message. Mainly used for for debugging during prototyping.
        /// These messages can likely be dropped in production.
        /// </summary>
        /// <param name="message"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Debug(string message)
        {
            Action<string> logEventHandler = s_debugLogEventHandler;
            if (logEventHandler != null)
            {
                logEventHandler(message);
            }
        }
    }
}
