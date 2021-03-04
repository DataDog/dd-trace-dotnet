using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Datadog.Logging.Emission
{
    internal struct LogSourceInfo
    {
        public string NamePart1 { get; }
        public string NamePart2 { get; }
        public int CallLineNumber { get; }
        public string CallMemberName { get; }
        public string CallFileName { get; }

        public LogSourceInfo(string name)
            : this(namePart1: null, namePart2: name, callLineNumber: 0, callMemberName: null, callFileName: null)
        { }

        public LogSourceInfo(string namePart1, string namePart2)
            : this(namePart1, namePart2, callLineNumber: 0, callMemberName: null, callFileName: null)
        { }

        public LogSourceInfo(string namePart1, string namePart2, int callLineNumber, string callMemberName)
            : this(namePart1, namePart2, callLineNumber, callMemberName, callFileName: null)
        { }

        public LogSourceInfo(string namePart1, string namePart2, int callLineNumber, string callMemberName, string callFileName)
        {
            NamePart1 = namePart1;
            NamePart2 = namePart2;
            CallLineNumber = callLineNumber;
            CallMemberName = callMemberName;
            CallFileName = callFileName;
        }

        public LogSourceInfo WithCallInfo([CallerLineNumber] int callLineNumber = 0, [CallerMemberName] string callMemberName = null)
        {
            return new LogSourceInfo(NamePart1, NamePart2, callLineNumber, callMemberName, callFileName: null);
        }

        public LogSourceInfo WithSrcFileInfo([CallerLineNumber] int callLineNumber = 0,
                                             [CallerMemberName] string callMemberName = null,
                                             [CallerFilePath] string callFilePath = null)
        {
            string callFileName = null;
            if (!String.IsNullOrWhiteSpace(callFilePath))
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

            return new LogSourceInfo(NamePart1, NamePart2, callLineNumber, callMemberName, callFileName);
        }

        public LogSourceInfo WithName(string name)
        {
            return new LogSourceInfo(namePart1: null, namePart2: name, CallLineNumber, CallMemberName, CallFileName);
        }

        public LogSourceInfo WithName(string namePart1, string namePart2)
        {
            return new LogSourceInfo(namePart1, namePart2, CallLineNumber, CallMemberName, CallFileName);
        }

        public LogSourceInfo WithinComponentGroup(string superGroupName)
        {
            if (superGroupName == null)
            {
                return this;
            }

            if (NamePart1 == null && NamePart2 == null)
            {
                return WithName(null, superGroupName);
            }
            
            if (NamePart1 == null && NamePart2 != null)
            {
                return WithName(superGroupName, NamePart2);
            }
            
            if (NamePart1 != null && NamePart2 == null)
            {
                return WithName(superGroupName, NamePart1);
            }

            // Must be (NamePart1 != null && NamePart2 != null)
            
            return WithName(superGroupName, DefaultFormat.MergeLogSourceName(NamePart1, NamePart2));
        }

        public LogSourceInfo WithComponentSubgroup(string subGroupName)
        {
            if (subGroupName == null)
            {
                return this;
            }

            if (NamePart1 == null && NamePart2 == null)
            {
                return WithName(null, subGroupName);
            }

            if (NamePart1 == null && NamePart2 != null)
            {
                return WithName(NamePart2, subGroupName);
            }

            if (NamePart1 != null && NamePart2 == null)
            {
                return WithName(NamePart1, subGroupName);
            }

            // Must be (NamePart1 != null && NamePart2 != null)

            return WithName(DefaultFormat.MergeLogSourceName(NamePart1, NamePart2), subGroupName);
        }

    }
}
