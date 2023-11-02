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
using Datadog.Trace.VendoredMicrosoftCode.System;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;

namespace Datadog.Trace.Debugger.Symbols;

internal class SymbolPdbExtractor : SymbolExtractor
{
    private const string GeneratedClassPrefix = "<>";
    private const int UnknownLocalLine = int.MaxValue;

    internal SymbolPdbExtractor(DatadogMetadataReader pdbReader, string assemblyName)
        : base(pdbReader, assemblyName)
    {
    }

    protected override Model.Scope CreateMethodScope(TypeDefinition type, MethodDefinition method)
    {
        var methodScope = base.CreateMethodScope(type, method);
        var sequencePoints = DatadogMetadataReader.GetMethodSequencePoints(method.Handle.RowId);
        if (sequencePoints == null || sequencePoints.Count == 0)
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
        var localsSymbol = GetLocalSymbols(method.Handle.RowId, sequencePoints);

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

    protected override Model.Scope? CreateMethodScopeForGeneratedMethod(MethodDefinition method, MethodDefinition generatedMethod, TypeDefinition nestedType)
    {
        if (method.Name.IsNil || generatedMethod.Name.IsNil)
        {
            return null;
        }

        var cdi = DatadogMetadataReader.GetAsyncAndClosureCustomDebugInfo(generatedMethod.Handle.RowId);
        if (cdi.IsNil)
        {
            return null;
        }

        string? methodName;
        if (!cdi.StateMachineHoistedLocal)
        {
            var generatedMethodName = MetadataReader.GetString(generatedMethod.Name);
            if (generatedMethodName[0] != '<')
            {
                return null;
            }

            var notGeneratedMethodName = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(generatedMethodName, 1, generatedMethodName.IndexOf('>') - 1);
            methodName = MetadataReader.GetString(method.Name);
            if (!methodName.Equals(notGeneratedMethodName.ToString()))
            {
                return null;
            }
        }
        else if (method.Handle.RowId == cdi.StateMachineKickoffMethodRid)
        {
            var kickoffDef = MetadataReader.GetMethodDefinition(MethodDefinitionHandle.FromRowId(cdi.StateMachineKickoffMethodRid));
            methodName = MetadataReader.GetString(kickoffDef.Name);
        }
        else
        {
            return null;
        }

        var closureMethodScope = CreateMethodScope(nestedType, generatedMethod);
        closureMethodScope.Name = methodName;
        closureMethodScope.ScopeType = SymbolType.Closure;
        return closureMethodScope;
    }

    private Symbol[]? GetLocalSymbols(int rowId, List<DatadogMetadataReader.DatadogSequencePoint> sequencePoints)
    {
        List<Symbol>? localsSymbol = null;
        var generatedClassPrefix = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(GeneratedClassPrefix);

        if (DatadogMetadataReader.GetAsyncAndClosureCustomDebugInfo(rowId).StateMachineHoistedLocal && DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnType(MetadataReader.GetMethodDefinition(MethodDefinitionHandle.FromRowId(rowId)).GetDeclaringType().RowId))
        {
            localsSymbol = new List<Symbol>();
            var fields = MetadataReader.GetTypeDefinition(MetadataReader.GetMethodDefinition(MethodDefinitionHandle.FromRowId(rowId)).GetDeclaringType()).GetFields();
            foreach (var fieldHandle in fields)
            {
                var field = MetadataReader.GetFieldDefinition(fieldHandle);
                var fieldName = MetadataReader.GetString(field.Name);
                var span = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(fieldName);
                if (Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.IndexOf(span, generatedClassPrefix, StringComparison.Ordinal) == 0)
                {
                    continue;
                }

                var localName = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(MetadataReader.GetString(field.Name));
                if (localName[0] == '<')
                {
                    var endNameIndex = localName.IndexOf('>');
                    if (endNameIndex > 1)
                    {
                        localName = localName.Slice(1, endNameIndex - 1);
                    }
                }

                localsSymbol.Add(new Symbol
                {
                    Name = localName.ToString(),
                    Type = field.DecodeSignature(new TypeProvider(false), 0),
                    SymbolType = SymbolType.Local,
                    Line = UnknownLocalLine // todo: get the correct line
                });
            }
        }

        var locals = DatadogMetadataReader.GetLocalSymbols(rowId, sequencePoints);
        if (locals is { Count: > 0 })
        {
            localsSymbol ??= new List<Symbol>();
            foreach (var local in locals)
            {
                var nameAsSpan = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(local.Name);
                if (Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.IndexOf(nameAsSpan, generatedClassPrefix, StringComparison.Ordinal) > 0)
                {
                    var cdi = DatadogMetadataReader.GetAsyncAndClosureCustomDebugInfo(rowId);
                    if (cdi.EncLambdaAndClosureMap || cdi.LocalSlot)
                    {
                        var nestedTypes = MetadataReader.GetTypeDefinition(MetadataReader.GetMethodDefinition(MethodDefinitionHandle.FromRowId(rowId)).GetDeclaringType()).GetNestedTypes();
                        for (int i = 0; i < nestedTypes.Length; i++)
                        {
                            var nestedHandle = nestedTypes[i];
                            if (nestedHandle.IsNil)
                            {
                                continue;
                            }

                            var name = nestedHandle.FullName(MetadataReader);
                            if (!Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(local.Type).SequenceEqual(
                                Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(name)))
                            {
                                continue;
                            }

                            var nestedType = MetadataReader.GetTypeDefinition(nestedHandle);
                            var fields = nestedType.GetFields();
                            int index = 0;
                            var added = ArrayPool<string>.Shared.Rent(fields.Count);
                            foreach (var fieldHandle in fields)
                            {
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
                                var fieldNameAsSpan = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(fieldName);
                                if (Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.IndexOf(fieldNameAsSpan, generatedClassPrefix, StringComparison.Ordinal) == 0)
                                {
                                    continue;
                                }

                                bool contains = false;
                                for (int j = 0; j < added.Length; j++)
                                {
                                    if (added[j] == fieldName)
                                    {
                                        contains = true;
                                        break;
                                    }
                                }

                                if (contains)
                                {
                                    continue;
                                }

                                added[index] = fieldName;
                                localsSymbol.Add(new Symbol
                                {
                                    Name = fieldName,
                                    Type = field.DecodeSignature(new TypeProvider(false), 0),
                                    SymbolType = SymbolType.Local,
                                    Line = local.Line
                                });
                                index++;
                            }

                            ArrayPool<string>.Shared.Return(added, true);
                        }
                    }
                }
                else
                {
                    localsSymbol.Add(new Symbol
                    {
                        Name = local.Name,
                        Type = local.Type,
                        SymbolType = SymbolType.Local,
                        Line = local.Line
                    });
                }
            }
        }

        return localsSymbol?.ToArray();
    }
}
