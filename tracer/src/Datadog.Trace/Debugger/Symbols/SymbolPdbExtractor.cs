// <copyright file="SymbolPdbExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Linq;
using Datadog.System.Buffers;
using Datadog.System.Reflection.Metadata;
using Datadog.System.Reflection.Metadata.Ecma335;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;

namespace Datadog.Trace.Debugger.Symbols;

internal class SymbolPdbExtractor : SymbolExtractor
{
    private readonly DatadogPdbReader _pdbReader;

    private bool _disposed;

    internal SymbolPdbExtractor(DatadogPdbReader pdbReader)
        : base(pdbReader.MetadataReader)
    {
        _pdbReader = pdbReader;
    }

    protected override Model.Scope CreateMethodScope(TypeDefinition type, MethodDefinition method)
    {
        var methodScope = base.CreateMethodScope(type, method);
        if (_pdbReader.GetMethodSymbolInfoOrDefault(method.Handle.RowId) is not { } symbolMethod)
        {
            return methodScope;
        }

        var firstSq = symbolMethod.SequencePoints.FirstOrDefault(sq => sq.IsHidden() == false && sq.Line > 0);
        var startLine = firstSq.Line == 0 ? UnknownStartLine : firstSq.Line;
        var typeSourceFile = firstSq.Document?.URL;
        var lastSq = symbolMethod.SequencePoints.LastOrDefault(sq => sq.IsHidden() == false && sq.EndLine > 0);
        var endLine = lastSq.EndLine == 0 ? UnknownEndLine : lastSq.EndLine;
        var startColumn = firstSq.Column == 0 ? UnknownStartLine : firstSq.Column;
        var endColumn = lastSq.EndColumn == 0 ? UnknownEndLine : lastSq.EndColumn;

        // locals
        var localsSymbol = GetLocalsSymbol(method, startLine, symbolMethod, out var localsCount);

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

    private StandaloneSignature GetLocalSignature(MethodDefinition method)
    {
        var methodBodyBlock = _pdbReader.PEReader?.GetMethodBody(method.RelativeVirtualAddress);
        return MetadataReader.GetStandaloneSignature(methodBodyBlock!.LocalSignature);
    }

    private int GetLocalVariablesCount(MethodDefinition method)
    {
        var signature = GetLocalSignature(method);
        BlobReader blobReader = MetadataReader.GetBlobReader(signature.Signature);

        if (blobReader.ReadByte() == (byte)SignatureKind.LocalVariables)
        {
            int variableCount = blobReader.ReadCompressedInteger();
            return variableCount;
        }

        return 0;
    }

    private Symbol[]? GetLocalsSymbol(MethodDefinition method, int startLine, SymbolMethod symbolMethod, out int localsCount)
    {
        localsCount = 0;
        var methodLocalsCount = GetLocalVariablesCount(method);
        if (methodLocalsCount == 0)
        {
            return null;
        }

        var localsSymbol = ArrayPool<Symbol>.Shared.Rent(methodLocalsCount);
        var signature = GetLocalSignature(method);
        var localTypes = signature.DecodeLocalSignature(new TypeProvider(), 0);

        Symbol[]? allLocals = null;
        foreach (var scopeHandle in MetadataReader.GetLocalScopes(method.Handle.ToDebugInformationHandle()))
        {
            var localScope = MetadataReader.GetLocalScope(scopeHandle);
            foreach (var localVarHandle in localScope.GetLocalVariables())
            {
                var local = MetadataReader.GetLocalVariable(localVarHandle);
                if (local.Index > methodLocalsCount || string.IsNullOrEmpty(MetadataReader.GetString(local.Name)))
                {
                    continue;
                }

                var line = UnknownEndLineEntireScope;
                foreach (var sequencePoint in MetadataReader.GetMethodDebugInformation(method.Handle.ToDebugInformationHandle()).GetSequencePoints())
                {
                    if (sequencePoint.Offset >= localScope.StartOffset)
                    {
                        line = sequencePoint.StartLine;
                        break;
                    }
                }

                // if (IsCompilerGeneratedAttributeDefined(local.Type.ToTypeDefOrRef().CustomAttributes))
                // {
                // continue;
                // }

                localsSymbol[localsCount] = new Symbol
                {
                    Name = MetadataReader.GetString(local.Name),
                    Type = localTypes[local.Index].Name,
                    SymbolType = SymbolType.Local,
                    Line = line
                };

                localsCount++;
            }
        }

        if (IsCompilerGeneratedAttributeDefined(MetadataReader.GetTypeDefinition(method.GetDeclaringType()).GetCustomAttributes()))
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

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _pdbReader?.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
