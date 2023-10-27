// <copyright file="SymbolPdbExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Linq;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Pdb;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
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
        var localsSymbol = GetLocalsSymbol(method, startLine, debugInfo, sequencePoints, out var localsCount);

        methodScope.Symbols = ConcatMethodSymbols(methodScope.Symbols?.ToArray() ?? null, localsSymbol, localsCount);
        methodScope.StartLine = startLine;
        methodScope.EndLine = endLine;
        methodScope.SourceFile = typeSourceFile;
        if (startColumn >= 0 && endColumn >= 0)
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

    private Symbol[]? GetLocalsSymbol(MethodDefinition method, int startLine, DatadogMetadataReader.CustomDebugInfoAsyncAndClosure debugInfo, DatadogMetadataReader.DatadogSequencePoint[] sequencePoints, out int localsCount)
    {
        localsCount = 0;

        if (DatadogMetadataReader.PdbReader == null)
        {
            return null;
        }

        var methodLocalsCount = DatadogMetadataReader.GetLocalVariablesCount(method);
        if (methodLocalsCount == 0)
        {
            return null;
        }

        var localsSymbol = ArrayPool<Symbol>.Shared.Rent(methodLocalsCount);
        var signature = DatadogMetadataReader.GetLocalSignature(method);
        if (signature == null)
        {
            return null;
        }

        var localTypes = signature.Value.DecodeLocalSignature(new TypeProvider(), 0);

        Symbol[]? allLocals = null;
        foreach (var scopeHandle in DatadogMetadataReader.PdbReader.GetLocalScopes(method.Handle.ToDebugInformationHandle()))
        {
            var localScope = DatadogMetadataReader.PdbReader.GetLocalScope(scopeHandle);
            foreach (var localVarHandle in localScope.GetLocalVariables())
            {
                var local = DatadogMetadataReader.PdbReader.GetLocalVariable(localVarHandle);
                if (local.Index > methodLocalsCount || string.IsNullOrEmpty(DatadogMetadataReader.PdbReader.GetString(local.Name)))
                {
                    continue;
                }

                var line = UnknownEndLineEntireScope;
                foreach (var sequencePoint in sequencePoints)
                {
                    if (sequencePoint.Offset >= localScope.StartOffset)
                    {
                        line = sequencePoint.StartLine;
                        break;
                    }
                }

                /*if (IsCompilerGeneratedAttributeDefined())
                {
                    continue;
                }*/

                localsSymbol[localsCount] = new Symbol
                {
                    Name = DatadogMetadataReader.PdbReader.GetString(local.Name),
                    Type = localTypes[local.Index].Name,
                    SymbolType = SymbolType.Local,
                    Line = line
                };

                localsCount++;
            }
        }

        if (debugInfo.StateMachineHoistedLocal && IsCompilerGeneratedAttributeDefined(MetadataReader.GetTypeDefinition(method.GetDeclaringType()).GetCustomAttributes()))
        {
            var fields = MetadataReader.GetTypeDefinition(method.GetDeclaringType()).GetFields();
            int index = 0;
            foreach (var fieldHandle in fields)
            {
                var field = MetadataReader.GetFieldDefinition(fieldHandle);
                if (MetadataReader.GetString(field.Name).StartsWith("<>"))
                {
                    continue;
                }

                if (allLocals == null)
                {
                    allLocals = new Symbol[localsCount + fields.Count - index];
                    Array.Copy(localsSymbol, 0, allLocals, 0, localsCount);
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

                allLocals[localsCount] = new Symbol
                {
                    Name = localName,
                    Type = field.DecodeSignature(new TypeProvider(), 0).Name,
                    SymbolType = SymbolType.Local,
                    Line = startLine
                };

                index++;
                localsCount++;
            }
        }

        return allLocals ?? localsSymbol;
    }
}
