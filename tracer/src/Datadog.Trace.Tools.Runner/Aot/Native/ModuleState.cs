// <copyright file="ModuleState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.MD;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// One loaded module: the original metadata (read through dnlib) plus the rows the native rewriter defines on top.
/// Original rows keep their tokens; new rows get the next RID of their table, like the CLR's in-memory metadata.
/// </summary>
internal sealed class ModuleState
{
    private readonly Dictionary<uint, IMDTokenProvider> _materialized = new();

    public ModuleState(int id, string path, ModuleDefMD module, bool writable)
    {
        Id = id;
        Path = path;
        Module = module;
        Writable = writable;
        Metadata = module.Metadata;
        Tables = Metadata.TablesStream;
        UserStringBase = Metadata.USStream.StreamLength;
    }

    public int Id { get; }

    public int AssemblyId => Id;

    public string AssemblyName => Module.Assembly?.Name?.String ?? Module.Name.String;

    public string Path { get; }

    public ModuleDefMD Module { get; }

    public Metadata Metadata { get; }

    public TablesStream Tables { get; }

    public bool Writable { get; }

    public object Sync { get; } = new();

    public List<OverlayTypeRef> TypeRefs { get; } = new();

    public List<OverlayMemberRef> MemberRefs { get; } = new();

    public List<OverlayMethodSpec> MethodSpecs { get; } = new();

    public List<byte[]> TypeSpecs { get; } = new();

    public List<byte[]> StandAloneSigs { get; } = new();

    public List<OverlayAssemblyRef> AssemblyRefs { get; } = new();

    public List<string> ModuleRefs { get; } = new();

    public Dictionary<uint, string> UserStrings { get; } = new();

    public uint UserStringBase { get; }

    public uint NextUserStringOffset { get; set; }

    /// <summary>Gets the new IL bodies set by the rewriter, keyed by MethodDef RID.</summary>
    public Dictionary<uint, byte[]> NewBodies { get; } = new();

    public static uint Token(Table table, uint rid) => ((uint)table << 24) | rid;

    public uint OriginalRows(Table table) => Tables.Get(table).Rows;

    public uint NextToken<T>(Table table, List<T> overlay) => Token(table, OriginalRows(table) + (uint)overlay.Count + 1);

    public bool IsOverlay(uint token)
    {
        var table = (Table)(token >> 24);
        var rid = token & 0x00FFFFFF;
        return table switch
        {
            Table.TypeRef or Table.MemberRef or Table.MethodSpec or Table.TypeSpec or Table.StandAloneSig or Table.AssemblyRef or Table.ModuleRef
                => rid > OriginalRows(table),
            _ => false,
        };
    }

    public int OverlayIndex(uint token) => (int)((token & 0x00FFFFFF) - OriginalRows((Table)(token >> 24)) - 1);

    /// <summary>
    /// Resolves a token (original or overlay) to a dnlib object usable when writing the module.
    /// </summary>
    public IMDTokenProvider Resolve(uint token, GenericParamContext context = default)
    {
        if (!IsOverlay(token))
        {
            if ((token >> 24) == (uint)Table.Module)
            {
                return Module;
            }

            return Module.ResolveToken(token, context);
        }

        lock (Sync)
        {
            if (_materialized.TryGetValue(token, out var existing))
            {
                return existing;
            }
        }

        var created = Materialize(token, context);
        lock (Sync)
        {
            _materialized[token] = created;
        }

        return created;
    }

