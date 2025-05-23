// <copyright file="SymbolPdbExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Pdb;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata; // keep vendored versions for now because we access internal members

#if NETCOREAPP3_1_OR_GREATER
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#else
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata.Ecma335;
using Datadog.Trace.VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe;
using Datadog.Trace.VendoredMicrosoftCode.System.Runtime.InteropServices;
#endif

namespace Datadog.Trace.Debugger.Symbols;

internal class SymbolPdbExtractor : SymbolExtractor
{
    private const string GeneratedClassPrefix = "<>";

    internal SymbolPdbExtractor(DatadogMetadataReader pdbReader, string assemblyName)
        : base(pdbReader, assemblyName)
    {
    }

    protected override bool TryCreateMethodScope(TypeDefinition type, MethodDefinition method, out Model.Scope methodScope)
    {
        if (!base.TryCreateMethodScope(type, method, out methodScope))
        {
            return false;
        }

        using var memory = DatadogMetadataReader.GetMethodSequencePointsAsMemoryOwner(MetadataTokens.GetToken(method.Handle), false, out var count);
        if (memory == null || count == 0)
        {
            return true;
        }

        ReadOnlySpan<DatadogMetadataReader.DatadogSequencePoint> sequencePoints = memory.Memory.Span.Slice(0, count);
        var sourcePdbInfo = GetSourceLocationInfo(sequencePoints);
        methodScope.StartLine = sourcePdbInfo.StartLine;
        methodScope.EndLine = sourcePdbInfo.EndLine;
        methodScope.SourceFile = sourcePdbInfo.Path;
        if (sourcePdbInfo.EndColumn > 0)
        {
            var ls = new LanguageSpecifics
            {
                Annotations = methodScope.LanguageSpecifics?.Annotations,
                AccessModifiers = methodScope.LanguageSpecifics?.AccessModifiers,
                ReturnType = methodScope.LanguageSpecifics?.ReturnType,
                StartColumn = sourcePdbInfo.StartColumn,
                EndColumn = sourcePdbInfo.EndColumn,
            };

            methodScope.LanguageSpecifics = ls;
        }

        var localScopes = GetLocalSymbols(method, sequencePoints, methodScope);
        methodScope.Scopes = ConcatMethodScopes(methodScope.Scopes ?? null, localScopes);
        return true;
    }

    private SourceLocationInfo GetSourceLocationInfo(ReadOnlySpan<DatadogMetadataReader.DatadogSequencePoint> span)
    {
        ref var firstSq = ref MemoryMarshal.GetReference(span);
        var startLine = firstSq.StartLine == 0 ? UnknownMethodStartLine : firstSq.StartLine;
        var lastSq = Unsafe.Add(ref firstSq, span.Length - 1);
        var endLine = lastSq.EndLine == 0 ? UnknownMethodEndLine : lastSq.EndLine;
        var typeSourceFile = firstSq.URL;
        int startColumn = firstSq.StartColumn;
        int endColumn = 0;

        for (int i = 0; i < span.Length; i++)
        {
            var current = Unsafe.Add(ref firstSq, i);

            if (endColumn < current.EndColumn)
            {
                endColumn = current.EndColumn;
            }
        }

        if (endColumn == 0)
        {
            endColumn = int.MaxValue;
        }

        return new SourceLocationInfo(startLine: startLine, endLine: endLine, startColumn: startColumn, endColumn: endColumn, path: typeSourceFile);
    }

