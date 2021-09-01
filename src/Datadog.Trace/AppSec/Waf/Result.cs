// <copyright file="Result.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Result : IResult
    {
        private DdwafResultStruct returnStruct;
        private DDWAF_RET_CODE returnCode;

        private bool disposed;

        public Result(DdwafResultStruct returnStruct, DDWAF_RET_CODE returnCode)
        {
            this.returnStruct = returnStruct;
            this.returnCode = returnCode;
        }

        ~Result()
        {
            Dispose(false);
        }

        public ReturnCode ReturnCode
        {
            get { return Encoder.DecodeReturnCode(returnCode); }
        }

        public string Data
        {
            get { return Marshal.PtrToStringAnsi(returnStruct.Data); }
        }

        public void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            WafNative.ResultFree(ref returnStruct);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
