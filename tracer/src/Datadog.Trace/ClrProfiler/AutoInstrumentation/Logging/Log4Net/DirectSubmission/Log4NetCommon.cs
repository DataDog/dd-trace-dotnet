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

        public static TResponseArray AddAppenderToResponse<TAppender>(TResponseArray originalResponseArray, TAppender appender)
        {
            try
            {
                if (originalResponseArray is null)
                {
                    return originalResponseArray;
                }

                var originalArray = (Array)(object)originalResponseArray;
                var originalArrayLength = originalArray.Length;

                var finalArray = Array.CreateInstance(AppenderElementType, originalArrayLength + 1);
                if (originalArrayLength > 0)
                {
                    Array.Copy(originalArray, finalArray, originalArrayLength);
                }

                _appenderProxy ??= appender.DuckImplement(AppenderElementType);
                finalArray.SetValue(_appenderProxy, finalArray.Length - 1);

                return (TResponseArray)(object)finalArray;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding Log4Net appender to response");
                return originalResponseArray;
            }
        }
    }
}