    protected override bool TryCreateMethodScopeForGeneratedMethod(MethodDefinition method, MethodDefinition generatedMethod, TypeDefinition nestedType, out Model.Scope closureMethodScope)
    {
        closureMethodScope = default;
        if (method.Name.IsNil || generatedMethod.Name.IsNil)
        {
            return false;
        }

        var methodToken = MetadataTokens.GetToken(generatedMethod.Handle);
        if (!DatadogMetadataReader.HasSequencePoints(methodToken))
        {
            return false;
        }

        var cdi = DatadogMetadataReader.GetAsyncAndClosureCustomDebugInfo(methodToken);
        string? methodName;
        if (cdi.StateMachineHoistedLocal && MetadataTokens.GetToken(method.Handle) == cdi.StateMachineKickoffMethodToken)
        {
            var kickoffDef = DatadogMetadataReader.GetMethodDef(cdi.StateMachineKickoffMethodToken);
            methodName = MetadataReader.GetString(kickoffDef.Name);
        }
        else
        {
            var generatedMethodName = MetadataReader.GetString(generatedMethod.Name);
            if (generatedMethodName[0] != '<')
            {
                return false;
            }

            var notGeneratedMethodName = generatedMethodName.AsSpan(1, generatedMethodName.IndexOf('>') - 1);
            methodName = MetadataReader.GetString(method.Name);
            if (!methodName.Equals(notGeneratedMethodName.ToString()))
            {
                return false;
            }
        }

        if (string.IsNullOrEmpty(methodName))
        {
            return false;
        }

        if (!TryCreateMethodScope(nestedType, generatedMethod, out closureMethodScope))
        {
            return false;
        }

        closureMethodScope.Name = methodName;
        closureMethodScope.ScopeType = ScopeType.Closure;
        return true;
    }

