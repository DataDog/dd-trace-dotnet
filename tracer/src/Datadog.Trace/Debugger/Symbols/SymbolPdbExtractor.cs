// <copyright file="SymbolPdbExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Linq;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.Emit;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;

namespace Datadog.Trace.Debugger.Symbols;

internal class SymbolPdbExtractor : SymbolExtractor
{
    private readonly DatadogPdbReader _pdbReader;

    private bool _disposed;

    internal SymbolPdbExtractor(DatadogPdbReader pdbReader)
        : base(pdbReader.Module)
    {
        _pdbReader = pdbReader;
    }

    protected override Model.Scope CreateMethodScope(TypeDef type, MethodDef method)
    {
        var methodScope = base.CreateMethodScope(type, method);
        if (_pdbReader.GetMethodSymbolInfoOrDefault(method.MDToken.ToInt32()) is not { } symbolMethod)
        {
            return methodScope;
        }

        var firstSq = symbolMethod.SequencePoints.FirstOrDefault(sq => sq.IsHidden() == false);
        var typeSourceFile = firstSq.Document?.URL;
        var startLine = firstSq.Line == 0 ? -1 : firstSq.Line;
        var lastSq = symbolMethod.SequencePoints.LastOrDefault(sq => sq.IsHidden() == false);
        var endLine = lastSq.EndLine;
        var startColumn = firstSq.Column == 0 ? -1 : firstSq.Column;
        var endColumn = lastSq.EndColumn;

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

    private Symbol[]? GetLocalsSymbol(MethodDef method, int startLine, SymbolMethod symbolMethod, out int localsCount)
    {
        localsCount = 0;
        if (method.Body is not { Variables.Count: > 0 })
        {
            return null;
        }

        var methodLocals = method.Body.Variables;
        var localsSymbol = new Symbol[methodLocals.Count];
        var allMethodScopes = GetAllScopes(symbolMethod);
        Symbol[]? allLocals = null;

        for (var k = 0; k < allMethodScopes.Count; k++)
        {
            var currentScope = allMethodScopes[k];
            for (var l = 0; l < currentScope.Locals.Count; l++)
            {
                var localSymbol = currentScope.Locals[l];
                if (localSymbol.Index > methodLocals.Count || string.IsNullOrEmpty(localSymbol.Name))
                {
                    continue;
                }

                var line = -1;
                for (var m = 0; m < symbolMethod.SequencePoints.Count; m++)
                {
                    if (symbolMethod.SequencePoints[m].Offset >= currentScope.StartOffset)
                    {
                        line = symbolMethod.SequencePoints[m].Line;
                        break;
                    }
                }

                Local? local = null;
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

                if (IsCompilerGeneratedAttributeDefined(local.Type.ToTypeDefOrRef().CustomAttributes))
                {
                    continue;
                }

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

        if (IsCompilerGeneratedAttributeDefined(method.DeclaringType.CustomAttributes))
        {
            var fields = method.DeclaringType.Fields;
            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field.Name.StartsWith("<>"))
                {
                    continue;
                }

                if (allLocals == null)
                {
                    allLocals = new Symbol[localsCount + fields.Count - i];
                    Array.Copy(localsSymbol, 0, allLocals, 0, localsCount);
                }

                allLocals[localsCount] = new Symbol
                {
                    Name = field.Name.String,
                    Type = field.FieldType.FullName,
                    SymbolType = SymbolType.Local,
                    Line = startLine
                };

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
