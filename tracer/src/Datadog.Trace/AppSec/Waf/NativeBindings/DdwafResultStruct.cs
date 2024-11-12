// <copyright file="DdwafResultStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct DdwafResultStruct
    {
        /** Whether there has been a timeout during the operation **/
        public byte Timeout;
        /** Array of events generated, this is guaranteed to be an array **/
        public DdwafObjectStruct Events;
        /** Array of actions generated, this is guaranteed to be an array **/
        public DdwafObjectStruct Actions;
        /** Map containing all derived objects in the format (address, value) **/
        public DdwafObjectStruct Derivatives;
        /** Total WAF runtime in nanoseconds **/
        public ulong TotalRuntime;
    }
}
