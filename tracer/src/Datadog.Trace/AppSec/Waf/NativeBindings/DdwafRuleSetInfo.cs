// <copyright file="DdwafRuleSetInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
#pragma warning disable SA1401
#pragma warning disable CS0649

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    /// <summary>
    /// This must be a class because it can sometimes be null
    /// the waf only needs it when updating rules. For other updates, this won't be filled and will be misleading
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal class DdwafRuleSetInfo
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
