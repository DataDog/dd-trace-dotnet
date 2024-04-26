// <copyright file="RuntimeHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace Datadog.Trace.Debugger.Helpers
{
    internal class RuntimeHelper
    {
        internal static bool IsNetOnward(int major)
        {
            return Environment.Version.Major >= major;
        }

        internal static bool IsModuleDebugCompiled(Assembly assembly)
        {
            var debuggableAttribute = assembly.GetCustomAttributes(typeof(DebuggableAttribute), false).FirstOrDefault() as DebuggableAttribute;
            return debuggableAttribute != null &&
                   (debuggableAttribute.IsJITOptimizerDisabled || debuggableAttribute.IsJITTrackingEnabled);
        }
    }
}
