// <copyright file="DatadogMetadataReader.Dnlib.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Dss;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Managed;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Portable;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;

#nullable enable
namespace Datadog.Trace.Pdb
{
    internal partial class DatadogMetadataReader : IDisposable
    {
        private readonly ModuleDefMD? _dnlibModule;

        private SymbolReader? DnlibPdbReader { get; }

        private MethodDef? GetMethodDefDnlib(uint methodRid)
        {
            return _dnlibModule?.ResolveMethod(methodRid);
        }

        private List<SymbolScope>? GetAllScopes(uint rowId, bool searchMoveNext)
        {
            var method = GetSymbolMethodDnlib(rowId, searchMoveNext);
            if (method == null)
            {
                return null;
            }

            var result = new List<SymbolScope>();
            RetrieveAllNestedScopes(method.RootScope, result);
            return result;

            void RetrieveAllNestedScopes(SymbolScope? scope, List<SymbolScope> scopes)
            {
                // Recursively extract all nested scopes in method
                if (scope == null)
                {
                    return;
                }

                scopes.Add(scope);
                foreach (var innerScope in scope.Children)
                {
                    RetrieveAllNestedScopes(innerScope, scopes);
                }
            }
        }

        private string[]? GetLocalVariableNamesDnlib(int methodRid, int localVariablesCount, bool searchMoveNext)
        {
            if (GetSymbolMethodDnlib((uint)methodRid, searchMoveNext) is not { } symbolMethod)
            {
                return null;
            }

            var localVariables = symbolMethod.GetLocalVariables();
            var localNames = new string[localVariablesCount];
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

        private SymbolMethod? GetSymbolMethodDnlib(uint rowId, bool searchMoveNext)
        {
            var mdMethod = GetMethodDefDnlib(rowId);
            return searchMoveNext ? (GetSymbolMethodOfAsyncMethodDnlib(mdMethod) ?? DnlibPdbReader?.GetMethod(mdMethod, version: 1)) : DnlibPdbReader?.GetMethod(mdMethod, version: 1);
        }

        private SymbolMethod? GetSymbolMethodOfAsyncMethodDnlib(MethodDef? mdMethod)
        {
            // Determine if the given mdMethod is a kickoff of a state machine(aka an async method)
            // The first step is to check if the method is decorated with the attribute `AsyncStateMachine`
            var stateMachineAttributeOfMdMethod = mdMethod?.CustomAttributes?.FirstOrDefault(ca => ca.TypeFullName == AsyncStateMachineAttribute);
            // Grab the generated inner type state machine from the argument given by the compiler to the AsyncStateMachineAttribute
            var stateMachineInnerTypeTypeDefOrRefSig = (stateMachineAttributeOfMdMethod?.ConstructorArguments.FirstOrDefault().Value as TypeDefOrRefSig);
            // Grab the MoveNext from inside the stateMachine and from there the corresponding debugInfo
            var moveNextDebugInfo = stateMachineInnerTypeTypeDefOrRefSig?.TypeDefOrRef?.ResolveTypeDef()?.FindMethod("MoveNext")?.CustomDebugInfos.OfType<PdbAsyncMethodCustomDebugInfo>().FirstOrDefault();
            var breakpointMethod = moveNextDebugInfo?.StepInfos.FirstOrDefault();

            if (breakpointMethod?.BreakpointMethod == null)
            {
                return null;
            }

            return DnlibPdbReader?.GetMethod(breakpointMethod.Value.BreakpointMethod, version: 1);
        }

        private ImmutableArray<LocalScope>? GetLocalSymbolsDnlib(int rowId, VendoredMicrosoftCode.System.ReadOnlySpan<DatadogSequencePoint> sequencePoints, bool searchMoveNext)
        {
            ImmutableArray<LocalScope>.Builder? localScopes = null;
            var method = GetMethodDefDnlib((uint)rowId);
            if (method!.Body is not { Variables.Count: > 0 })
            {
                return null;
            }

            var methodLocals = method.Body.Variables;
            var allMethodScopes = GetAllScopes(method.MDToken.Rid, searchMoveNext);
            if (allMethodScopes == null)
            {
                return null;
            }

            localScopes = new ImmutableArray<LocalScope>.Builder();

            for (var k = 0; k < allMethodScopes.Count; k++)
            {
                var datadogScop = new LocalScope();
                var scopeLocals = new ImmutableArray<DatadogLocal>.Builder();
                var currentScope = allMethodScopes[k];
                DatadogSequencePoint sequencePointForScope = default;
                for (var l = 0; l < currentScope.Locals.Count; l++)
                {
                    var localSymbol = currentScope.Locals[l];
                    if (localSymbol.Index > methodLocals.Count || string.IsNullOrEmpty(localSymbol.Name))
                    {
                        continue;
                    }

                    if (sequencePointForScope == default)
                    {
                        for (var m = 0; m < sequencePoints.Length; m++)
                        {
                            if (sequencePoints[m].Offset >= currentScope.StartOffset)
                            {
                                sequencePointForScope = sequencePoints[m];
                                break;
                            }
                        }
                    }

                    Datadog.Trace.Vendors.dnlib.DotNet.Emit.Local? local = null;
                    for (var i = 0; i < methodLocals.Count; i++)
                    {
                        if (methodLocals[i].Index != localSymbol.Index)
                        {
                            continue;
                        }

                        local = methodLocals[i];
                        break;
                    }

                    if (local == null)
                    {
                        continue;
                    }

                    if (IsCompilerGeneratedAttributeDefinedOnType(local.Type.MDToken.ToInt32()))
                    {
                        continue;
                    }

                    datadogScop.SequencePoint = sequencePointForScope;
                    scopeLocals.Add(
                        new DatadogLocal
                        {
                            Name = localSymbol.Name,
                            Type = local.Type?.FullName ?? Unknown,
                            Line = sequencePointForScope.StartLine
                        });
                }

                datadogScop.Locals = scopeLocals.ToImmutable();
                localScopes.Add(datadogScop);
            }

            return localScopes.ToImmutable();
        }

        private CustomDebugInfoAsyncAndClosure GetAsyncAndClosureCustomDebugInfoDnlib(int methodRid)
        {
            CustomDebugInfoAsyncAndClosure cdiAsyncAndClosure = default;
            Datadog.Trace.Vendors.dnlib.DotNet.MethodDef? method = GetMethodDefDnlib((uint)methodRid);
            if (method is not { HasCustomDebugInfos: true })
            {
                return cdiAsyncAndClosure;
            }

            foreach (var methodCustomDebugInfo in method.CustomDebugInfos)
            {
                if (methodCustomDebugInfo.Kind is PdbCustomDebugInfoKind.EditAndContinueLocalSlotMap)
                {
                    cdiAsyncAndClosure.EncLambdaAndClosureMap = true;
                }
                else if (methodCustomDebugInfo.Kind is PdbCustomDebugInfoKind.StateMachineHoistedLocalScopes)
                {
                    cdiAsyncAndClosure.StateMachineHoistedLocal = true;
                    cdiAsyncAndClosure.StateMachineKickoffMethodRid = (int)((PdbAsyncMethodCustomDebugInfo)methodCustomDebugInfo).KickoffMethod.MDToken.Rid;
                }
            }

            return cdiAsyncAndClosure;
        }

        private int? GetContainingMethodTokenAndOffsetDnlib(string filePath, int line, int? column, out int? byteCodeOffset)
        {
            return DnlibPdbReader switch
            {
                PortablePdbReader portablePdbReader => portablePdbReader.GetContainingMethod(filePath, line, column, out byteCodeOffset)?.Token,
                PdbReader managedPdbReader => managedPdbReader.GetContainingMethod(filePath, line, column, out byteCodeOffset)?.Token,
                SymbolReaderImpl symUnmanagedReader => symUnmanagedReader.GetContainingMethod(filePath, line, column, out byteCodeOffset)?.Token,
                _ => throw new ArgumentOutOfRangeException(nameof(filePath), $"Reader type {DnlibPdbReader!.GetType().FullName} is not supported")
            };
        }

        private string? GetSourceLinkJsonDocumentDnlib()
        {
            var sourceLink = _dnlibModule?.CustomDebugInfos.OfType<PdbSourceLinkCustomDebugInfo>().FirstOrDefault();
            return sourceLink == null ? null : Encoding.UTF8.GetString(sourceLink.FileBlob);
        }

        private IMemoryOwner<DatadogSequencePoint>? GetMethodSequencePointsDnlib(int rowId, bool searchMoveNext, out int count)
        {
            count = 0;
            var symbolMethod = GetSymbolMethodDnlib((uint)rowId, searchMoveNext);
            if (symbolMethod == null)
            {
                return null;
            }

            var memory = ArrayMemoryPool<DatadogSequencePoint>.Shared.Rent(symbolMethod.SequencePoints.Count);
            var sequencePoints = memory.Memory.Span;

            for (int i = 0; i < symbolMethod.SequencePoints.Count; i++)
            {
                var sp = symbolMethod.SequencePoints[i];
                if (sp.IsHidden())
                {
                    continue;
                }

                sequencePoints[count] =
                    new DatadogSequencePoint
                    {
                        StartLine = sp.Line,
                        EndLine = sp.EndLine,
                        StartColumn = sp.Column,
                        EndColumn = sp.EndColumn,
                        Offset = sp.Offset,
                        IsHidden = sp.IsHidden(),
                        URL = sp.Document.URL
                    };
                count++;
            }

            return memory;
        }
    }
}
