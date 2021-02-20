using System;
using System.Diagnostics;
using System.Text;

namespace Datadog.Logging.Emission
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1308:Variable names must not be prefixed", Justification = "Should not apply to statics")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1311:Static readonly fields must begin with upper-case letter", Justification = "Should only apply to vars that are logically const.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1116:Split parameters must start on line after declaration", Justification = "Bad rule")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names must not contain underscore", Justification = "Underscore aid visibility in long names")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0007:Use implicit type", Justification = "Worst piece of advise Style tools ever gave.")]
    internal static class DefaultFormat
    {
        public const string TimestampPattern_Local = @"yyyy-MM-dd, HH\:mm\:ss\.fff \(zzz\)";
        public const string TimestampPattern_Utc = @"yyyy-MM-dd, HH\:mm\:ss\.fff";

        public const string LogLevelMoniker_Error = "ERROR";
        public const string LogLevelMoniker_Info = "INFO ";
        public const string LogLevelMoniker_Debug = "DEBUG";

        private const string NullWord = "null";
        private const string DataValueNotSpecifiedWord = "unspecified";
        private static readonly string s_procIdInfo = GetProcIdInfoString();

        public static string ConstructErrorMessage(string message, Exception exception)
        {
            if (message != null && exception != null)
            {
                if (message.Length > 0 && message[message.Length - 1] == '.')
                {
                    return message + " " + exception.ToString();
                }
                else
                {
                    return message + ". " + exception.ToString();
                }
            }
            else if (message != null && exception == null)
            {
                return message;
            }
            else if (message == null && exception != null)
            {
                return exception.ToString();
            }
            else
            {
                return null;
            }
        }

        public static StringBuilder ConstructLogLine(string logLevelMoniker, string componentName, bool useUtcTimestamp, string message, params object[] dataNamesAndValues)
        {
            return ConstructLogLine(logLevelMoniker, componentName, null, useUtcTimestamp, message, dataNamesAndValues);
        }

        public static StringBuilder ConstructLogLine(string logLevelMoniker,
                                                     string componentNamePart1,
                                                     string componentNamePart2,
                                                     bool useUtcTimestamp,
                                                     string message,
                                                     params object[] dataNamesAndValues)
        {
            var logLine = new StringBuilder(capacity: 128);
            AppendLogLinePrefix(logLine, logLevelMoniker, useUtcTimestamp);
            AppendEventInfo(logLine, componentNamePart1, componentNamePart2, message, dataNamesAndValues);

            return logLine;
        }

        public static void AppendLogLinePrefix(StringBuilder targetBuffer, string logLevelMoniker, bool useUtcTimestamp)
        {
            targetBuffer.Append("[");
            AppendLogLinePrefixCore(targetBuffer, logLevelMoniker, useUtcTimestamp);
            targetBuffer.Append("] ");
        }

        public static void AppendLogLinePrefixCore(StringBuilder targetBuffer, string logLevelMoniker, bool useUtcTimestamp)
        {
            if (targetBuffer == null)
            {
                return;
            }

            if (useUtcTimestamp)
            {
                targetBuffer.Append(DateTimeOffset.UtcNow.ToString(TimestampPattern_Utc));
                targetBuffer.Append(" UTC");
            }
            else
            {
                targetBuffer.Append(DateTimeOffset.Now.ToString(TimestampPattern_Local));
            }

            if (logLevelMoniker != null)
            {
                targetBuffer.Append(" | ");
                targetBuffer.Append(logLevelMoniker);
            }

            if (s_procIdInfo != null)
            {
                targetBuffer.Append(s_procIdInfo);
            }
        }

        public static void AppendEventInfo(StringBuilder targetBuffer,
                                           string componentNamePart1,
                                           string componentNamePart2,
                                           string message,
                                           params object[] dataNamesAndValues)
        {
            bool hasComponentNamePart1 = !string.IsNullOrWhiteSpace(componentNamePart1);
            bool hasComponentNamePart2 = !string.IsNullOrWhiteSpace(componentNamePart2);

            if (hasComponentNamePart1)
            {
                targetBuffer.Append(componentNamePart1);
            }

            if (hasComponentNamePart1 && hasComponentNamePart2)
            {
                targetBuffer.Append("::");
            }

            if (hasComponentNamePart2)
            {
                targetBuffer.Append(componentNamePart2);
            }

            if (hasComponentNamePart1 || hasComponentNamePart2)
            {
                targetBuffer.Append(": ");
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                targetBuffer.Append(message);

                if (message.Length > 0 && message[message.Length - 1] == '.')
                {
                    targetBuffer.Append(' ');
                }
                else
                {
                    targetBuffer.Append(". ");
                }
            }

            if (dataNamesAndValues != null && dataNamesAndValues.Length > 0)
            {
                targetBuffer.Append("{");
                for (int i = 0; i < dataNamesAndValues.Length; i += 2)
                {
                    if (i > 0)
                    {
                        targetBuffer.Append(", ");
                    }

                    targetBuffer.Append('[');
                    QuoteIfString(targetBuffer, dataNamesAndValues[i]);
                    targetBuffer.Append(']');
                    targetBuffer.Append('=');

                    if (i + 1 < dataNamesAndValues.Length)
                    {
                        QuoteIfString(targetBuffer, dataNamesAndValues[i + 1]);
                    }
                    else
                    {
                        targetBuffer.Append(DataValueNotSpecifiedWord);
                    }
                }

                targetBuffer.Append("}");
            }
        }

        private static string GetProcIdInfoString()
        {
            const int MinPidWidth = 6;
            const int MaxPidWidth = 10;
            const string PIdPrefix = " | PId:";

            int maxInfoStringLen = MaxPidWidth + PIdPrefix.Length;

            try
            {
                var pidStr = new StringBuilder(capacity: maxInfoStringLen + 1);

                pidStr.Append(Process.GetCurrentProcess().Id);
                while (pidStr.Length < MinPidWidth)
                {
                    pidStr.Insert(0, ' ');
                }

                pidStr.Insert(0, PIdPrefix);

                return pidStr.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static void QuoteIfString<T>(StringBuilder targetBuffer, T val)
        {
            if (val == null)
            {
                targetBuffer.Append(NullWord);
            }
            else
            {
                if (val is string strValue)
                {
                    targetBuffer.Append('"');
                    targetBuffer.Append(strValue);
                    targetBuffer.Append('"');
                }
                else
                {
                    targetBuffer.Append(val.ToString());
                }
            }
        }
    }
}
