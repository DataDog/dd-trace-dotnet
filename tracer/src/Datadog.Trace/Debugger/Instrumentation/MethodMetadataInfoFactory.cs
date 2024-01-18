// <copyright file="MethodMetadataInfoFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// Responsible to create <see cref="MethodMetadataInfo"/> structures.
    /// </summary>
    internal static class MethodMetadataInfoFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MethodMetadataInfoFactory));

        public static MethodMetadataInfo Create(MethodBase method, Type type)
        {
            return new MethodMetadataInfo(GetParameterNames(method), GetLocalVariableNames(method), type, method);
        }

        public static MethodMetadataInfo Create(MethodBase method, Type type, AsyncHelper.AsyncKickoffMethodInfo asyncKickOffInfo)
        {
            return new MethodMetadataInfo(
                GetParameterNames(method),
                GetLocalVariableNames(method),
                AsyncHelper.GetHoistedLocalsFromStateMachine(type, asyncKickOffInfo),
                AsyncHelper.GetHoistedArgumentsFromStateMachine(type, GetParameterNames(asyncKickOffInfo.KickoffMethod)),
                type,
                method,
                asyncKickOffInfo.KickoffParentType,
                asyncKickOffInfo.KickoffMethod);
        }

        private static string[] GetParameterNames(MethodBase method)
        {
            return method.GetParameters()?.Select(parameter => parameter.Name).ToArray() ?? Array.Empty<string>();
        }

        /// <summary>
        /// Gets the local variable names from the PDB.
        /// </summary>
        /// <param name="method">The method for which we are requesting the local variables</param>
        /// <returns>
        /// The names of the method's local variable, in the same order as they appear in the method's LocalVarSig.
        /// May contain null entries to denote compiler generated locals whose names are meaningless.
        /// </returns>
        private static string[] GetLocalVariableNames(MethodBase method)
        {
            try
            {
                var methodBody = method.GetMethodBody();
                if (methodBody == null)
                {
                    return null; // Could not read method body, so we can't verify locals
                }

                using var pdbReader = DatadogMetadataReader.CreatePdbReader(method.Module.Assembly);
                return pdbReader?.GetLocalVariableNames(method.MetadataToken, methodBody.LocalVariables.Count, true);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to obtain local variable names from PDB for {Type}.{Name}", method.DeclaringType?.Name, method.Name);
                return null;
            }
        }
    }
}
