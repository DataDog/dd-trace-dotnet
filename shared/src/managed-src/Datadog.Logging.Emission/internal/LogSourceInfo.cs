// <copyright file="LogSourceInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Runtime.CompilerServices;

namespace Datadog.Logging.Emission
{
    internal struct LogSourceInfo
    {
#pragma warning disable SA1308 // Variable names should not be prefixed
        private static string s_assemblyName = null;
#pragma warning restore SA1308 // Variable names should not be prefixed

        public LogSourceInfo(string logSourceName)
            : this(logSourceNamePart1: null, logSourceNamePart2: logSourceName, callLineNumber: 0, callMemberName: null, callFileName: null, assemblyName: null)
        {
        }

        public LogSourceInfo(string logSourceNamePart1, string logSourceNamePart2, int callLineNumber, string callMemberName, string callFileName, string assemblyName)
        {
            LogSourceNamePart1 = logSourceNamePart1;
            LogSourceNamePart2 = logSourceNamePart2;
            CallLineNumber = callLineNumber;
            CallMemberName = callMemberName;
            CallFileName = callFileName;
            AssemblyName = assemblyName;
        }

        public string LogSourceNamePart1 { get; }
        public string LogSourceNamePart2 { get; }
        public int CallLineNumber { get; }
        public string CallMemberName { get; }
        public string CallFileName { get; }
        public string AssemblyName { get; }

        public LogSourceInfo WithCallInfo([CallerLineNumber] int callLineNumber = 0, [CallerMemberName] string callMemberName = null)
        {
            return new LogSourceInfo(LogSourceNamePart1, LogSourceNamePart2, callLineNumber, callMemberName, callFileName: null, AssemblyName);
        }

        public LogSourceInfo WithSrcFileInfo(
                                [CallerLineNumber] int callLineNumber = 0,
                                [CallerMemberName] string callMemberName = null,
                                [CallerFilePath] string callFilePath = null)
        {
            string callFileName = null;
            if (callFilePath != null)
            {
                try
                {
                    callFileName = Path.GetFileName(callFilePath);
                }
                catch
                {
                    callFileName = null;
                }
            }

            return new LogSourceInfo(LogSourceNamePart1, LogSourceNamePart2, callLineNumber, callMemberName, callFileName, AssemblyName);
        }

        public LogSourceInfo WithAssemblyName()
        {
            string assemblyName = GetAssemblyName();
            return new LogSourceInfo(LogSourceNamePart1, LogSourceNamePart2, CallLineNumber, CallMemberName, CallFileName, assemblyName);
        }

        public LogSourceInfo WithinLogSourcesGroup(string superGroupName)
        {
            if (superGroupName == null)
            {
                return this;
            }

            if (LogSourceNamePart1 == null && LogSourceNamePart2 == null)
            {
                return WithLogSourcesName(null, superGroupName);
            }

            if (LogSourceNamePart1 == null && LogSourceNamePart2 != null)
            {
                return WithLogSourcesName(superGroupName, LogSourceNamePart2);
            }

            if (LogSourceNamePart1 != null && LogSourceNamePart2 == null)
            {
                return WithLogSourcesName(superGroupName, LogSourceNamePart1);
            }

            // Must be (LogSourceNamePart1 != null && LogSourceNamePart2 != null)

            return WithLogSourcesName(superGroupName, DefaultFormat.LogSourceInfo.MergeNames(LogSourceNamePart1, LogSourceNamePart2));
        }

        public LogSourceInfo WithLogSourcesSubgroup(string subGroupName)
        {
            if (subGroupName == null)
            {
                return this;
            }

            if (LogSourceNamePart1 == null && LogSourceNamePart2 == null)
            {
                return WithLogSourcesName(null, subGroupName);
            }

            if (LogSourceNamePart1 == null && LogSourceNamePart2 != null)
            {
                return WithLogSourcesName(LogSourceNamePart2, subGroupName);
            }

            if (LogSourceNamePart1 != null && LogSourceNamePart2 == null)
            {
                return WithLogSourcesName(LogSourceNamePart1, subGroupName);
            }

            // Must be (LogSourceNamePart1 != null && LogSourceNamePart2 != null)

            return WithLogSourcesName(DefaultFormat.LogSourceInfo.MergeNames(LogSourceNamePart1, LogSourceNamePart2), subGroupName);
        }

        private LogSourceInfo WithLogSourcesName(string logSourceNamePart1, string logSourceNamePart2)
        {
            return new LogSourceInfo(logSourceNamePart1, logSourceNamePart2, CallLineNumber, CallMemberName, CallFileName, AssemblyName);
        }

        private string GetAssemblyName()
        {
            string assemblyName = s_assemblyName;
            if (assemblyName == null)
            {
                try
                {
                    assemblyName = this.GetType().Assembly?.FullName;
                }
                catch
                {
                    assemblyName = null;
                }

                s_assemblyName = assemblyName;  // benign race
            }

            return assemblyName;
        }
    }
}