    private IMDTokenProvider Materialize(uint token, GenericParamContext context)
    {
        var index = OverlayIndex(token);
        switch ((Table)(token >> 24))
        {
            case Table.TypeRef:
                {
                    var row = TypeRefs[index];
                    var scope = (IResolutionScope)Resolve(row.ResolutionScope, context);
                    return new TypeRefUser(Module, row.Namespace, row.Name, scope);
                }

            case Table.AssemblyRef:
                {
                    var row = AssemblyRefs[index];
                    var publicKey = row.PublicKeyOrToken is { Length: > 0 }
                                        ? ((row.Flags & 1) != 0 ? (PublicKeyBase)new PublicKey(row.PublicKeyOrToken) : new PublicKeyToken(row.PublicKeyOrToken))
                                        : null;
                    return new AssemblyRefUser(row.Name, row.Version, publicKey, row.Culture) { Attributes = (AssemblyAttributes)(row.Flags & ~1u) };
                }

            case Table.ModuleRef:
                return new ModuleRefUser(Module, ModuleRefs[index]);

            case Table.MemberRef:
                {
                    var row = MemberRefs[index];
                    var parent = (IMemberRefParent)Resolve(row.Parent, context);
                    return ReadSignature(row.Signature, context) switch
                    {
                        MethodSig methodSig => new MemberRefUser(Module, row.Name, methodSig, parent),
                        FieldSig fieldSig => new MemberRefUser(Module, row.Name, fieldSig, parent),
                        var other => throw new InvalidOperationException($"Unexpected MemberRef signature {other}"),
                    };
                }

            case Table.MethodSpec:
                {
                    var row = MethodSpecs[index];
                    var method = (IMethodDefOrRef)Resolve(row.Method, context);
                    var instantiation = (GenericInstMethodSig)ReadSignature(row.Instantiation, context);
                    return new MethodSpecUser(method, instantiation);
                }

            case Table.TypeSpec:
                return new TypeSpecUser(ReadTypeSignature(TypeSpecs[index], context));

            case Table.StandAloneSig:
                return ReadSignature(StandAloneSigs[index], context) switch
                {
                    LocalSig localSig => new StandAloneSigUser(localSig),
                    MethodSig methodSig => new StandAloneSigUser(methodSig),
                    var other => throw new InvalidOperationException($"Unexpected StandAloneSig signature {other}"),
                };

            default:
                throw new InvalidOperationException($"Unsupported overlay token 0x{token:x8}");
        }
    }

    public CallingConventionSig ReadSignature(byte[] blob, GenericParamContext context)
        => SignatureReader.ReadSig(new SignatureHelper(this), Module.CorLibTypes, blob, context)
           ?? throw new InvalidOperationException($"Invalid signature blob {Convert.ToHexString(blob)}");

    public TypeSig ReadTypeSignature(byte[] blob, GenericParamContext context)
        => SignatureReader.ReadTypeSig(new SignatureHelper(this), Module.CorLibTypes, blob, context)
           ?? throw new InvalidOperationException($"Invalid type signature blob {Convert.ToHexString(blob)}");

    private sealed class SignatureHelper : ISignatureReaderHelper
    {
        private readonly ModuleState _owner;

        public SignatureHelper(ModuleState owner)
        {
            _owner = owner;
        }

        public ITypeDefOrRef? ResolveTypeDefOrRef(uint codedToken, GenericParamContext gpContext)
        {
            if (!CodedToken.TypeDefOrRef.Decode(codedToken, out uint token))
            {
                return null;
            }

            return _owner.Resolve(token, gpContext) as ITypeDefOrRef;
        }

        public TypeSig? ConvertRTInternalAddress(IntPtr address) => null;
    }

    internal sealed class OverlayTypeRef
    {
        public uint ResolutionScope { get; init; }

        public string Namespace { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;
    }

    internal sealed class OverlayMemberRef
    {
        public uint Parent { get; init; }

        public string Name { get; init; } = string.Empty;

        public byte[] Signature { get; init; } = Array.Empty<byte>();
    }

    internal sealed class OverlayMethodSpec
    {
        public uint Method { get; init; }

        public byte[] Instantiation { get; init; } = Array.Empty<byte>();
    }

    internal sealed class OverlayAssemblyRef
    {
        public string Name { get; init; } = string.Empty;

        public Version Version { get; init; } = new(0, 0, 0, 0);

        public string Culture { get; init; } = string.Empty;

        public byte[] PublicKeyOrToken { get; init; } = Array.Empty<byte>();

        public uint Flags { get; init; }
    }
}
#endif
