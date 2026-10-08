// <copyright file="MetadataImporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using dnlib.DotNet.MD;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// Brings members of one scope into another, like the CLR's <c>IMetaDataEmit::DefineImportMember</c>: the type tokens of
/// a signature (TypeDef, TypeRef and TypeSpec of the source, nested types included) become TypeRefs of the target, with
/// the AssemblyRefs they need. The native tracer's call site instrumentation imports its aspect methods this way.
/// </summary>
internal sealed class MetadataImporter
{
    private const byte ElementTypeValueType = 0x11;
    private const byte ElementTypeClass = 0x12;
    private const byte ElementTypePtr = 0x0F;
    private const byte ElementTypeByRef = 0x10;
    private const byte ElementTypeVar = 0x13;
    private const byte ElementTypeArray = 0x14;
    private const byte ElementTypeGenericInst = 0x15;
    private const byte ElementTypeFnPtr = 0x1B;
    private const byte ElementTypeSzArray = 0x1D;
    private const byte ElementTypeMVar = 0x1E;
    private const byte ElementTypeCModReqd = 0x1F;
    private const byte ElementTypeCModOpt = 0x20;
    private const byte ElementTypeSentinel = 0x41;
    private const byte ElementTypePinned = 0x45;

    private readonly ModuleMetadata _source;
    private readonly ModuleMetadata _target;
    private readonly Dictionary<int, int> _types = new();

    public MetadataImporter(ModuleMetadata source, ModuleMetadata target)
    {
        _source = source;
        _target = target;
    }

    /// <summary>
    /// Returns the signature (method, field, local variables, property or method instantiation) with the type tokens of
    /// the target scope.
    /// </summary>
    public byte[] ImportSignature(byte[] signature)
    {
        var output = new List<byte>(signature.Length + 8);
        var position = 0;
        CopySignature(signature, ref position, output);
        return output.ToArray();
    }

    /// <summary>
    /// Returns the token of the target scope for a type of the source scope.
    /// </summary>
    public int ImportType(int token)
    {
        if (_types.TryGetValue(token, out var imported))
        {
            return imported;
        }

        var rid = (uint)token & 0x00FFFFFF;
        switch ((Table)((uint)token >> 24))
        {
            case Table.TypeDef:
            {
                var (fullName, enclosing) = _source.GetTypeDef(rid);
                imported = enclosing != 0
                               ? _target.ImportTypeRef(ImportType((int)ModuleState.Token(Table.TypeDef, enclosing)), fullName)
                               : ImportTopLevel(_source.GetAssemblyIdentity(), fullName);
                break;
            }

            case Table.TypeRef:
            {
                var (scope, fullName) = _source.GetTypeRef(token);
                imported = (Table)(scope >> 24) switch
                {
                    Table.AssemblyRef => ImportTopLevel(_source.GetAssemblyRefIdentity((int)scope), fullName),
                    Table.TypeRef => _target.ImportTypeRef(ImportType((int)scope), fullName),
                    Table.Module => ImportTopLevel(_source.GetAssemblyIdentity(), fullName),
                    _ => throw new NotSupportedException($"Type reference 0x{token:x8} with a resolution scope 0x{scope:x8} can't be imported."),
                };
                break;
            }

            case Table.TypeSpec:
                imported = _target.ImportTypeSpec(ImportSignatureOfType(_source.GetTypeSpec(token)));
                break;

            default:
                throw new NotSupportedException($"Token 0x{token:x8} isn't a type.");
        }

        _types[token] = imported;
        return imported;
    }

    private static uint ReadCompressed(byte[] data, ref int position)
    {
        var first = data[position];
        if ((first & 0x80) == 0)
        {
            position += 1;
            return first;
        }

        if ((first & 0xC0) == 0x80)
        {
            var value = ((uint)(first & 0x3F) << 8) | data[position + 1];
            position += 2;
            return value;
        }

        var large = ((uint)(first & 0x1F) << 24) | ((uint)data[position + 1] << 16) | ((uint)data[position + 2] << 8) | data[position + 3];
        position += 4;
        return large;
    }

    private static void WriteCompressed(List<byte> output, uint value)
    {
        if (value < 0x80)
        {
            output.Add((byte)value);
        }
        else if (value < 0x4000)
        {
            output.Add((byte)(0x80 | (value >> 8)));
            output.Add((byte)value);
        }
        else
        {
            output.Add((byte)(0xC0 | (value >> 24)));
            output.Add((byte)(value >> 16));
            output.Add((byte)(value >> 8));
            output.Add((byte)value);
        }
    }

