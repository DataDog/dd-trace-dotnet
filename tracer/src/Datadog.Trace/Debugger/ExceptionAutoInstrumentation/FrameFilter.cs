// <copyright file="FrameFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation.ThirdParty;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Logging;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal static class FrameFilter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FrameFilter));

        internal static bool IsDatadogAssembly(string assemblyName)
        {
            return assemblyName?.StartsWith("datadog.", StringComparison.OrdinalIgnoreCase) == true;
        }

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

            var moduleName = GetModuleNameWithoutExtension(method.Module.Name);

            return string.IsNullOrEmpty(moduleName) ||
                   ThirdPartyModules.Contains(moduleName) ||
                   AssemblyFilter.ShouldSkipAssembly(method.Module.Assembly);
        }

        private static string GetModuleNameWithoutExtension(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return moduleName;
            }

            try
            {
                var lastPeriod = moduleName.LastIndexOf('.');
                if (lastPeriod == -1)
                {
                    return moduleName;
                }

                if (lastPeriod == moduleName.Length - 1)
                {
                    return moduleName.Substring(0, moduleName.Length - 1);
                }

                var ext = moduleName.Remove(0, lastPeriod + 1).ToLower();

                if (ext == "dll" ||
                    ext == "exe" ||
                    ext == "so")
                {
                    return moduleName.Substring(0, lastPeriod);
                }

                return moduleName;
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed tlo get the name of {ModuleName} without extension", moduleName);

                throw;
            }
        }

        internal static bool ShouldSkipNamespaceIfOnTopOfStack(MethodBase method)
        {
            return IsBlockList(method);
        }
    }
}
