// <copyright file="Log4NetCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission
{
    internal class Log4NetCommon<TResponseArray>
    {
        private static readonly Type _appenderElementType;
        private static object _appenderProxy;

        static Log4NetCommon()
        {
            _appenderElementType = typeof(TResponseArray).GetElementType();
        }

        public static TResponseArray AddAppenderToResponse(TResponseArray originalResponseArray, DirectSubmissionLog4NetAppender appender)
        {
            var originalArray = (Array)(object)originalResponseArray;
            var originalArrayLength = originalArray.Length;

            var finalArray = Array.CreateInstance(_appenderElementType, originalArrayLength + 1);
            Array.Copy(originalArray, finalArray, originalArrayLength);

            _appenderProxy ??= appender.DuckCast(_appenderElementType);
            finalArray.SetValue(_appenderProxy, finalArray.Length - 1);

            return (TResponseArray)(object)finalArray;
        }
    }
}
