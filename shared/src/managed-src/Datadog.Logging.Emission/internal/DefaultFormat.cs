using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Datadog.Logging.Emission
{
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1308:Variable names must not be prefixed", Justification = "Should not apply to statics")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1311:Static readonly fields must begin with upper-case letter", Justification = "Should only apply to vars that are logically const.")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1116:Split parameters must start on line after declaration", Justification = "Bad rule")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names must not contain underscore", Justification = "Underscore aid visibility in long names")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0007:Use implicit type", Justification = "Worst piece of advise Style tools ever gave.")]
    internal static class DefaultFormat
    {
        public class Options
        {
            public static readonly Options SingleLines = new Options(useUtcTimestamps: false, useNewLinesInErrorMessages: false, useNewLinesInDataNamesAndValues: false);
            public static readonly Options StructuredMultilines = new Options(useUtcTimestamps: false, useNewLinesInErrorMessages: true, useNewLinesInDataNamesAndValues: true);

            public Options(bool useUtcTimestamps, bool useNewLinesInErrorMessages, bool useNewLinesInDataNamesAndValues)
            {
                this.UseUtcTimestamps = useUtcTimestamps;
                this.UseNewLinesInErrorMessages = useNewLinesInErrorMessages;
                this.UseNewLinesInDataNamesAndValues = useNewLinesInDataNamesAndValues;
            }

            public bool UseUtcTimestamps { get; }
            public bool UseNewLinesInErrorMessages { get; }
            public bool UseNewLinesInDataNamesAndValues { get; }
        }

        public const string LogLevelMoniker_Error = "ERROR";
        public const string LogLevelMoniker_Info = "INFO ";
        public const string LogLevelMoniker_Debug = "DEBUG";

        private const string LogSourceNamePartsSeparator = "::";

        private const string TimestampPattern_Local = @"yyyy-MM-dd, HH\:mm\:ss\.fff \(zzz\)";
        private const string TimestampPattern_Utc = @"yyyy-MM-dd, HH\:mm\:ss\.fff";

        private const string NewLineReplacement = "^->";
        private const string NullWord = "null";
        private const string DataValueNotSpecifiedWord = "unspecified";
        private const string IndentStr = "    ";

        private const string BlockMoniker_DataNamesAndValues = "DATA=";
        private const string BlockMoniker_LogSourceInfo = "LOGSRC=";

        public static class LogSourceInfo
        {
            public static string MergeNames(string part1, string part2)
            {
                if (part1 == null && part2 == null)
                {
                    return null;
                }
                else if (part1 == null && part2 != null)
                {
                    return part2;
                }
                else if (part1 != null && part2 == null)
                {
                    return part1;
                }
                else // MUST BE (part1 != null && part2 != null)
                {
                    return part1 + LogSourceNamePartsSeparator + part2;
                }
            }
        }  // class DefaultFormat.LogSourceInfo

        public static class ErrorMessage
        {
            public static string Construct(string message, Exception exception, bool useNewLines)
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
                while (p < srcText.Length)
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
                            if (p < srcText.Length)
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
        }  // class DefaultFormat.LogSourceInfo

        public static class LogLine
        {
            private static readonly string s_procIdInfo = GetProcIdInfoString();

            public static StringBuilder Construct(string logLevelMoniker,
                                                  string logSourceNamePart1,
                                                  string logSourceNamePart2,
                                                  int logSourceCallLineNumber,
                                                  string logSourceCallMemberName,
                                                  string logSourceCallFileName,
                                                  string logSourceAssemblyName,
                                                  string message,
                                                  IEnumerable<object> dataNamesAndValues,
                                                  bool useUtcTimestamp,
                                                  bool useNewLines)
            {
                var logLine = new StringBuilder(capacity: 512);
                AppendPrefix(logLine, logLevelMoniker, useUtcTimestamp);
                AppendEventInfo(logLine,
                                logSourceNamePart1,
                                logSourceNamePart2,
                                logSourceCallLineNumber,
                                logSourceCallMemberName,
                                logSourceCallFileName,
                                logSourceAssemblyName,
                                message,
                                dataNamesAndValues,
                                useNewLines);

                AppendDataNamesAndValues(logLine, dataNamesAndValues, useNewLines);

                AppendLogSourceInfo(logLine,
                                    logSourceCallLineNumber,
                                    logSourceCallMemberName,
                                    logSourceCallFileName,
                                    logSourceAssemblyName,
                                    useNewLines);

                return logLine;
            }

            public static void AppendPrefix(StringBuilder targetBuffer, string logLevelMoniker, bool useUtcTimestamp)
            {
                if (targetBuffer == null)
                {
                    return;
                }

                targetBuffer.Append('[');
                AppendPrefixCore(targetBuffer, logLevelMoniker, useUtcTimestamp);
                targetBuffer.Append("] ");
            }

            public static void AppendPrefixCore(StringBuilder targetBuffer, string logLevelMoniker, bool useUtcTimestamp)
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

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "@ToDo review")]
            public static void AppendEventInfo(StringBuilder targetBuffer,
                                               string logSourceNamePart1,
                                               string logSourceNamePart2,
                                               int logSourceCallLineNumber,
                                               string logSourceCallMemberName,
                                               string logSourceCallFileName,
                                               string logSourceAssemblyName,
                                               string message,
                                               IEnumerable<object> dataNamesAndValues,
                                               bool useNewLines)
            {
                bool hasLogSourceNamePart1 = !String.IsNullOrWhiteSpace(logSourceNamePart1);
                bool hasLogSourceNamePart2 = !String.IsNullOrWhiteSpace(logSourceNamePart2);

                if (hasLogSourceNamePart1)
                {
                    targetBuffer.Append(logSourceNamePart1);
                }

                if (hasLogSourceNamePart1 && hasLogSourceNamePart2)
                {
                    targetBuffer.Append(LogSourceNamePartsSeparator);
                }

                if (hasLogSourceNamePart2)
                {
                    targetBuffer.Append(logSourceNamePart2);
                }

                if (hasLogSourceNamePart1 || hasLogSourceNamePart2)
                {
                    targetBuffer.Append(": ");
                }

                if (!String.IsNullOrWhiteSpace(message))
                {
                    targetBuffer.Append(message);

                    if (message.Length > 0)
                    {
                        char lastMsgChar = message[message.Length - 1];
                        if (lastMsgChar == '.' || lastMsgChar == '\r' || lastMsgChar == '\n')
                        {
                            // Append space after '.' or New Line, append nothing.
                        }
                        else
                        {
                            // Append '.' in all other cases:
                            targetBuffer.Append('.');
                        }
                    }
                }
            }

            public static void AppendDataNamesAndValues(StringBuilder targetBuffer, IEnumerable<object> dataNamesAndValues, bool useNewLines)
            {
                if (targetBuffer == null || dataNamesAndValues == null)
                {
                    return;
                }

                if (dataNamesAndValues is object[] dataNamesAndValuesArray)
                {
                    AppendDataNamesAndValuesArr(targetBuffer, dataNamesAndValuesArray, useNewLines);
                }
                else
                {
                    AppendDataNamesAndValuesEnum(targetBuffer, dataNamesAndValues, useNewLines);
                }
            }

            private static void AppendDataNamesAndValuesArr(StringBuilder targetBuffer, object[] dataNamesAndValues, bool useNewLines)
            {
                const string BlockMonikerWithOpeningBrace = BlockMoniker_DataNamesAndValues + "{";

                if (dataNamesAndValues.Length < 1)
                {
                    return;
                }

                for (int i = 0; i < dataNamesAndValues.Length; i += 2)
                {
                    if (i == 0)
                    {
                        AppendSectionWhitespaceSeparator(targetBuffer, useNewLines);

                        if (useNewLines)
                        {
                            targetBuffer.AppendLine(BlockMonikerWithOpeningBrace);
                            targetBuffer.Append(IndentStr);
                        }
                        else
                        {
                            targetBuffer.Append(BlockMonikerWithOpeningBrace);
                        }
                    }
                    else
                    {
                        if (useNewLines)
                        {
                            targetBuffer.AppendLine(",");
                            targetBuffer.Append(IndentStr);
                        }
                        else
                        {
                            targetBuffer.Append(", ");
                        }
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

            private static void AppendDataNamesAndValuesEnum(StringBuilder targetBuffer, IEnumerable<object> dataNamesAndValues, bool useNewLines)
            {
                const string BlockMonikerWithOpeningBrace = BlockMoniker_DataNamesAndValues + "{";

                int enumIndex = 0;
                using (IEnumerator<object> enumerator = dataNamesAndValues.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        if (enumIndex == 0)
                        {
                            AppendSectionWhitespaceSeparator(targetBuffer, useNewLines);

                            if (useNewLines)
                            {
                                targetBuffer.AppendLine(BlockMonikerWithOpeningBrace);
                                targetBuffer.Append(IndentStr);
                            }
                            else
                            {
                                targetBuffer.Append(BlockMonikerWithOpeningBrace);
                            }
                        }
                        else
                        {
                            if (useNewLines)
                            {
                                targetBuffer.AppendLine(",");
                                targetBuffer.Append(IndentStr);
                            }
                            else
                            {
                                targetBuffer.Append(", ");
                            }
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

            public static void AppendLogSourceInfo(StringBuilder targetBuffer,
                                                   int logSourceCallLineNumber,
                                                   string logSourceCallMemberName,
                                                   string logSourceCallFileName,
                                                   string logSourceAssemblyName,
                                                   bool useNewLines)
            {
                if ((targetBuffer == null)
                        || (logSourceCallLineNumber == 0
                            && logSourceCallMemberName == null
                            && logSourceCallFileName == null
                            && logSourceAssemblyName == null))
                {
                    return;
                }

                AppendSectionWhitespaceSeparator(targetBuffer, useNewLines);

                bool isFirstElement = true;

                if (logSourceCallMemberName != null)
                {
                    AppendLogSourceInfoElement(targetBuffer, "CallMemberName", logSourceCallMemberName, isFirstElement, useNewLines);
                    isFirstElement = false;
                }

                if (logSourceCallLineNumber != 0 || logSourceCallMemberName != null)
                {
                    AppendLogSourceInfoElement(targetBuffer, "CallLineNumber", logSourceCallLineNumber, isFirstElement, useNewLines);
                    isFirstElement = false;
                }

                if (logSourceCallFileName != null)
                {
                    AppendLogSourceInfoElement(targetBuffer, "CallFileName", logSourceCallFileName, isFirstElement, useNewLines);
                    isFirstElement = false;
                }

                if (logSourceAssemblyName != null)
                {
                    AppendLogSourceInfoElement(targetBuffer, "AssemblyName", logSourceAssemblyName, isFirstElement, useNewLines);
                }

                if (useNewLines)
                {
                    targetBuffer.AppendLine();
                }

                targetBuffer.Append('}');
            }

            private static void AppendSectionWhitespaceSeparator(StringBuilder targetBuffer, bool useNewLines)
            {
                if (targetBuffer.Length > 0)
                {
                    char lastBufferChar = targetBuffer[targetBuffer.Length - 1];
                    if (useNewLines)
                    {
                        if (lastBufferChar != '\r' && lastBufferChar != '\n')
                        {
                            targetBuffer.AppendLine();
                        }
                    }
                    else
                    {
                        if (!Char.IsWhiteSpace(lastBufferChar))
                        {
                            targetBuffer.Append(' ');
                        }
                    }
                }
            }

            private static void AppendLogSourceInfoElement<T>(StringBuilder targetBuffer,
                                                              string logSourceInfoElementName,
                                                              T logSourceInfoElementValue,
                                                              bool isFirstElement,
                                                              bool useNewLines)
            {
                const string BlockMonikerWithOpeningBrace = BlockMoniker_LogSourceInfo + "{";

                if (useNewLines)
                {
                    targetBuffer.AppendLine(isFirstElement ? BlockMonikerWithOpeningBrace : ",");
                    targetBuffer.Append(IndentStr);
                }
                else
                {
                    targetBuffer.Append(isFirstElement ? BlockMonikerWithOpeningBrace : ", ");
                }

                targetBuffer.Append('[');
                targetBuffer.Append(logSourceInfoElementName);
                targetBuffer.Append("]=");
                QuoteIfString<T>(targetBuffer, logSourceInfoElementValue);
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
        }  // class DefaultFormat.LogLine
    }
}
