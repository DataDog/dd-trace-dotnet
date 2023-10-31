// <copyright file="SymbolPdbExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Pdb;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;

namespace Datadog.Trace.Debugger.Symbols;

internal class SymbolPdbExtractor : SymbolExtractor
{
    internal SymbolPdbExtractor(DatadogMetadataReader pdbReader, string assemblyName)
        : base(pdbReader, assemblyName)
    {
    }

    protected override Model.Scope CreateMethodScope(TypeDefinition type, MethodDefinition method, DatadogMetadataReader.CustomDebugInfoAsyncAndClosure debugInfo)
    {
        var methodScope = base.CreateMethodScope(type, method, debugInfo);
        var sequencePoints = DatadogMetadataReader.GetMethodSequencePoints(method.Handle.RowId);
        if (sequencePoints.Length == 0)
        {
            return methodScope;
        }

        var firstSq = sequencePoints.FirstOrDefault(sq => sq is { IsHidden: false, StartLine: > 0 });
        var startLine = firstSq.StartLine == 0 ? UnknownMethodStartLine : firstSq.StartLine;
        var typeSourceFile = firstSq.URL;
        var lastSq = sequencePoints.LastOrDefault(sq => sq is { IsHidden: false, EndLine: > 0 });
        var endLine = lastSq.EndLine == 0 ? UnknownMethodEndLine : lastSq.EndLine;
        var startColumn = firstSq.StartColumn == 0 ? UnknownMethodStartLine : firstSq.StartColumn;
        var endColumn = lastSq.EndColumn == 0 ? UnknownMethodEndLine : lastSq.EndColumn;

        // locals
        var localsSymbol = GetLocalsSymbol(method, startLine, debugInfo, sequencePoints);

        methodScope.Symbols = ConcatMethodSymbols(methodScope.Symbols?.ToArray() ?? null, localsSymbol);
        methodScope.StartLine = startLine;
        methodScope.EndLine = endLine;
        methodScope.SourceFile = typeSourceFile;
        if (endColumn > 0)
        {
            var ls = new LanguageSpecifics
            {
                Annotations = methodScope.LanguageSpecifics?.Annotations,
                AccessModifiers = methodScope.LanguageSpecifics?.AccessModifiers,
                ReturnType = methodScope.LanguageSpecifics?.ReturnType,
                StartColumn = startColumn,
                EndColumn = endColumn,
            };

            methodScope.LanguageSpecifics = ls;
        }

        return methodScope;
    }

