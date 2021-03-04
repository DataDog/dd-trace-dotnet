using System;
using System.Collections.Generic;
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

        private const string ComponentPartSeparator = "::";
        private const string NewLineReplacement = "^->";
        private const string NullWord = "null";
        private const string DataValueNotSpecifiedWord = "unspecified";
        private static readonly string s_procIdInfo = GetProcIdInfoString();

        public static void ComposeComponentName(string inPart1, string inPart2, string inPart3, out string outPart1, out string outPart2)
        {
            if (inPart1 != null && string.IsNullOrWhiteSpace(inPart1))
            {
                inPart1 = null;
            }

            if (inPart2 != null && string.IsNullOrWhiteSpace(inPart2))
            {
                inPart2 = null;
            }

            if (inPart3 != null && string.IsNullOrWhiteSpace(inPart3))
            {
                inPart3 = null;
            }

            if (inPart2 == null && inPart3 == null)
            {
                outPart2 = inPart1;
                outPart1 = null;
                return;
            }
            else if (inPart2 == null && inPart3 != null)
            {
                outPart2 = inPart3;
                outPart1 = inPart1;
                return;
            }
            else if (inPart2 != null && inPart3 == null)
            {
                outPart2 = inPart2;
                outPart1 = inPart1;
                return;
            }
            else
            {
                // Must be (inPart2 != null && inPart3 != null)
                outPart2 = inPart2 + ComponentPartSeparator + inPart3;
                outPart1 = inPart1;
                return;
            }
        }

        public static string ConstructErrorMessage(string message, Exception exception, bool useNewLines)
        {
            if (message == null && exception == null)
            {
                return null;
            }
            else if (message != null && exception == null)
            {
                return message;
            }
            else if (message == null && exception != null)
            {
                var errorMsg = new StringBuilder();
                DetectReplaceNewLines(exception.ToString(), errorMsg, useNewLines, out bool hasEncounteredNewLines);

                if (useNewLines && hasEncounteredNewLines)
                {
                    errorMsg.Append(Environment.NewLine);
                }

                return errorMsg.ToString();
            }
            else
            {
                // So we have (message != null && exception != null)

                var errorMsg = new StringBuilder(message);
                if (message.Length > 0 && message[message.Length - 1] != '.')
                {
                    errorMsg.Append('.');
                }

                int sepPos = errorMsg.Length;
                DetectReplaceNewLines(exception.ToString(), errorMsg, useNewLines, out bool hasEncounteredNewLines);

                if (useNewLines && hasEncounteredNewLines)
                {
                    errorMsg.Insert(sepPos, Environment.NewLine);
                    errorMsg.Append(Environment.NewLine);
                }
                else
                {
                    errorMsg.Insert(sepPos, ' ');
                }

                return errorMsg.ToString();
            }
        }

        private static void DetectReplaceNewLines(string srcText, StringBuilder destBuffer, bool useNewLines, out bool hasEncounteredNewLines)
        {
            hasEncounteredNewLines = false;
            int p = 0;
            while(p < srcText.Length)
            {
                char c = srcText[p];
                if (c == '\n' || c == '\r')
                {
                    hasEncounteredNewLines = true;
                    if (useNewLines)
                    {
                        destBuffer.Append(c);
                        p++;
                    }
                    else
                    {
                        // Add space if the last char copied was not a white-space:
                        int destBufferLen = destBuffer.Length;
                        if (destBufferLen > 0 && !Char.IsWhiteSpace(destBuffer[destBufferLen - 1]))
                        {
                            destBuffer.Append(' ');
                        }

                        // Use the replacement and skip current char:
                        destBuffer.Append(NewLineReplacement);
                        p++;

                        // Skip all immediately following NL chars:
                        while (p < srcText.Length)
                        {
                            c = srcText[p];
                            if (c == '\n' || c == '\r')
                            {
                                p++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        // Next char is not NL. If it is not a white-space add a space now:
                        if (p < srcText.Length )
                        {
                            c = srcText[p];
                            if (!Char.IsWhiteSpace(c))
                            {
                                destBuffer.Append(' ');
                            }
                        }
                    }
                }
                else
                {
                    destBuffer.Append(c);
                    p++;
                }
            }
        }

        public static StringBuilder ConstructLogLine(string logLevelMoniker, string componentName, bool useUtcTimestamp, string message, IEnumerable<object> dataNamesAndValues, bool useNewLines)
        {
            return ConstructLogLine(logLevelMoniker, componentName, null, useUtcTimestamp, message, dataNamesAndValues, useNewLines);
        }

        public static StringBuilder ConstructLogLine(string logLevelMoniker,
                                                     string componentNamePart1,
                                                     string componentNamePart2,
                                                     bool useUtcTimestamp,
                                                     string message,
                                                     IEnumerable<object> dataNamesAndValues,
                                                     bool useNewLines)
        {
            var logLine = new StringBuilder(capacity: 128);
            AppendLogLinePrefix(logLine, logLevelMoniker, useUtcTimestamp);
            AppendEventInfo(logLine, componentNamePart1, componentNamePart2, message, dataNamesAndValues, useNewLines);

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
                                           IEnumerable<object> dataNamesAndValues,
                                           bool useNewLines)
        {
            bool hasComponentNamePart1 = !string.IsNullOrWhiteSpace(componentNamePart1);
            bool hasComponentNamePart2 = !string.IsNullOrWhiteSpace(componentNamePart2);

            if (hasComponentNamePart1)
            {
                targetBuffer.Append(componentNamePart1);
            }

            if (hasComponentNamePart1 && hasComponentNamePart2)
            {
                targetBuffer.Append(ComponentPartSeparator);
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

                char lastMsgChar = '\0';
                if (message.Length > 0)
                {
                    lastMsgChar = message[message.Length - 1];
                }

                if (lastMsgChar == '.')
                {
                    // Append space after '.':

                    if (useNewLines)
                    {
                        targetBuffer.AppendLine();
                    }
                    else
                    {
                        targetBuffer.Append(' ');
                    }
                }
                else if (lastMsgChar == '\r' || lastMsgChar == '\n')
                {
                    // Append nothing after New Line.
                }
                else
                {
                    // Append ". " in all other cases:
                    
                    if (useNewLines)
                    {
                        targetBuffer.AppendLine(".");
                    }
                    else
                    {
                        targetBuffer.Append(". ");
                    }
                }
            }

            if (dataNamesAndValues != null)
            {
                if (dataNamesAndValues is object[] dataNamesAndValuesArray)
                {
                    AppenddataNamesAndValuesArr(targetBuffer, dataNamesAndValuesArray, useNewLines);
                }
                else
                {
                    AppenddataNamesAndValuesEnum(targetBuffer, dataNamesAndValues, useNewLines);
                }
            }
        }

        private static void AppenddataNamesAndValuesArr(StringBuilder targetBuffer, object[] dataNamesAndValues, bool useNewLines)
        {
            const string IndentStr = "    ";

            if (dataNamesAndValues.Length < 1)
            {
                return;
            }

            for (int i = 0; i < dataNamesAndValues.Length; i += 2)
            {
                if (useNewLines)
                {
                    targetBuffer.AppendLine((i == 0) ? "{" : ",");
                }
                else
                {
                    targetBuffer.Append((i == 0) ? "{" : ", ");
                }

                if (useNewLines)
                {
                    targetBuffer.Append(IndentStr);
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

            if (useNewLines)
            {
                targetBuffer.AppendLine();
            }
            
            targetBuffer.Append('}');
        }

        private static void AppenddataNamesAndValuesEnum(StringBuilder targetBuffer, IEnumerable<object> dataNamesAndValues, bool useNewLines)
        {
            const string IndentStr = "    ";

            int enumIndex = 0;
            using (IEnumerator<object> enumerator = dataNamesAndValues.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (useNewLines)
                    {
                        targetBuffer.AppendLine((enumIndex == 0) ? "{" : ",");
                    }
                    else
                    {
                        targetBuffer.Append((enumIndex == 0) ? "{" : ", ");
                    }

                    if (useNewLines)
                    {
                        targetBuffer.Append(IndentStr);
                    }

                    targetBuffer.Append('[');
                    QuoteIfString(targetBuffer, enumerator.Current);
                    targetBuffer.Append(']');
                    enumIndex++;

                    targetBuffer.Append('=');

                    if (enumerator.MoveNext())
                    {
                        QuoteIfString(targetBuffer, enumerator.Current);
                        enumIndex++;
                    }
                    else
                    {
                        targetBuffer.Append(DataValueNotSpecifiedWord);
                    }
                }
            }

            if (enumIndex > 0)
            {
                if (useNewLines)
                {
                    targetBuffer.AppendLine();
                }

                targetBuffer.Append('}');
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
