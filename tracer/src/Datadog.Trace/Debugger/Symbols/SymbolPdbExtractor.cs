// <copyright file="SymbolPdbExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Linq;
using Datadog.System.Reflection.Metadata;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet.Emit;
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

    public void GetLocalVariables(MethodDefinition method)
    {
        var methodBodyBlock = _pdbReader.PEReader?.GetMethodBody(method.RelativeVirtualAddress);
        var methodDebugInfo = _pdbReader.MetadataReader.GetMethodDebugInformation(method.Handle);
        foreach (var localScope in MetadataReader.GetLocalScopes(method.Handle))
        {
            MetadataReader.GetLocalVariableRange(localScope, out int first, out int last);
            foreach (var localVariable in MetadataReader.LocalVariables)
            {
                // localVariable.RowId
            }

            // MetadataReader.LocalVariableTable.GetName(MetadataReader.LocalVariables)
        }

        MetadataReader.GetLocalScopes(method.Handle);
        foreach (var sequencePoint in methodDebugInfo.GetSequencePoints())
        {
            // var localScopes = _pdbReader.MetadataReader.GetLocalScopes(methodDebugInfo.);
            // foreach (var localScope in localScopes)
            // {
            // _pdbReader.PEReader.GetSectionData()
            // foreach (var localVariableHandle in localScope..GetLocalVariables())
            // {
            // var localVariable = pdbReader.GetLocalVariable(localVariableHandle);
            // Console.WriteLine($"Index: {localVariable.Index}, Name: {pdbReader.GetString(localVariable.Name)}");
            // }
            // }
        }

        // var localVarSig = MetadataReader.GetLocalVariable(methodBodyBlock.LocalSignature);
        // MethodBodyBlock methodBody = MetadataReader.MethodDefTable..GetMethodImplementation(method.Handle).MethodBody;
        // StandAloneSignature signature = MetadataReader.GetStandaloneSignature(methodBody.LocalSignature);

        // BlobReader blobReader = MetadataReader.GetBlobReader(signature.Signature);

        // if (blobReader.ReadByte() == (byte)SignatureKind.LocalVariables)
        // {
        // int variableCount = blobReader.ReadCompressedInteger();

        // for (int i = 0; i < variableCount; i++)
        // {
        // SignatureTypeCode typeCode = (SignatureTypeCode)blobReader.ReadCompressedInteger();

        // For simple types, the type code maps directly to the variable type.
        // For other types, more detailed parsing will be necessary.
        // }
        // }
    }

    private Symbol[]? GetLocalsSymbol(MethodDefinition method, int startLine, SymbolMethod symbolMethod, out int localsCount)
    {
        localsCount = 0;
        var methodBody = _pdbReader.PEReader?.GetMethodBody(method.RelativeVirtualAddress);
        // if (method.Body is not { Variables.Count: > 0 })
        // {
        // return null;
        // }

        var methodLocals = new int[1]; // methodBody.Variables;
        var localsSymbol = new Symbol[methodLocals.Length];
        var allMethodScopes = GetAllScopes(symbolMethod);
        Symbol[]? allLocals = null;

        for (var k = 0; k < allMethodScopes.Count; k++)
        {
            var currentScope = allMethodScopes[k];
            for (var l = 0; l < currentScope.Locals.Count; l++)
            {
                var localSymbol = currentScope.Locals[l];
                if (localSymbol.Index > methodLocals.Length || string.IsNullOrEmpty(localSymbol.Name))
                {
                    continue;
                }

                var line = UnknownEndLineEntireScope;
                for (var m = 0; m < symbolMethod.SequencePoints.Count; m++)
                {
                    if (symbolMethod.SequencePoints[m].Offset >= currentScope.StartOffset)
                    {
                        line = symbolMethod.SequencePoints[m].Line;
                        break;
                    }
                }

                Local? local = null;
                // for (var i = 0; i < methodLocals.Length; i++)
                // {
                // if (methodLocals[i].Index != localSymbol.Index)
                // {
                // continue;
                // }

                // local = methodLocals[i];
                // break;
                // }

                if (local == null)
                {
                    continue;
                }

                // if (IsCompilerGeneratedAttributeDefined(local.Type.ToTypeDefOrRef().CustomAttributes))
                // {
                // continue;
                // }

                localsSymbol[localsCount] = new Symbol
                {
                    Name = localSymbol.Name,
                    Type = local.Type?.FullName,
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
                    // Type = field.FieldType.FullName,
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