    // TODO: this should be wrapped in DatadogMetadataReader
    private Symbol[]? GetLocalsSymbol(MethodDefinition method, int startLine, DatadogMetadataReader.CustomDebugInfoAsyncAndClosure debugInfo, DatadogMetadataReader.DatadogSequencePoint[] sequencePoints)
    {
        if (DatadogMetadataReader.PdbReader == null)
        {
            return null;
        }

        var methodLocalsCount = DatadogMetadataReader.GetLocalVariablesCount(method);
        if (methodLocalsCount == 0)
        {
            return null;
        }

        var signature = DatadogMetadataReader.GetLocalSignature(method);
        if (signature == null)
        {
            return null;
        }

        var generatedClassPrefix = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan("<>");
        var localTypes = signature.Value.DecodeLocalSignature(new TypeProvider(), 0);
        var localsSymbol = new List<Symbol>();
        Dictionary<string, int>? hoistedLocals = null;

        foreach (var scopeHandle in DatadogMetadataReader.PdbReader.GetLocalScopes(method.Handle.ToDebugInformationHandle()))
        {
            var localScope = DatadogMetadataReader.PdbReader.GetLocalScope(scopeHandle);
            foreach (var localVarHandle in localScope.GetLocalVariables())
            {
                if (localVarHandle.IsNil)
                {
                    continue;
                }

                var local = DatadogMetadataReader.PdbReader.GetLocalVariable(localVarHandle);
                if (local.Name.IsNil)
                {
                    continue;
                }

                var localName = DatadogMetadataReader.PdbReader.GetString(local.Name);
                if (local.Index > methodLocalsCount ||
                    string.IsNullOrEmpty(localName))
                {
                    continue;
                }

                var span = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(localTypes[local.Index].Name);
                if (Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.Contains(span, generatedClassPrefix, StringComparison.Ordinal))
                {
                    var cdi = DatadogMetadataReader.GetAsyncAndClosureCustomDebugInfo(method.Handle);
                    if (cdi.EncLambdaAndClosureMap || cdi.LocalSlot)
                    {
                        hoistedLocals ??= new Dictionary<string, int>();
                        if (hoistedLocals.TryGetValue(localTypes[local.Index].Name, out var count))
                        {
                            hoistedLocals[localTypes[local.Index].Name] = count + 1;
                        }
                        else
                        {
                            hoistedLocals[localTypes[local.Index].Name] = 1;
                        }
                    }

                    continue;
                }

                var line = UnknownLocalLine;
                foreach (var sequencePoint in sequencePoints)
                {
                    if (sequencePoint.Offset >= localScope.StartOffset)
                    {
                        line = sequencePoint.StartLine;
                        break;
                    }
                }

                localsSymbol.Add(new Symbol
                {
                    Name = DatadogMetadataReader.PdbReader.GetString(local.Name),
                    Type = localTypes[local.Index].Name,
                    SymbolType = SymbolType.Local,
                    Line = line
                });
            }
        }

        if (hoistedLocals != null)
        {
            foreach (var hoistedLocal in hoistedLocals)
            {
                var nestedTypes = MetadataReader.GetTypeDefinition(method.GetDeclaringType()).GetNestedTypes();
                for (int i = 0; i < nestedTypes.Length; i++)
                {
                    var nestedHandle = nestedTypes[i];
                    if (nestedHandle.IsNil)
                    {
                        continue;
                    }

                    var nestedType = MetadataReader.GetTypeDefinition(nestedHandle);
                    var name = nestedType.FullName(MetadataReader);
                    // TODO: related task - normalize type names
                    if (!hoistedLocal.Key.Contains(name))
                    {
                        continue;
                    }

                    var fields = nestedType.GetFields();
                    int index = 0;
                    foreach (var fieldHandle in fields)
                    {
                        if (index >= hoistedLocal.Value)
                        {
                            break;
                        }

                        if (fieldHandle.IsNil)
                        {
                            continue;
                        }

                        var field = MetadataReader.GetFieldDefinition(fieldHandle);
                        if (field.Name.IsNil)
                        {
                            continue;
                        }

                        var fieldName = MetadataReader.GetString(field.Name);
                        var span = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(fieldName);
                        if (Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.StartsWith(span, generatedClassPrefix, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        localsSymbol.Add(new Symbol
                        {
                            Name = fieldName,
                            Type = field.DecodeSignature(new TypeProvider(), 0).Name,
                            SymbolType = SymbolType.Local,
                            Line = startLine
                        });
                        index++;
                    }
                }
            }
        }

        if (debugInfo.StateMachineHoistedLocal && IsCompilerGeneratedAttributeDefined(MetadataReader.GetTypeDefinition(method.GetDeclaringType()).GetCustomAttributes()))
        {
            var fields = MetadataReader.GetTypeDefinition(method.GetDeclaringType()).GetFields();
            foreach (var fieldHandle in fields)
            {
                var field = MetadataReader.GetFieldDefinition(fieldHandle);
                var fieldName = MetadataReader.GetString(field.Name);
                var span = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(fieldName);
                if (Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.StartsWith(span, generatedClassPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var localName = MetadataReader.GetString(field.Name);
                if (localName[0] == '<')
                {
                    var endNameIndex = localName.IndexOf('>');
                    if (endNameIndex > 1)
                    {
                        localName = localName.Substring(1, endNameIndex - 1);
                    }
                }

                localsSymbol.Add(new Symbol
                {
                    Name = localName,
                    Type = field.DecodeSignature(new TypeProvider(), 0).Name,
                    SymbolType = SymbolType.Local,
                    Line = startLine
                });
            }
        }

        return localsSymbol.ToArray();
    }
}
