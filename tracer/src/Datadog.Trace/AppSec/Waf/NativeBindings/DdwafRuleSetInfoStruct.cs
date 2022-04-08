// <copyright file="DdwafRuleSetInfoStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct DdwafRuleSetInfoStruct
    {
        /// <summary>
        /// Number of rules successfully loaded
        /// </summary>
        public ushort Loaded;

        /// <summary>
        /// Number of rules which failed to parse
        /// </summary>
        public ushort Failed;

        /// <summary>
        /// Map from an error string to an array of all the rule ids for which that error was raised. { error: [rule_ids]}
        /// </summary>
        public DdwafObjectStruct Errors;

        /// <summary>
        /// Ruleset version
        /// </summary>
        public IntPtr Version;
    }
}
