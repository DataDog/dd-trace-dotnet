// <copyright file="FrameFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.Debugger.Symbols;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal static class FrameFilter
    {
        internal static bool IsUserCode(in ParticipatingFrame participatingFrameToRejit)
        {
            return IsUserCode(participatingFrameToRejit.Method);
        }

        private static bool IsUserCode(MethodBase method)
        {
            if (method == null)
            {
                return false;
            }

            var declaringType = method.DeclaringType;

            if (declaringType == null)
            {
                return false;
            }

            var namespaceName = declaringType.Namespace;

            if (namespaceName == null)
            {
                return false;
            }

            return IsBlockList(method) == false;
        }

        internal static bool IsBlockList(MethodBase method)
        {
            if (method == null)
            {
                return true;
            }

            if (method is DynamicMethod || method.Module.Assembly.IsDynamic)
            {
                return true;
            }

            if (method.GetType().Name.Equals("RTDynamicMethod", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (method.DeclaringType == null)
            {
                return true;
            }

            return AssemblyFilter.ShouldSkipAssembly(method.Module.Assembly, LiveDebugger.Instance.Settings.ThirdPartyDetectionExcludes, LiveDebugger.Instance.Settings.ThirdPartyDetectionIncludes);
        }

        internal static bool ShouldSkipNamespaceIfOnTopOfStack(MethodBase method)
        {
            return IsBlockList(method);
        }
    }
}
