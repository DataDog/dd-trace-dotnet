// <copyright file="Log4NetCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission
{
    internal class Log4NetCommon<TResponseArray>
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Log4NetCommon<TResponseArray>));

        // ReSharper disable StaticMemberInGenericType
        private static readonly Type AppenderElementType;
        private static object? _appenderProxy;

        static Log4NetCommon()
        {
            AppenderElementType = typeof(TResponseArray).GetElementType()!;
        }

        public static bool TryAddAppenderToResponse<TAppender>(TResponseArray originalResponseArray, TAppender appender, out TResponseArray updatedResponseArray)
        {
            try
            {
                if (originalResponseArray is null)
                {
                    updatedResponseArray = originalResponseArray;
                    return false;
                }

                var originalArray = (Array)(object)originalResponseArray;
                var originalArrayLength = originalArray.Length;

                var finalArray = Array.CreateInstance(AppenderElementType, originalArrayLength + 1);
                if (originalArrayLength > 0)
                {
                    Array.Copy(originalArray, finalArray, originalArrayLength);
                }

                if (_appenderProxy is null)
                {
                    if (appender is null)
                    {
                        Log.Error("Error adding Log4Net appender to response: appender is null");
                        updatedResponseArray = originalResponseArray;
                        return false;
                    }

                    _appenderProxy = appender.DuckImplement(AppenderElementType);
                }

                finalArray.SetValue(_appenderProxy, finalArray.Length - 1);
                updatedResponseArray = (TResponseArray)(object)finalArray;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding Log4Net appender to response");
                updatedResponseArray = originalResponseArray;
                return false;
            }
        }
    }
}
