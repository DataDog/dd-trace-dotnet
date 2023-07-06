// <copyright file="Result.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Result : IResult
    {
        private readonly DDWAF_RET_CODE returnCode;

        public Result(DdwafResultStruct returnStruct, DDWAF_RET_CODE returnCode, ulong aggregatedTotalRuntime, ulong aggregatedTotalRuntimeWithBindings)
        {
            this.returnCode = returnCode;
            Actions = new((int)returnStruct.ActionsSize);
            ReadActions(returnStruct);
            ShouldBlock = Actions.Contains("block");
            ShouldBeReported = returnCode >= DDWAF_RET_CODE.DDWAF_MATCH;
            AggregatedTotalRuntime = aggregatedTotalRuntime;
            AggregatedTotalRuntimeWithBindings = aggregatedTotalRuntimeWithBindings;
            Data = ShouldBeReported ? Marshal.PtrToStringAnsi(returnStruct.Data) : string.Empty;
            Timeout = returnStruct.Timeout;
        }

        public ReturnCode ReturnCode => Encoder.DecodeReturnCode(returnCode);

        public string Data { get; }

        public List<string> Actions { get; }

        /// <summary>
        /// Gets the total runtime in microseconds
        /// </summary>
        public ulong AggregatedTotalRuntime { get; }

        /// <summary>
        /// Gets the total runtime in microseconds with parameter passing to the waf
        /// </summary>
        public ulong AggregatedTotalRuntimeWithBindings { get; }

        public bool ShouldBlock { get; }

        public bool ShouldBeReported { get; }

        public bool Timeout { get; }

        private void ReadActions(DdwafResultStruct returnStruct)
        {
            var pointer = returnStruct.ActionsArray;
            for (var i = 0; i < returnStruct.ActionsSize; i++)
            {
                var pointerString = Marshal.ReadIntPtr(pointer, IntPtr.Size * i);
                var action = Marshal.PtrToStringAnsi(pointerString);
                Actions.Add(action);
            }
        }
    }
}