    private static uint CopyCompressed(byte[] data, ref int position, List<byte> output)
    {
        var start = position;
        var value = ReadCompressed(data, ref position);
        for (var i = start; i < position; i++)
        {
            output.Add(data[i]);
        }

        return value;
    }

    /// <summary>
    /// A top-level type of an assembly: a TypeRef, or the TypeDef when the assembly is the target's.
    /// </summary>
    private int ImportTopLevel(AssemblyIdentity assembly, string fullName)
        => _target.FindOwnTypeDef(assembly, fullName) ?? _target.ImportTypeRef(_target.ImportAssemblyRef(assembly), fullName);

    private byte[] ImportSignatureOfType(byte[] signature)
    {
        var output = new List<byte>(signature.Length + 8);
        var position = 0;
        CopyType(signature, ref position, output);
        return output.ToArray();
    }

    private void CopySignature(byte[] data, ref int position, List<byte> output)
    {
        var callingConvention = data[position++];
        output.Add(callingConvention);
        switch (callingConvention & 0x0F)
        {
            case 0x06: // field
                CopyType(data, ref position, output);
                return;
            case 0x07: // local variables
            case 0x0A: // method instantiation
            {
                var count = CopyCompressed(data, ref position, output);
                for (var i = 0; i < count; i++)
                {
                    CopyType(data, ref position, output);
                }

                return;
            }

            case 0x08: // property
            {
                var count = CopyCompressed(data, ref position, output);
                CopyType(data, ref position, output);
                for (var i = 0; i < count; i++)
                {
                    CopyType(data, ref position, output);
                }

                return;
            }

            default: // method
            {
                if ((callingConvention & 0x10) != 0)
                {
                    CopyCompressed(data, ref position, output);
                }

                var count = CopyCompressed(data, ref position, output);
                CopyType(data, ref position, output);
                for (var i = 0; i < count; i++)
                {
                    if (data[position] == ElementTypeSentinel)
                    {
                        output.Add(data[position++]);
                    }

                    CopyType(data, ref position, output);
                }

                return;
            }
        }
    }

    private void CopyType(byte[] data, ref int position, List<byte> output)
    {
        var elementType = data[position++];
        output.Add(elementType);
        switch (elementType)
        {
            case ElementTypeCModReqd:
            case ElementTypeCModOpt:
                CopyTypeToken(data, ref position, output);
                CopyType(data, ref position, output);
                break;
            case ElementTypeValueType:
            case ElementTypeClass:
                CopyTypeToken(data, ref position, output);
                break;
            case ElementTypePtr:
            case ElementTypeByRef:
            case ElementTypeSzArray:
            case ElementTypePinned:
                CopyType(data, ref position, output);
                break;
            case ElementTypeGenericInst:
            {
                CopyType(data, ref position, output);
                var count = CopyCompressed(data, ref position, output);
                for (var i = 0; i < count; i++)
                {
                    CopyType(data, ref position, output);
                }

                break;
            }

            case ElementTypeVar:
            case ElementTypeMVar:
                CopyCompressed(data, ref position, output);
                break;
            case ElementTypeArray:
            {
                CopyType(data, ref position, output);
                CopyCompressed(data, ref position, output); // rank
                var sizes = CopyCompressed(data, ref position, output);
                for (var i = 0; i < sizes; i++)
                {
                    CopyCompressed(data, ref position, output);
                }

                // Lower bounds are signed, with the same lengths as the unsigned encoding: copied as they are.
                var lowerBounds = CopyCompressed(data, ref position, output);
                for (var i = 0; i < lowerBounds; i++)
                {
                    CopyCompressed(data, ref position, output);
                }

                break;
            }

            case ElementTypeFnPtr:
                CopySignature(data, ref position, output);
                break;
        }
    }

    private void CopyTypeToken(byte[] data, ref int position, List<byte> output)
    {
        var encoded = ReadCompressed(data, ref position);
        var table = (encoded & 3) switch
        {
            0 => Table.TypeDef,
            1 => Table.TypeRef,
            2 => Table.TypeSpec,
            _ => throw new NotSupportedException($"Invalid type token 0x{encoded:x} in a signature."),
        };
        var imported = (uint)ImportType((int)ModuleState.Token(table, encoded >> 2));
        var tag = (Table)(imported >> 24) switch
        {
            Table.TypeDef => 0u,
            Table.TypeRef => 1u,
            _ => 2u,
        };
        WriteCompressed(output, ((imported & 0x00FFFFFF) << 2) | tag);
    }
}
#endif
