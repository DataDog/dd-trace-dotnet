// <copyright file="MethodMetadataInfoFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Instrumentation.Collections;
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
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MethodMetadataInfoFactory));

        public static MethodMetadataInfo Create(MethodBase method, Type type)
        {
            var pdbData = ExtractFilePathAndLineNumbersFromPdb(method);

            return new MethodMetadataInfo(GetParameterNames(method), GetLocalVariableNames(method), type, method, pdbData.Item1, pdbData.Item2, pdbData.Item3, pdbData.Item4);
        }

        public static MethodMetadataInfo Create(MethodBase method, Type type, AsyncHelper.AsyncKickoffMethodInfo asyncKickOffInfo)
        {
            var pdbData = ExtractFilePathAndLineNumbersFromPdb(method);

            return new MethodMetadataInfo(
                GetParameterNames(method),
                GetLocalVariableNames(method),
                AsyncHelper.GetHoistedLocalsFromStateMachine(type, asyncKickOffInfo),
                AsyncHelper.GetHoistedArgumentsFromStateMachine(type, GetParameterNames(asyncKickOffInfo.KickoffMethod)),
                type,
                method,
                asyncKickOffInfo.KickoffParentType,
                asyncKickOffInfo.KickoffMethod,
                pdbData.Item1,
                pdbData.Item2,
                pdbData.Item3,
                pdbData.Item4);
        }

        private static Tuple<string, string, string, Dictionary<int, int>> ExtractFilePathAndLineNumbersFromPdb(MethodBase method)
        {
            try
            {
                var userSymbolMethod = Pdb.DatadogPdbReader.CreatePdbReader(method.Module.Assembly)?.ReadMethodSymbolInfo((int)(method.MetadataToken));
                var filePath = string.Empty;
                var methodBeginLineNumber = string.Empty;
                var methodEndLineNumber = string.Empty;
                Dictionary<int, int> ilOffsetToLineNumberMapping = null;

                if (userSymbolMethod != null && userSymbolMethod.SequencePoints != null && userSymbolMethod.SequencePoints.Any())
                {
                    const int hiddenSequencePoint = 0x00feefee;
                    filePath = userSymbolMethod.SequencePoints.First(sp => sp.Line != hiddenSequencePoint).Document.URL;
                    methodBeginLineNumber = userSymbolMethod.SequencePoints.First(sp => sp.Line != hiddenSequencePoint).Line.ToString();
                    methodEndLineNumber = userSymbolMethod.SequencePoints.Last(sp => sp.Line != hiddenSequencePoint).Line.ToString();

                    ilOffsetToLineNumberMapping = new Dictionary<int, int>();
                    foreach (var sp in userSymbolMethod.SequencePoints.Where(sp => sp.Line != hiddenSequencePoint))
                    {
                        ilOffsetToLineNumberMapping[sp.Offset] = sp.Line;
                    }
                }

                return Tuple.Create(filePath, methodBeginLineNumber, methodEndLineNumber, ilOffsetToLineNumberMapping);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to extract file path and line numbers from PDB for method: {Name}", method.Name);
                return Tuple.Create(string.Empty, string.Empty, string.Empty, (Dictionary<int, int>)null);
            }
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
                Log.Error(e, "Failed to obtain local variable names from PDB for {Name}", method.Name);
                return null;
            }
        }
    }
}
