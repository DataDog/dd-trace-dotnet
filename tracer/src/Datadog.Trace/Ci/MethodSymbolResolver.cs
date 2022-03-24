// <copyright file="MethodSymbolResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb;

namespace Datadog.Trace.Ci
{
    internal class MethodSymbolResolver
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MethodSymbolResolver));
        private static readonly Lazy<MethodSymbolResolver> _instance = new(() => new MethodSymbolResolver());

        private readonly Dictionary<Module, ModuleDefMD> _modulesDefMDs = new();

        private MethodSymbolResolver()
        {
        }

        /// <summary>
        /// Gets the singleton instance
        /// </summary>
        public static MethodSymbolResolver Instance => _instance.Value;

        /// <summary>
        /// Try get the method symbols from the MethodInfo
        /// </summary>
        /// <param name="methodInfo">MethodInfo instance</param>
        /// <param name="methodSymbol">MethodSymbol instance</param>
        /// <returns>true if the method symbol struct could be retrieved, otherwise; false</returns>
        public bool TryGetMethodSymbol(MethodInfo methodInfo, out MethodSymbol methodSymbol)
        {
            if (methodInfo is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(methodInfo));
            }

            if (!AppDomain.CurrentDomain.IsFullyTrusted)
            {
                methodSymbol = default;
                return false;
            }

            // Try to load ModuleDefMD from cache
            ModuleDefMD moduleDef = null;
            lock (_modulesDefMDs)
            {
                var module = methodInfo.Module;
                if (!_modulesDefMDs.TryGetValue(module, out moduleDef))
                {
                    var options = new ModuleCreationOptions(ModuleDef.CreateModuleContext());
                    try
                    {
                        var mDef = ModuleDefMD.Load(module, options);
                        // We enable the type cache
                        mDef.EnableTypeDefFindCache = true;

                        // Check if the module has pdb info
                        if (mDef.PdbState is not null)
                        {
                            moduleDef = mDef;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error loading method symbols.");
                    }

                    _modulesDefMDs.Add(module, moduleDef);
                }
            }

            // If not ModuleDefMD is found then we cannot do nothing.
            if (moduleDef is null)
            {
                methodSymbol = default;
                return false;
            }

            try
            {
                string file = null;
                SequencePoint first = null;
                SequencePoint last = null;

                var method = (MethodDef)moduleDef.ResolveToken(methodInfo.MetadataToken);
                var body = method.Body;

                // If the method is async, we need to switch to the MoveNext method
                if (method.HasCustomAttributes)
                {
                    var asyncStateMachineAttribute = method.CustomAttributes.Find(typeof(AsyncStateMachineAttribute).FullName);
                    if (asyncStateMachineAttribute != null)
                    {
                        var attrArgument = (ClassSig)asyncStateMachineAttribute.ConstructorArguments[0].Value;
                        var attrArgumentTypeDef = attrArgument.TypeDef;
                        var asyncMethod = attrArgumentTypeDef.FindMethod("MoveNext");
                        body = asyncMethod.Body;
                    }
                }

                // Extract file and the first and last sequencePoint;
                if (body.HasInstructions)
                {
                    const int HIDDEN = 0xFEEFEE;
                    var lastIndex = body.Instructions.Count - 1;
                    for (var i = 0; i < body.Instructions.Count; i++)
                    {
                        if (file is null || first is null)
                        {
                            // We extract the file and the start method line searching from the top
                            var fromTop = body.Instructions[i];
                            if (fromTop?.SequencePoint is not null)
                            {
                                file ??= fromTop.SequencePoint.Document.Url;
                                if (first is null && fromTop.SequencePoint.StartLine != HIDDEN)
                                {
                                    first = fromTop.SequencePoint;
                                }
                            }
                        }

                        if (last is null)
                        {
                            // We extract the end method line searching from the bottom
                            var fromBottom = body.Instructions[lastIndex - i];
                            if (fromBottom?.SequencePoint is not null && fromBottom.SequencePoint.EndLine != HIDDEN)
                            {
                                last = fromBottom.SequencePoint;
                            }
                        }

                        if (file is not null &&
                            first is not null &&
                            last is not null)
                        {
                            // All data collected.
                            break;
                        }
                    }

                    methodSymbol = new MethodSymbol(file, first?.StartLine ?? 0, last?.EndLine ?? 0);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading method symbols.");
            }

            methodSymbol = default;
            return false;
        }

        /// <summary>
        /// Clear modules cache
        /// </summary>
        public void Clear()
        {
            lock (_modulesDefMDs)
            {
                foreach (var modulesDefMD in _modulesDefMDs)
                {
                    if (modulesDefMD.Value is { } moduleDef)
                    {
                        moduleDef.Dispose();
                    }
                }

                _modulesDefMDs.Clear();
            }
        }

        /// <summary>
        /// Method symbols struct
        /// </summary>
        public readonly ref struct MethodSymbol
        {
            /// <summary>
            /// File Path
            /// </summary>
            public readonly string File;

            /// <summary>
            /// Start line
            /// </summary>
            public readonly int StartLine;

            /// <summary>
            /// End line
            /// </summary>
            public readonly int EndLine;

            public MethodSymbol(string file, int startLine, int endLine)
            {
                File = file;
                StartLine = startLine;
                EndLine = endLine;
            }
        }
    }
}
