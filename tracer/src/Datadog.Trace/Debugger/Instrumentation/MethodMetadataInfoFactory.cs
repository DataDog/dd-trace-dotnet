// <copyright file="MethodMetadataInfoFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// Responsible to create <see cref="MethodMetadataInfo"/> structures.
    /// </summary>
    internal static class MethodMetadataInfoFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<MethodMetadataInfo>();

        public static MethodMetadataInfo Create(MethodBase method, Type type)
        {
            return new MethodMetadataInfo(GetParameterNames(method), GetLocalVariableNames(method), type, method);
        }

        public static MethodMetadataInfo Create<TTarget>(MethodBase method, TTarget targetObject, Type type)
        {
            return new MethodMetadataInfo(GetParameterNames(method), GetLocalVariableNames(method), AsyncHelper.GetHoistedLocalsFromStateMachine(targetObject), type, method);
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
                using var pdbReader = DatadogPdbReader.CreatePdbReader(method.Module.Assembly);
                if (pdbReader == null)
                {
                    return null; // PDB file could not be loaded
                }

                var symbolMethod = pdbReader.ReadMethodSymbolInfo(method.MetadataToken);
                if (symbolMethod == null)
                {
                    return null; // Method was not found in PDB file
                }

                var methodBody = method.GetMethodBody();
                if (methodBody == null)
                {
                    return null; // Could not read method body, so we can't verify locals
                }

                var localVariables = symbolMethod.GetLocalVariables();
                int localVariablesCount = methodBody.LocalVariables.Count;
                string[] localNames = new string[localVariablesCount];
                foreach (var local in localVariables)
                {
                    if (local.Attributes.HasFlag(PdbLocalAttributes.DebuggerHidden))
                    {
                        continue;
                    }

                    if (local.Index > localVariablesCount)
                    {
                        // PDB information is inconsistent with the locals that are actually in the metadata.
                        // This might be caused by code obfuscation tools that try to remove/modify locals, and neglect to update the PDB.
                        // We'll simply ignore these additional locals in the hope that things will work out for the best.
                        continue;
                    }

                    localNames[local.Index] = local.Name;
                }

                return localNames;
            }
            catch (Exception e)
            {
                Log.Error(e, $"Failed to obtain local variable names from PDB for {method.Name}");
                return null;
            }
        }
    }
}
