// <copyright file="GenericContextRemapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Linq;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// Moves types from the generic context of a rewritten method to the one of its registration type, whose generic
/// parameters are the ones of the declaring type followed by the ones of the method: <c>!n</c> stays <c>!n</c> and
/// <c>!!n</c> becomes <c>!(typeParameters + n)</c>.
/// </summary>
internal sealed class GenericContextRemapper
{
    private readonly int _typeParameters;
    private readonly TypeDef? _owner;

    public GenericContextRemapper(int typeParameters, TypeDef? owner)
    {
        _typeParameters = typeParameters;
        _owner = owner;
    }

    public TypeSig Remap(TypeSig signature)
    {
        switch (signature)
        {
            case GenericMVar methodVariable:
                return new GenericVar((uint)(_typeParameters + methodVariable.Number), _owner);
            case GenericVar typeVariable:
                return new GenericVar(typeVariable.Number, _owner);
            case CorLibTypeSig or TypeDefOrRefSig:
                return signature;
            case GenericInstSig instance:
                return new GenericInstSig(instance.GenericType, instance.GenericArguments.Select(Remap).ToList());
            case SZArraySig array:
                return new SZArraySig(Remap(array.Next));
            case ArraySig array:
                return new ArraySig(Remap(array.Next), array.Rank, array.Sizes, array.LowerBounds);
            case ByRefSig byRef:
                return new ByRefSig(Remap(byRef.Next));
            case PtrSig pointer:
                return new PtrSig(Remap(pointer.Next));
            case PinnedSig pinned:
                return new PinnedSig(Remap(pinned.Next));
            case CModReqdSig required:
                return new CModReqdSig(required.Modifier, Remap(required.Next));
            case CModOptSig optional:
                return new CModOptSig(optional.Modifier, Remap(optional.Next));
            default:
                throw new NotSupportedException($"Unsupported type signature in a CallTarget instantiation: {signature.FullName}");
        }
    }
}
#endif