    private Model.Scope[]? GetLocalSymbols(int rowId, VendoredMicrosoftCode.System.ReadOnlySpan<DatadogMetadataReader.DatadogSequencePoint> sequencePoints, Model.Scope methodScope)
    {
        List<Model.Scope>? scopes = null;
        var generatedClassPrefix = GeneratedClassPrefix.AsSpan();

        var methodToken = MetadataTokens.GetToken(methodDefinition.Handle);
        if (DatadogMetadataReader.GetAsyncAndClosureCustomDebugInfo(methodToken).StateMachineHoistedLocal
         && DatadogMetadataReader.IsCompilerGeneratedAttributeDefinedOnType(MetadataTokens.GetToken(methodDefinition.GetDeclaringType())))
        {
            scopes = new List<Model.Scope>();
            var scope = new Model.Scope();
            using var localsMemory = MemoryPool<Model.Symbol>.Shared.Rent();
            var localIndex = 0;
            var localSymbols = localsMemory.Memory.Span;
            var fields = MetadataReader.GetTypeDefinition(methodDefinition.GetDeclaringType()).GetFields();
            foreach (var fieldHandle in fields)
            {
                var field = MetadataReader.GetFieldDefinition(fieldHandle);
                if (field.Name.IsNil)
                {
                    continue;
                }

                var fieldName = MetadataReader.GetString(field.Name);
                var span = fieldName.AsSpan();
                if (span.IndexOf(generatedClassPrefix, StringComparison.Ordinal) == 0)
                {
                    continue;
                }

                var type = field.DecodeSignature(new TypeProvider(false), 0);
                if (string.IsNullOrEmpty(type))
                {
                    continue;
                }

                var localName = MetadataReader.GetString(field.Name).AsSpan();
                if (localName.IsEmpty || localName[0] != '<')
                {
                    continue;
                }

                var endNameIndex = localName.IndexOf('>');
                if (endNameIndex > 1)
                {
                    localName = localName.Slice(1, endNameIndex - 1);
                }

                var name = localName.ToString();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (IsArgument(methodScope.Symbols, name, type))
                {
                    continue;
                }

                localSymbols[localIndex] = new Symbol
                {
                    Name = name,
                    Type = type,
                    SymbolType = SymbolType.Local,
                    Line = 0, // they are actually fields
                };
                localIndex++;
            }

            scope.Symbols = localSymbols.Slice(0, localIndex).ToArray();
            scope.ScopeType = ScopeType.Local;
            scope.StartLine = methodScope.StartLine;
            scope.EndLine = methodScope.EndLine;
            scope.SourceFile = methodScope.SourceFile;
            if (methodScope.LanguageSpecifics.HasValue)
            {
                var ls = new LanguageSpecifics { StartColumn = methodScope.LanguageSpecifics.Value.StartColumn, EndColumn = methodScope.LanguageSpecifics.Value.EndColumn };
                scope.LanguageSpecifics = ls;
            }

            scopes.Add(scope);
        }

        var localScopes = DatadogMetadataReader.GetLocalSymbols(methodToken, sequencePoints, false);
        if (localScopes is not { Length: > 0 })
        {
            return scopes?.ToArray();
        }

        scopes ??= new List<Model.Scope>(localScopes.Value.Length);
        foreach (var localScope in localScopes)
        {
            var scope = new Model.Scope();
            using var localsMemory = MemoryPool<Model.Symbol>.Shared.Rent();
            var localSymbols = localsMemory.Memory.Span;
            int localIndex = 0;
            foreach (var local in localScope.Locals)
            {
                var nameAsSpan = local.Name.AsSpan();
                if (nameAsSpan.IsEmpty)
                {
                    continue;
                }

                if (nameAsSpan.IndexOf(generatedClassPrefix, StringComparison.Ordinal) > 0)
                {
                    var cdi = DatadogMetadataReader.GetAsyncAndClosureCustomDebugInfo(methodToken);
                    if (cdi.EncLambdaAndClosureMap || cdi.LocalSlot)
                    {
                        var nestedTypes = MetadataReader.GetTypeDefinition(methodDefinition.GetDeclaringType()).GetNestedTypes();
                        for (int i = 0; i < nestedTypes.Length; i++)
                        {
                            var nestedHandle = nestedTypes[i];
                            if (nestedHandle.IsNil)
                            {
                                continue;
                            }

                            var name = nestedHandle.FullName(MetadataReader);
                            if (local.Type != name)
                            {
                                continue;
                            }

                            var nestedType = MetadataReader.GetTypeDefinition(nestedHandle);
                            var fields = nestedType.GetFields();
                            int addedIndex = 0;
                            using var addedMemory = MemoryPool<string?>.Shared.Rent(fields.Count);
                            var added = addedMemory.Memory.Span;

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
                                if (fieldName.AsSpan().IndexOf(generatedClassPrefix, StringComparison.Ordinal) == 0)
                                {
                                    continue;
                                }

                                var type = field.DecodeSignature(new TypeProvider(false), 0);
                                if (string.IsNullOrEmpty(type))
                                {
                                    continue;
                                }

                                if (IsArgument(methodScope.Symbols, fieldName, type))
                                {
                                    continue;
                                }

                                bool contains = false;
                                for (int j = 0; j < added.Length; j++)
                                {
                                    if (added[j] == null)
                                    {
                                        break;
                                    }

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

                                added[addedIndex] = fieldName;
                                localSymbols[localIndex] = new Symbol
                                {
                                    Name = fieldName,
                                    Type = type,
                                    SymbolType = SymbolType.Local,
                                    Line = local.Line
                                };
                                addedIndex++;
                                localIndex++;
                            }
                        }
                    }
                }
                else
                {
                    localSymbols[localIndex] = new Symbol
                    {
                        Name = local.Name,
                        Type = local.Type,
                        SymbolType = SymbolType.Local,
                        Line = local.Line
                    };
                    localIndex++;
                }
            }

            scope.Symbols = localSymbols.Slice(0, localIndex).ToArray();
            scope.ScopeType = ScopeType.Local;
            scope.StartLine = methodScope.StartLine;
            scope.EndLine = methodScope.EndLine;
            scope.SourceFile = methodScope.SourceFile;
            if (methodScope.LanguageSpecifics.HasValue)
            {
                var ls = new LanguageSpecifics { StartColumn = methodScope.LanguageSpecifics.Value.StartColumn, EndColumn = methodScope.LanguageSpecifics.Value.EndColumn };
                scope.LanguageSpecifics = ls;
            }

            scopes.Add(scope);
        }

        return scopes?.ToArray();
    }

    private Model.Scope[]? ConcatMethodScopes(Model.Scope[]? oldScopes, Model.Scope[]? localScopes)
    {
        var localScopesLength = localScopes?.Length ?? 0;
        var oldScopesLength = oldScopes?.Length ?? 0;
        if (oldScopesLength == 0)
        {
            return localScopes;
        }

        var scopesLength = oldScopesLength + localScopesLength;
        if (scopesLength == 0)
        {
            return null;
        }

        using var memory = MemoryPool<Model.Scope>.Shared.Rent(scopesLength);
        var scopes = memory.Memory.Span;
        oldScopes!.CopyTo(scopes);
        var localScopesSlice = scopes.Slice(oldScopesLength);
        localScopes!.CopyTo(localScopesSlice);
        return scopes.Slice(0, scopesLength).ToArray();
    }

    private bool IsArgument(IReadOnlyList<Symbol>? args, string name, string type)
    {
        if (args == null)
        {
            return false;
        }

        for (int i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.Name == name && arg.Type == type)
            {
                return true;
            }
        }

        return false;
    }
}
