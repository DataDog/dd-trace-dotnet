// <copyright file="ModuleMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.MD;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// IMetaData* emulation for one module. Mirrors the CLR's RegMeta semantics for the calls the native tracer makes.
/// </summary>
internal sealed unsafe class ModuleMetadata : IMetaDataImport2, IMetaDataAssemblyImport, IMetaDataEmit2, IMetaDataAssemblyEmit
{
    private readonly ModuleState _state;
    private readonly EmulatedRuntime _runtime;
    private readonly EnumRegistry _enums = new();
    private readonly NativeObjects.IMetaDataImport2 _import;
    private readonly NativeObjects.IMetaDataEmit2 _emit;
    private readonly NativeObjects.IMetaDataAssemblyImport _assemblyImport;
    private readonly NativeObjects.IMetaDataAssemblyEmit _assemblyEmit;

    public ModuleMetadata(EmulatedRuntime runtime, ModuleState state)
    {
        _runtime = runtime;
        _state = state;
        _import = NativeObjects.IMetaDataImport2.Wrap(this);
        _emit = NativeObjects.IMetaDataEmit2.Wrap(this);
        _assemblyImport = NativeObjects.IMetaDataAssemblyImport.Wrap(this);
        _assemblyEmit = NativeObjects.IMetaDataAssemblyEmit.Wrap(this);
    }

    public ModuleState State => _state;

    private Metadata Md => _state.Metadata;

    private TablesStream Tables => _state.Tables;

    private static string Join(string ns, string name) => string.IsNullOrEmpty(ns) ? name : ns + "." + name;

    private static (string Namespace, string Name) Split(string fullName)
    {
        var index = fullName.LastIndexOf('.');
        return index < 0 ? (string.Empty, fullName) : (fullName.Substring(0, index), fullName.Substring(index + 1));
    }

    private static uint Rid(int token) => (uint)token & 0x00FFFFFF;

    private static Table TableOf(int token) => (Table)((uint)token >> 24);

    private static int Tok(Table table, uint rid) => (int)ModuleState.Token(table, rid);

    private static bool SignatureEquals(byte[] left, byte* right, uint length)
    {
        if (right == null)
        {
            return true;
        }

        if (left.Length != length)
        {
            return false;
        }

        for (var i = 0; i < length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    // ---------------------------------------------------------------- IUnknown

    public HResult QueryInterface(in Guid guid, out IntPtr ptr)
    {
        if (guid == IUnknown.Guid || guid == IMetaDataImport.Guid || guid == IMetaDataImport2.Guid)
        {
            ptr = _import;
            return HResult.S_OK;
        }

        if (guid == IMetaDataEmit.Guid || guid == IMetaDataEmit2.Guid)
        {
            ptr = _emit;
            return HResult.S_OK;
        }

        if (guid == IMetaDataAssemblyImport.Guid)
        {
            ptr = _assemblyImport;
            return HResult.S_OK;
        }

        if (guid == IMetaDataAssemblyEmit.Guid)
        {
            ptr = _assemblyEmit;
            return HResult.S_OK;
        }

        AotLog.Warn($"ModuleMetadata.QueryInterface: unsupported interface {guid}");
        ptr = IntPtr.Zero;
        return HResult.E_NOINTERFACE;
    }

    public int AddRef() => 1;

    public int Release() => 1;

    // ---------------------------------------------------------------- helpers

    private string Str(uint offset) => Md.StringsStream.ReadNoNull(offset).String;

    private byte[] Blob(uint offset) => Md.BlobStream.Read(offset) ?? Array.Empty<byte>();

    private (IntPtr Pointer, int Length) Pin(int token, Func<byte[]> factory) => _runtime.Blobs.Get(_state, token, factory);

    private string TypeDefFullName(uint rid)
    {
        Tables.TryReadTypeDefRow(rid, out var row);
        return Join(Str(row.Namespace), Str(row.Name));
    }

    private uint EnclosingTypeOf(uint typeDefRid)
    {
        var type = _state.Module.ResolveTypeDef(typeDefRid);
        return type?.DeclaringType?.Rid ?? 0;
    }

    private (uint Scope, string FullName) TypeRefInfo(int token)
    {
        if (_state.IsOverlay((uint)token))
        {
            var overlay = _state.TypeRefs[_state.OverlayIndex((uint)token)];
            return (overlay.ResolutionScope, Join(overlay.Namespace, overlay.Name));
        }

        Tables.TryReadTypeRefRow(Rid(token), out var row);
        CodedToken.ResolutionScope.Decode(row.ResolutionScope, out uint scope);
        return (scope, Join(Str(row.Namespace), Str(row.Name)));
    }

    private (uint Parent, string Name, byte[] Signature) MemberRefInfo(int token)
    {
        if (_state.IsOverlay((uint)token))
        {
            var overlay = _state.MemberRefs[_state.OverlayIndex((uint)token)];
            return (overlay.Parent, overlay.Name, overlay.Signature);
        }

        Tables.TryReadMemberRefRow(Rid(token), out var row);
        CodedToken.MemberRefParent.Decode(row.Class, out uint parent);
        return (parent, Str(row.Name), Blob(row.Signature));
    }

    private IEnumerable<int> AllRows(Table table, int overlayCount)
    {
        var rows = _state.OriginalRows(table) + (uint)overlayCount;
        for (uint rid = 1; rid <= rows; rid++)
        {
            yield return Tok(table, rid);
        }
    }

    // ---------------------------------------------------------------- IMetaDataImport: enumeration

    public void CloseEnum(HCORENUM hEnum) => _enums.Close(hEnum);

    public HResult CountEnum(HCORENUM hEnum, uint* pulCount) => _enums.Count(hEnum, pulCount);

    public HResult ResetEnum(HCORENUM hEnum, uint ulPos) => _enums.Reset(hEnum, ulPos);

    public HResult EnumTypeDefs(HCORENUM* phEnum, MdTypeDef* rTypeDefs, uint cMax, uint* pcTypeDefs)
    {
        // Like the CLR, skip the <Module> type (RID 1).
        return _enums.Fill(phEnum, () => AllRows(Table.TypeDef, 0).Skip(1), rTypeDefs, cMax, pcTypeDefs);
    }

    public HResult EnumTypeRefs(HCORENUM* phEnum, MdTypeRef* rTypeRefs, uint cMax, uint* pcTypeRefs)
    {
        lock (_state.Sync)
        {
            return _enums.Fill(phEnum, () => AllRows(Table.TypeRef, _state.TypeRefs.Count).ToArray(), rTypeRefs, cMax, pcTypeRefs);
        }
    }

    public HResult EnumTypeSpecs(HCORENUM* phEnum, MdTypeSpec* rTypeSpecs, uint cmax, out uint pcTypeSpecs)
    {
        uint fetched;
        HResult hr;
        lock (_state.Sync)
        {
            hr = _enums.Fill(phEnum, () => AllRows(Table.TypeSpec, _state.TypeSpecs.Count).ToArray(), rTypeSpecs, cmax, &fetched);
        }

        pcTypeSpecs = fetched;
        return hr;
    }

    public HResult EnumModuleRefs(HCORENUM* phEnum, MdModuleRef* rModuleRefs, uint cmax, out uint pcModuleRefs)
    {
        uint fetched;
        HResult hr;
        lock (_state.Sync)
        {
            hr = _enums.Fill(phEnum, () => AllRows(Table.ModuleRef, _state.ModuleRefs.Count).ToArray(), rModuleRefs, cmax, &fetched);
        }

        pcModuleRefs = fetched;
        return hr;
    }

    public HResult EnumInterfaceImpls(HCORENUM* phEnum, MdTypeDef td, MdInterfaceImpl* rImpls, uint cMax, uint* pcImpls)
    {
        return _enums.Fill(
            phEnum,
            () =>
            {
                var list = Md.GetInterfaceImplRidList(Rid(td.Value));
                var result = new List<int>();
                for (var i = 0; i < list.Count; i++)
                {
                    result.Add(Tok(Table.InterfaceImpl, list[i]));
                }

                return result;
            },
            rImpls,
            cMax,
            pcImpls);
    }

    public HResult GetInterfaceImplProps(MdInterfaceImpl iiImpl, out MdTypeDef pClass, out MdToken ptkIface)
    {
        Tables.TryReadInterfaceImplRow(Rid(iiImpl.Value), out var row);
        CodedToken.TypeDefOrRef.Decode(row.Interface, out uint iface);
        pClass = new MdTypeDef(Tok(Table.TypeDef, row.Class));
        ptkIface = new MdToken((int)iface);
        return HResult.S_OK;
    }

    private IEnumerable<int> MethodsOf(uint typeDefRid)
    {
        var list = Md.GetMethodRidList(typeDefRid);
        for (var i = 0; i < list.Count; i++)
        {
            yield return Tok(Table.Method, list[i]);
        }
    }

    private IEnumerable<int> FieldsOf(uint typeDefRid)
    {
        var list = Md.GetFieldRidList(typeDefRid);
        for (var i = 0; i < list.Count; i++)
        {
            yield return Tok(Table.Field, list[i]);
        }
    }

    private string MethodName(int token)
    {
        Tables.TryReadMethodRow(Rid(token), out var row);
        return Str(row.Name);
    }

    private string FieldName(int token)
    {
        Tables.TryReadFieldRow(Rid(token), out var row);
        return Str(row.Name);
    }

    public HResult EnumMethods(HCORENUM* phEnum, MdTypeDef cl, MdMethodDef* rMethods, uint cMax, out uint pcTokens)
    {
        uint fetched;
        var hr = _enums.Fill(phEnum, () => MethodsOf(Rid(cl.Value)).ToArray(), rMethods, cMax, &fetched);
        pcTokens = fetched;
        return hr;
    }

    public HResult EnumMethodsWithName(HCORENUM* phEnum, MdTypeDef cl, char* szName, MdMethodDef* rMethods, uint cMax, uint* pcTokens)
    {
        var name = NativeBuffers.ReadString(szName);
        return _enums.Fill(phEnum, () => MethodsOf(Rid(cl.Value)).Where(t => name == null || MethodName(t) == name).ToArray(), rMethods, cMax, pcTokens);
    }

    public HResult EnumFields(HCORENUM* phEnum, MdTypeDef cl, MdFieldDef* rFields, uint cMax, out uint pcTokens)
    {
        uint fetched;
        var hr = _enums.Fill(phEnum, () => FieldsOf(Rid(cl.Value)).ToArray(), rFields, cMax, &fetched);
        pcTokens = fetched;
        return hr;
    }

    public HResult EnumFieldsWithName(HCORENUM* phEnum, MdTypeDef cl, char* szName, MdFieldDef* rFields, uint cMax, out uint pcTokens)
    {
        var name = NativeBuffers.ReadString(szName);
        uint fetched;
        var hr = _enums.Fill(phEnum, () => FieldsOf(Rid(cl.Value)).Where(t => name == null || FieldName(t) == name).ToArray(), rFields, cMax, &fetched);
        pcTokens = fetched;
        return hr;
    }

    public HResult EnumMemberRefs(HCORENUM* phEnum, MdToken tkParent, MdMemberRef* rMemberRefs, uint cMax, uint* pcTokens)
    {
        lock (_state.Sync)
        {
            var parent = (uint)tkParent.Value;
            return _enums.Fill(
                phEnum,
                () => AllRows(Table.MemberRef, _state.MemberRefs.Count).Where(t => MemberRefInfo(t).Parent == parent).ToArray(),
                rMemberRefs,
                cMax,
                pcTokens);
        }
    }

    public HResult EnumProperties(HCORENUM* phEnum, MdTypeDef td, MdProperty* rProperties, uint cMax, uint* pcProperties)
    {
        return _enums.Fill(
            phEnum,
            () =>
            {
                var type = _state.Module.ResolveTypeDef(Rid(td.Value));
                return type == null ? Array.Empty<int>() : type.Properties.Select(p => (int)p.MDToken.Raw).ToArray();
            },
            rProperties,
            cMax,
            pcProperties);
    }

    public HResult EnumCustomAttributes(HCORENUM* phEnum, MdToken tk, MdToken tkType, MdCustomAttribute* rCustomAttributes, uint cMax, uint* pcCustomAttributes)
    {
        return _enums.Fill(
            phEnum,
            () =>
            {
                IEnumerable<uint> rids;
                if (tk.Value == 0)
                {
                    rids = Enumerable.Range(1, (int)_state.OriginalRows(Table.CustomAttribute)).Select(i => (uint)i);
                }
                else
                {
                    var list = Md.GetCustomAttributeRidList(TableOf(tk.Value), Rid(tk.Value));
                    var collected = new List<uint>();
                    for (var i = 0; i < list.Count; i++)
                    {
                        collected.Add(list[i]);
                    }

                    rids = collected;
                }

                var result = new List<int>();
                foreach (var rid in rids)
                {
                    if (tkType.Value != 0)
                    {
                        Tables.TryReadCustomAttributeRow(rid, out var row);
                        CodedToken.CustomAttributeType.Decode(row.Type, out uint ctor);
                        if (AttributeTypeOfConstructor(ctor) != (uint)tkType.Value)
                        {
                            continue;
                        }
                    }

                    result.Add(Tok(Table.CustomAttribute, rid));
                }

                return result;
            },
            rCustomAttributes,
            cMax,
            pcCustomAttributes);
    }

    private uint AttributeTypeOfConstructor(uint ctor)
    {
        if ((Table)(ctor >> 24) == Table.Method)
        {
            return ModuleState.Token(Table.TypeDef, Md.GetOwnerTypeOfMethod(ctor & 0x00FFFFFF));
        }

        return MemberRefInfo((int)ctor).Parent;
    }

    public HResult GetCustomAttributeProps(MdCustomAttribute cv, MdToken* ptkObj, MdToken* ptkType, IntPtr* ppBlob, uint* pcbSize)
    {
        if (!Tables.TryReadCustomAttributeRow(Rid(cv.Value), out var row))
        {
            return NativeBuffers.RecordNotFound;
        }

        CodedToken.HasCustomAttribute.Decode(row.Parent, out uint parent);
        CodedToken.CustomAttributeType.Decode(row.Type, out uint ctor);
        NativeBuffers.Set(ptkObj, new MdToken((int)parent));
        NativeBuffers.Set(ptkType, new MdToken((int)ctor));
        var blob = Pin(cv.Value, () => Blob(row.Value));
        NativeBuffers.Set(ppBlob, blob.Pointer);
        NativeBuffers.Set(pcbSize, (uint)blob.Length);
        return HResult.S_OK;
    }

    public HResult GetCustomAttributeByName(MdToken tkObj, char* szName, out void* ppData, out uint pcbData)
    {
        ppData = null;
        pcbData = 0;
        var name = NativeBuffers.ReadString(szName);
        var list = Md.GetCustomAttributeRidList(TableOf(tkObj.Value), Rid(tkObj.Value));
        for (var i = 0; i < list.Count; i++)
        {
            Tables.TryReadCustomAttributeRow(list[i], out var row);
            CodedToken.CustomAttributeType.Decode(row.Type, out uint ctor);
            var type = (int)AttributeTypeOfConstructor(ctor);
            var typeName = TableOf(type) switch
            {
                Table.TypeDef => TypeDefFullName(Rid(type)),
                Table.TypeRef => TypeRefInfo(type).FullName,
                _ => null,
            };

            if (typeName == name)
            {
                var blob = Pin(Tok(Table.CustomAttribute, list[i]), () => Blob(row.Value));
                ppData = (void*)blob.Pointer;
                pcbData = (uint)blob.Length;
                return HResult.S_OK;
            }
        }

        return HResult.S_FALSE;
    }

    // ---------------------------------------------------------------- IMetaDataImport: properties

    public HResult GetScopeProps(char* szName, uint cchName, out uint pchName, out Guid pmvid)
    {
        uint length;
        var hr = NativeBuffers.WriteString(_state.Module.Name, szName, cchName, &length);
        pchName = length;
        pmvid = _state.Module.Mvid ?? Guid.Empty;
        return hr;
    }

    public HResult GetModuleFromScope(MdModule* pmd)
    {
        NativeBuffers.Set(pmd, new MdModule(Tok(Table.Module, 1)));
        return HResult.S_OK;
    }

    public HResult FindTypeDefByName(char* szTypeDef, MdToken tkEnclosingClass, MdTypeDef* ptd)
    {
        var name = NativeBuffers.ReadString(szTypeDef);
        var enclosing = TableOf(tkEnclosingClass.Value) == Table.TypeDef ? Rid(tkEnclosingClass.Value) : 0;
        var rows = _state.OriginalRows(Table.TypeDef);
        for (uint rid = 1; rid <= rows; rid++)
        {
            Tables.TryReadTypeDefRow(rid, out var row);
            var full = Join(Str(row.Namespace), Str(row.Name));
            if (full != name && Str(row.Name) != name)
            {
                continue;
            }

            if (full != name && enclosing == 0)
            {
                continue;
            }

            if (EnclosingTypeOf(rid) != enclosing)
            {
                continue;
            }

            NativeBuffers.Set(ptd, new MdTypeDef(Tok(Table.TypeDef, rid)));
            return HResult.S_OK;
        }

        return NativeBuffers.RecordNotFound;
    }

    public HResult GetTypeDefProps(MdTypeDef td, char* szTypeDef, uint cchTypeDef, uint* pchTypeDef, int* pdwTypeDefFlags, MdToken* ptkExtends)
    {
        if (!Tables.TryReadTypeDefRow(Rid(td.Value), out var row))
        {
            return NativeBuffers.RecordNotFound;
        }

        NativeBuffers.Set(pdwTypeDefFlags, (int)row.Flags);
        CodedToken.TypeDefOrRef.Decode(row.Extends, out uint extends);
        NativeBuffers.Set(ptkExtends, new MdToken((int)extends));
        return NativeBuffers.WriteString(Join(Str(row.Namespace), Str(row.Name)), szTypeDef, cchTypeDef, pchTypeDef);
    }

    public HResult GetNestedClassProps(MdTypeDef tdNestedClass, MdTypeDef* ptdEnclosingClass)
    {
        var enclosing = EnclosingTypeOf(Rid(tdNestedClass.Value));
        if (enclosing == 0)
        {
            return NativeBuffers.RecordNotFound;
        }

        NativeBuffers.Set(ptdEnclosingClass, new MdTypeDef(Tok(Table.TypeDef, enclosing)));
        return HResult.S_OK;
    }

    public HResult GetTypeRefProps(MdTypeRef tr, MdToken* ptkResolutionScope, char* szName, uint cchName, uint* pchName)
    {
        lock (_state.Sync)
        {
            var (scope, fullName) = TypeRefInfo(tr.Value);
            NativeBuffers.Set(ptkResolutionScope, new MdToken((int)scope));
            return NativeBuffers.WriteString(fullName, szName, cchName, pchName);
        }
    }

    public HResult FindTypeRef(MdToken tkResolutionScope, char* szName, MdTypeRef* ptr)
    {
        var name = NativeBuffers.ReadString(szName);
        var scope = Rid(tkResolutionScope.Value) == 0 ? 0u : (uint)tkResolutionScope.Value;
        lock (_state.Sync)
        {
            foreach (var token in AllRows(Table.TypeRef, _state.TypeRefs.Count))
            {
                var info = TypeRefInfo(token);
                if (info.Scope == scope && info.FullName == name)
                {
                    NativeBuffers.Set(ptr, new MdTypeRef(token));
                    return HResult.S_OK;
                }
            }
        }

        return NativeBuffers.RecordNotFound;
    }

    public HResult GetMethodProps(MdMethodDef mb, MdToken* pClass, char* szMethod, uint cchMethod, uint* pchMethod, int* pdwAttr, IntPtr* ppvSigBlob, uint* pcbSigBlob, uint* pulCodeRVA, int* pdwImplFlags)
    {
        if (TableOf(mb.Value) != Table.Method || !Tables.TryReadMethodRow(Rid(mb.Value), out var row))
        {
            return NativeBuffers.RecordNotFound;
        }

        NativeBuffers.Set(pClass, new MdToken(Tok(Table.TypeDef, Md.GetOwnerTypeOfMethod(Rid(mb.Value)))));
        NativeBuffers.Set(pdwAttr, (int)row.Flags);
        NativeBuffers.Set(pdwImplFlags, (int)row.ImplFlags);
        NativeBuffers.Set(pulCodeRVA, row.RVA);
        var blob = Pin(mb.Value, () => Blob(row.Signature));
        NativeBuffers.Set(ppvSigBlob, blob.Pointer);
        NativeBuffers.Set(pcbSigBlob, (uint)blob.Length);
        return NativeBuffers.WriteString(Str(row.Name), szMethod, cchMethod, pchMethod);
    }

    public HResult GetFieldProps(MdFieldDef mb, MdTypeDef* pClass, char* szField, uint cchField, uint* pchField, int* pdwAttr, IntPtr* ppvSigBlob, uint* pcbSigBlob, int* pdwCPlusTypeFlag, IntPtr* ppValue, uint* pcchValue)
    {
        if (TableOf(mb.Value) != Table.Field || !Tables.TryReadFieldRow(Rid(mb.Value), out var row))
        {
            return NativeBuffers.RecordNotFound;
        }

        NativeBuffers.Set(pClass, new MdTypeDef(Tok(Table.TypeDef, Md.GetOwnerTypeOfField(Rid(mb.Value)))));
        NativeBuffers.Set(pdwAttr, (int)row.Flags);
        var blob = Pin(mb.Value, () => Blob(row.Signature));
        NativeBuffers.Set(ppvSigBlob, blob.Pointer);
        NativeBuffers.Set(pcbSigBlob, (uint)blob.Length);
        NativeBuffers.Set(pdwCPlusTypeFlag, 0);
        NativeBuffers.Set(ppValue, IntPtr.Zero);
        NativeBuffers.Set(pcchValue, 0u);
        return NativeBuffers.WriteString(Str(row.Name), szField, cchField, pchField);
    }

    public HResult GetMemberProps(MdToken mb, MdToken* pClass, char* szMember, uint cchMember, uint* pchMember, int* pdwAttr, IntPtr* ppvSigBlob, uint* pcbSigBlob, uint* pulCodeRVA, int* pdwImplFlags, int* pdwCPlusTypeFlag, IntPtr* ppValue, uint* pcchValue)
    {
        if (TableOf(mb.Value) == Table.Method)
        {
            NativeBuffers.Set(pdwCPlusTypeFlag, 0);
            NativeBuffers.Set(ppValue, IntPtr.Zero);
            NativeBuffers.Set(pcchValue, 0u);
            return GetMethodProps(new MdMethodDef(mb.Value), pClass, szMember, cchMember, pchMember, pdwAttr, ppvSigBlob, pcbSigBlob, pulCodeRVA, pdwImplFlags);
        }

        NativeBuffers.Set(pulCodeRVA, 0u);
        NativeBuffers.Set(pdwImplFlags, 0);
        return GetFieldProps(new MdFieldDef(mb.Value), (MdTypeDef*)pClass, szMember, cchMember, pchMember, pdwAttr, ppvSigBlob, pcbSigBlob, pdwCPlusTypeFlag, ppValue, pcchValue);
    }

    public HResult GetMemberRefProps(MdMemberRef mr, MdToken* ptk, char* szMember, uint cchMember, uint* pchMember, IntPtr* ppvSigBlob, uint* pbSig)
    {
        lock (_state.Sync)
        {
            var (parent, name, signature) = MemberRefInfo(mr.Value);
            NativeBuffers.Set(ptk, new MdToken((int)parent));
            var blob = Pin(mr.Value, () => signature);
            NativeBuffers.Set(ppvSigBlob, blob.Pointer);
            NativeBuffers.Set(pbSig, (uint)blob.Length);
            return NativeBuffers.WriteString(name, szMember, cchMember, pchMember);
        }
    }

    public HResult FindMethod(MdTypeDef td, char* szName, byte* pvSigBlob, uint cbSigBlob, MdMethodDef* pmb)
    {
        var name = NativeBuffers.ReadString(szName);
        foreach (var token in MethodsOf(Rid(td.Value)))
        {
            Tables.TryReadMethodRow(Rid(token), out var row);
            if (Str(row.Name) == name && SignatureEquals(Blob(row.Signature), pvSigBlob, cbSigBlob))
            {
                NativeBuffers.Set(pmb, new MdMethodDef(token));
                return HResult.S_OK;
            }
        }

        return NativeBuffers.RecordNotFound;
    }

    public HResult FindField(MdTypeDef td, char* szName, byte* pvSigBlob, uint cbSigBlob, out MdFieldDef pmb)
    {
        var name = NativeBuffers.ReadString(szName);
        foreach (var token in FieldsOf(Rid(td.Value)))
        {
            Tables.TryReadFieldRow(Rid(token), out var row);
            if (Str(row.Name) == name && SignatureEquals(Blob(row.Signature), pvSigBlob, cbSigBlob))
            {
                pmb = new MdFieldDef(token);
                return HResult.S_OK;
            }
        }

        pmb = default;
        return NativeBuffers.RecordNotFound;
    }

    public HResult FindMemberRef(MdTypeRef td, char* szName, byte* pvSigBlob, uint cbSigBlob, out MdMemberRef pmr)
    {
        var name = NativeBuffers.ReadString(szName);
        lock (_state.Sync)
        {
            foreach (var token in AllRows(Table.MemberRef, _state.MemberRefs.Count))
            {
                var info = MemberRefInfo(token);
                if (info.Parent == (uint)td.Value && info.Name == name && SignatureEquals(info.Signature, pvSigBlob, cbSigBlob))
                {
                    pmr = new MdMemberRef(token);
                    return HResult.S_OK;
                }
            }
        }

        pmr = default;
        return NativeBuffers.RecordNotFound;
    }

    public HResult GetPropertyProps(MdProperty prop, MdTypeDef* pClass, char* szProperty, uint cchProperty, uint* pchProperty, int* pdwPropFlags, IntPtr* ppvSig, uint* pbSig, int* pdwCPlusTypeFlag, IntPtr* ppDefaultValue, uint* pcchDefaultValue, MdMethodDef* pmdSetter, MdMethodDef* pmdGetter, MdMethodDef* rmdOtherMethod, uint cMax, uint* pcOtherMethod)
    {
        var property = _state.Module.ResolveProperty(Rid(prop.Value));
        if (property == null)
        {
            return NativeBuffers.RecordNotFound;
        }

        Tables.TryReadPropertyRow(Rid(prop.Value), out var row);
        NativeBuffers.Set(pClass, new MdTypeDef((int)property.DeclaringType.MDToken.Raw));
        NativeBuffers.Set(pdwPropFlags, (int)row.PropFlags);
        var blob = Pin(prop.Value, () => Blob(row.Type));
        NativeBuffers.Set(ppvSig, blob.Pointer);
        NativeBuffers.Set(pbSig, (uint)blob.Length);
        NativeBuffers.Set(pdwCPlusTypeFlag, 0);
        NativeBuffers.Set(ppDefaultValue, IntPtr.Zero);
        NativeBuffers.Set(pcchDefaultValue, 0u);
        NativeBuffers.Set(pmdSetter, new MdMethodDef((int)(property.SetMethod?.MDToken.Raw ?? 0x06000000)));
        NativeBuffers.Set(pmdGetter, new MdMethodDef((int)(property.GetMethod?.MDToken.Raw ?? 0x06000000)));
        NativeBuffers.Set(pcOtherMethod, 0u);
        return NativeBuffers.WriteString(Str(row.Name), szProperty, cchProperty, pchProperty);
    }

    public HResult GetSigFromToken(MdSignature mdSig, IntPtr* ppvSig, uint* pcbSig)
    {
        lock (_state.Sync)
        {
            byte[] signature;
            if (_state.IsOverlay((uint)mdSig.Value))
            {
                signature = _state.StandAloneSigs[_state.OverlayIndex((uint)mdSig.Value)];
            }
            else if (Tables.TryReadStandAloneSigRow(Rid(mdSig.Value), out var row))
            {
                signature = Blob(row.Signature);
            }
            else
            {
                return NativeBuffers.RecordNotFound;
            }

            var blob = Pin(mdSig.Value, () => signature);
            NativeBuffers.Set(ppvSig, blob.Pointer);
            NativeBuffers.Set(pcbSig, (uint)blob.Length);
            return HResult.S_OK;
        }
    }

    public HResult GetTypeSpecFromToken(MdTypeSpec typespec, IntPtr* ppvSig, uint* pcbSig)
    {
        lock (_state.Sync)
        {
            byte[] signature;
            if (_state.IsOverlay((uint)typespec.Value))
            {
                signature = _state.TypeSpecs[_state.OverlayIndex((uint)typespec.Value)];
            }
            else if (Tables.TryReadTypeSpecRow(Rid(typespec.Value), out var row))
            {
                signature = Blob(row.Signature);
            }
            else
            {
                return NativeBuffers.RecordNotFound;
            }

            var blob = Pin(typespec.Value, () => signature);
            NativeBuffers.Set(ppvSig, blob.Pointer);
            NativeBuffers.Set(pcbSig, (uint)blob.Length);
            return HResult.S_OK;
        }
    }

    public HResult GetModuleRefProps(MdModuleRef mur, char* szName, uint cchName, out uint pchName)
    {
        string name;
        lock (_state.Sync)
        {
            if (_state.IsOverlay((uint)mur.Value))
            {
                name = _state.ModuleRefs[_state.OverlayIndex((uint)mur.Value)];
            }
            else
            {
                Tables.TryReadModuleRefRow(Rid(mur.Value), out var row);
                name = Str(row.Name);
            }
        }

        uint length;
        var hr = NativeBuffers.WriteString(name, szName, cchName, &length);
        pchName = length;
        return hr;
    }

    public HResult GetUserString(MdString stk, char* szString, uint cchString, uint* pchString)
    {
        string? value;
        lock (_state.Sync)
        {
            var offset = Rid(stk.Value);
            if (!_state.UserStrings.TryGetValue(offset, out value))
            {
                value = Md.USStream.Read(offset);
            }
        }

        if (value == null)
        {
            return NativeBuffers.RecordNotFound;
        }

        NativeBuffers.Set(pchString, (uint)value.Length);
        if (szString != null)
        {
            var count = Math.Min(value.Length, (int)cchString);
            for (var i = 0; i < count; i++)
            {
                szString[i] = value[i];
            }
        }

        return HResult.S_OK;
    }

    public HResult GetPinvokeMap(MdToken tk, out int pdwMappingFlags, char* szImportName, uint cchImportName, out uint pchImportName, out MdModuleRef pmrImportDLL)
    {
        pdwMappingFlags = 0;
        pchImportName = 0;
        pmrImportDLL = default;
        var rid = Md.GetImplMapRid(TableOf(tk.Value), Rid(tk.Value));
        if (rid == 0 || !Tables.TryReadImplMapRow(rid, out var row))
        {
            return NativeBuffers.RecordNotFound;
        }

        pdwMappingFlags = row.MappingFlags;
        pmrImportDLL = new MdModuleRef(Tok(Table.ModuleRef, row.ImportScope));
        uint length;
        var hr = NativeBuffers.WriteString(Str(row.ImportName), szImportName, cchImportName, &length);
        pchImportName = length;
        return hr;
    }

    public HResult GetRVA(MdToken tk, uint* pulCodeRVA, int* pdwImplFlags)
    {
        if (TableOf(tk.Value) == Table.Method && Tables.TryReadMethodRow(Rid(tk.Value), out var row))
        {
            NativeBuffers.Set(pulCodeRVA, row.RVA);
            NativeBuffers.Set(pdwImplFlags, (int)row.ImplFlags);
            return HResult.S_OK;
        }

        return NativeBuffers.RecordNotFound;
    }

    public bool IsValidToken(MdToken tk)
    {
        var table = TableOf(tk.Value);
        var rid = Rid(tk.Value);
        lock (_state.Sync)
        {
            return rid != 0 && (rid <= _state.OriginalRows(table) || _state.IsOverlay((uint)tk.Value));
        }
    }

    // ---------------------------------------------------------------- IMetaDataImport2

    public HResult EnumGenericParams(HCORENUM* phEnum, MdToken tk, MdGenericParam* rGenericParams, uint cMax, out uint pcGenericParams)
    {
        uint fetched;
        var hr = _enums.Fill(
            phEnum,
            () =>
            {
                var list = Md.GetGenericParamRidList(TableOf(tk.Value), Rid(tk.Value));
                var result = new List<int>();
                for (var i = 0; i < list.Count; i++)
                {
                    result.Add(Tok(Table.GenericParam, list[i]));
                }

                return result;
            },
            rGenericParams,
            cMax,
            &fetched);
        pcGenericParams = fetched;
        return hr;
    }

    public HResult GetGenericParamProps(MdGenericParam gp, out uint pulParamSeq, out int pdwParamFlags, MdToken* ptOwner, out int reserved, char* wzname, uint cchName, out uint pchName)
    {
        reserved = 0;
        if (!Tables.TryReadGenericParamRow(Rid(gp.Value), out var row))
        {
            pulParamSeq = 0;
            pdwParamFlags = 0;
            pchName = 0;
            return NativeBuffers.RecordNotFound;
        }

        pulParamSeq = row.Number;
        pdwParamFlags = row.Flags;
        CodedToken.TypeOrMethodDef.Decode(row.Owner, out uint owner);
        NativeBuffers.Set(ptOwner, new MdToken((int)owner));
        uint length;
        var hr = NativeBuffers.WriteString(Str(row.Name), wzname, cchName, &length);
        pchName = length;
        return hr;
    }

    public HResult EnumGenericParamConstraints(HCORENUM* phEnum, MdGenericParam tk, MdGenericParamConstraint* rGenericParamConstraints, uint cMax, out uint pcGenericParamConstraints)
    {
        uint fetched;
        var hr = _enums.Fill(
            phEnum,
            () =>
            {
                var list = Md.GetGenericParamConstraintRidList(Rid(tk.Value));
                var result = new List<int>();
                for (var i = 0; i < list.Count; i++)
                {
                    result.Add(Tok(Table.GenericParamConstraint, list[i]));
                }

                return result;
            },
            rGenericParamConstraints,
            cMax,
            &fetched);
        pcGenericParamConstraints = fetched;
        return hr;
    }

    public HResult GetGenericParamConstraintProps(MdGenericParamConstraint gpc, MdGenericParam* ptGenericParam, out MdToken ptkConstraintType)
    {
        if (!Tables.TryReadGenericParamConstraintRow(Rid(gpc.Value), out var row))
        {
            ptkConstraintType = default;
            return NativeBuffers.RecordNotFound;
        }

        NativeBuffers.Set(ptGenericParam, new MdGenericParam(Tok(Table.GenericParam, row.Owner)));
        CodedToken.TypeDefOrRef.Decode(row.Constraint, out uint constraint);
        ptkConstraintType = new MdToken((int)constraint);
        return HResult.S_OK;
    }

    public HResult GetMethodSpecProps(MdMethodSpec mi, MdToken* tkParent, IntPtr* ppvSigBlob, uint* pcbSigBlob)
    {
        lock (_state.Sync)
        {
            uint parent;
            byte[] instantiation;
            if (_state.IsOverlay((uint)mi.Value))
            {
                var overlay = _state.MethodSpecs[_state.OverlayIndex((uint)mi.Value)];
                parent = overlay.Method;
                instantiation = overlay.Instantiation;
            }
            else if (Tables.TryReadMethodSpecRow(Rid(mi.Value), out var row))
            {
                CodedToken.MethodDefOrRef.Decode(row.Method, out parent);
                instantiation = Blob(row.Instantiation);
            }
            else
            {
                return NativeBuffers.RecordNotFound;
            }

            NativeBuffers.Set(tkParent, new MdToken((int)parent));
            var blob = Pin(mi.Value, () => instantiation);
            NativeBuffers.Set(ppvSigBlob, blob.Pointer);
            NativeBuffers.Set(pcbSigBlob, (uint)blob.Length);
            return HResult.S_OK;
        }
    }

    public HResult GetVersionString(char* pwzBuf, int ccBufSize, out int pccBufSize)
    {
        uint length;
        var hr = NativeBuffers.WriteString(_state.Module.RuntimeVersion, pwzBuf, (uint)ccBufSize, &length);
        pccBufSize = (int)length;
        return hr;
    }

    // ---------------------------------------------------------------- IMetaDataAssemblyImport

    public HResult GetAssemblyFromScope(out MdAssembly ptkAssembly)
    {
        ptkAssembly = new MdAssembly(Tok(Table.Assembly, 1));
        return _state.OriginalRows(Table.Assembly) > 0 ? HResult.S_OK : NativeBuffers.RecordNotFound;
    }

    public HResult GetAssemblyProps(MdAssembly mda, IntPtr* ppbPublicKey, int* pcbPublicKey, int* pulHashAlgId, char* szName, uint cchName, uint* pchName, ASSEMBLYMETADATA* pMetaData, int* pdwAssemblyFlags)
    {
        if (!Tables.TryReadAssemblyRow(1, out var row))
        {
            return NativeBuffers.RecordNotFound;
        }

        var publicKey = Pin(mda.Value, () => Blob(row.PublicKey));
        NativeBuffers.Set(ppbPublicKey, publicKey.Pointer);
        NativeBuffers.Set(pcbPublicKey, publicKey.Length);
        NativeBuffers.Set(pulHashAlgId, (int)row.HashAlgId);
        NativeBuffers.Set(pdwAssemblyFlags, (int)row.Flags);
        WriteAssemblyMetadata((AssemblyMetadataRaw*)pMetaData, row.MajorVersion, row.MinorVersion, row.BuildNumber, row.RevisionNumber, Str(row.Locale));
        return NativeBuffers.WriteString(Str(row.Name), szName, cchName, pchName);
    }

    private static void WriteAssemblyMetadata(AssemblyMetadataRaw* metadata, ushort major, ushort minor, ushort build, ushort revision, string locale)
    {
        if (metadata == null)
        {
            return;
        }

        metadata->MajorVersion = major;
        metadata->MinorVersion = minor;
        metadata->BuildNumber = build;
        metadata->RevisionNumber = revision;
        uint length;
        NativeBuffers.WriteString(locale ?? string.Empty, metadata->Locale, metadata->LocaleLength, &length);
        metadata->LocaleLength = length;
        metadata->ProcessorCount = 0;
        metadata->OperatingSystemCount = 0;
    }

    public HResult EnumAssemblyRefs(HCORENUM* phEnum, MdAssemblyRef* rAssemblyRefs, uint cMax, out uint pcTokens)
    {
        uint fetched;
        HResult hr;
        lock (_state.Sync)
        {
            hr = _enums.Fill(phEnum, () => AllRows(Table.AssemblyRef, _state.AssemblyRefs.Count).ToArray(), rAssemblyRefs, cMax, &fetched);
        }

        pcTokens = fetched;
        return hr;
    }

    public HResult GetAssemblyRefProps(MdAssemblyRef mdar, IntPtr* ppbPublicKeyOrToken, int* pcbPublicKeyOrToken, char* szName, uint cchName, uint* pchName, ASSEMBLYMETADATA* pMetaData, IntPtr* ppbHashValue, int* pcbHashValue, int* pdwAssemblyRefFlags)
    {
        lock (_state.Sync)
        {
            string name;
            byte[] publicKey;
            Version version;
            string locale;
            uint flags;
            if (_state.IsOverlay((uint)mdar.Value))
            {
                var overlay = _state.AssemblyRefs[_state.OverlayIndex((uint)mdar.Value)];
                (name, publicKey, version, locale, flags) = (overlay.Name, overlay.PublicKeyOrToken, overlay.Version, overlay.Culture, overlay.Flags);
            }
            else if (Tables.TryReadAssemblyRefRow(Rid(mdar.Value), out var row))
            {
                name = Str(row.Name);
                publicKey = Blob(row.PublicKeyOrToken);
                version = new Version(row.MajorVersion, row.MinorVersion, row.BuildNumber, row.RevisionNumber);
                locale = Str(row.Locale);
                flags = row.Flags;
            }
            else
            {
                return NativeBuffers.RecordNotFound;
            }

            var key = Pin(mdar.Value, () => publicKey);
            NativeBuffers.Set(ppbPublicKeyOrToken, key.Pointer);
            NativeBuffers.Set(pcbPublicKeyOrToken, key.Length);
            NativeBuffers.Set(ppbHashValue, IntPtr.Zero);
            NativeBuffers.Set(pcbHashValue, 0);
            NativeBuffers.Set(pdwAssemblyRefFlags, (int)flags);
            WriteAssemblyMetadata((AssemblyMetadataRaw*)pMetaData, (ushort)version.Major, (ushort)version.Minor, (ushort)version.Build, (ushort)version.Revision, locale);
            return NativeBuffers.WriteString(name, szName, cchName, pchName);
        }
    }

    public HResult FindExportedTypeByName(char* szName, MdToken mdtExportedType, MdExportedType* ptkExportedType)
    {
        var name = NativeBuffers.ReadString(szName);
        var rows = _state.OriginalRows(Table.ExportedType);
        for (uint rid = 1; rid <= rows; rid++)
        {
            Tables.TryReadExportedTypeRow(rid, out var row);
            if (Join(Str(row.TypeNamespace), Str(row.TypeName)) == name)
            {
                NativeBuffers.Set(ptkExportedType, new MdExportedType(Tok(Table.ExportedType, rid)));
                return HResult.S_OK;
            }
        }

        return NativeBuffers.RecordNotFound;
    }

    public HResult GetExportedTypeProps(MdExportedType mdct, char* szName, uint cchName, out uint pchName, MdToken* ptkImplementation, MdTypeDef* ptkTypeDef, out int pdwExportedTypeFlags)
    {
        if (!Tables.TryReadExportedTypeRow(Rid(mdct.Value), out var row))
        {
            pchName = 0;
            pdwExportedTypeFlags = 0;
            return NativeBuffers.RecordNotFound;
        }

        CodedToken.Implementation.Decode(row.Implementation, out uint implementation);
        NativeBuffers.Set(ptkImplementation, new MdToken((int)implementation));
        NativeBuffers.Set(ptkTypeDef, new MdTypeDef((int)row.TypeDefId));
        pdwExportedTypeFlags = (int)row.Flags;
        uint length;
        var hr = NativeBuffers.WriteString(Join(Str(row.TypeNamespace), Str(row.TypeName)), szName, cchName, &length);
        pchName = length;
        return hr;
    }

    // ---------------------------------------------------------------- IMetaDataEmit / Emit2 / AssemblyEmit

    public HResult DefineTypeRefByName(MdToken tkResolutionScope, char* szName, MdTypeRef* ptr)
    {
        var name = NativeBuffers.ReadString(szName);
        if (FindTypeRef(tkResolutionScope, szName, ptr) == HResult.S_OK)
        {
            return HResult.S_OK;
        }

        lock (_state.Sync)
        {
            var (ns, simpleName) = Split(name ?? string.Empty);
            var token = (int)_state.NextToken(Table.TypeRef, _state.TypeRefs);
            // Like RegMeta, a nil resolution scope (the native tracer can pass mdAssemblyRefNil) is stored as a nil coded
            // index, which reads back as mdTokenNil.
            var scope = Rid(tkResolutionScope.Value) == 0 ? 0u : (uint)tkResolutionScope.Value;
            _state.TypeRefs.Add(new ModuleState.OverlayTypeRef { ResolutionScope = scope, Namespace = ns, Name = simpleName });
            NativeBuffers.Set(ptr, new MdTypeRef(token));
            AotLog.Debug($"[{_state.Module.Name}] DefineTypeRefByName {name} (scope 0x{tkResolutionScope.Value:x8}) -> 0x{token:x8}");
            return HResult.S_OK;
        }
    }

    public HResult DefineMemberRef(MdToken tkImport, char* szName, IntPtr pvSigBlob, int cbSigBlob, MdMemberRef* pmr)
    {
        var name = NativeBuffers.ReadString(szName);
        var signature = NativeBuffers.ReadBytes(pvSigBlob, cbSigBlob);
        NativeBuffers.Set(pmr, new MdMemberRef(ImportMemberRef((uint)tkImport.Value, name ?? string.Empty, signature)));
        return HResult.S_OK;
    }

    public HResult DefineImportMember(IntPtr pAssemImport, byte* pbHashValue, int cbHashValue, IntPtr pImport, MdToken mbMember, IntPtr pAssemEmit, MdToken tkParent, MdMemberRef* pmr)
    {
        var source = _runtime.FindMetadata(pImport);
        if (source is null || !source.TryGetMember(mbMember.Value, out var name, out var signature))
        {
            AotLog.Warn($"[{_state.Module.Name}] DefineImportMember: member 0x{mbMember.Value:x8} not found in the import scope");
            return HResult.E_INVALIDARG;
        }

        var token = ImportMemberRef((uint)tkParent.Value, name, new MetadataImporter(source, this).ImportSignature(signature));
        NativeBuffers.Set(pmr, new MdMemberRef(token));
        AotLog.Debug($"[{_state.Module.Name}] DefineImportMember {source.State.Module.Name}!0x{mbMember.Value:x8} {name} -> 0x{token:x8}");
        return HResult.S_OK;
    }

    public HResult DefineMethodSpec(MdToken tkParent, IntPtr pvSigBlob, int cbSigBlob, MdMethodSpec* pmi)
    {
        var signature = NativeBuffers.ReadBytes(pvSigBlob, cbSigBlob);
        lock (_state.Sync)
        {
            var original = _state.OriginalRows(Table.MethodSpec);
            for (uint rid = 1; rid <= original; rid++)
            {
                Tables.TryReadMethodSpecRow(rid, out var row);
                CodedToken.MethodDefOrRef.Decode(row.Method, out uint method);
                if (method == (uint)tkParent.Value && Blob(row.Instantiation).AsSpan().SequenceEqual(signature))
                {
                    NativeBuffers.Set(pmi, new MdMethodSpec(Tok(Table.MethodSpec, rid)));
                    return HResult.S_OK;
                }
            }

            for (var i = 0; i < _state.MethodSpecs.Count; i++)
            {
                if (_state.MethodSpecs[i].Method == (uint)tkParent.Value && _state.MethodSpecs[i].Instantiation.AsSpan().SequenceEqual(signature))
                {
                    NativeBuffers.Set(pmi, new MdMethodSpec(Tok(Table.MethodSpec, original + (uint)i + 1)));
                    return HResult.S_OK;
                }
            }

            var token = (int)_state.NextToken(Table.MethodSpec, _state.MethodSpecs);
            _state.MethodSpecs.Add(new ModuleState.OverlayMethodSpec { Method = (uint)tkParent.Value, Instantiation = signature });
            NativeBuffers.Set(pmi, new MdMethodSpec(token));
            return HResult.S_OK;
        }
    }

    public HResult GetTokenFromSig(IntPtr pvSig, int cbSig, MdSignature* pmsig)
    {
        var signature = NativeBuffers.ReadBytes(pvSig, cbSig);
        lock (_state.Sync)
        {
            var original = _state.OriginalRows(Table.StandAloneSig);
            for (uint rid = 1; rid <= original; rid++)
            {
                Tables.TryReadStandAloneSigRow(rid, out var row);
                if (Blob(row.Signature).AsSpan().SequenceEqual(signature))
                {
                    NativeBuffers.Set(pmsig, new MdSignature(Tok(Table.StandAloneSig, rid)));
                    return HResult.S_OK;
                }
            }

            for (var i = 0; i < _state.StandAloneSigs.Count; i++)
            {
                if (_state.StandAloneSigs[i].AsSpan().SequenceEqual(signature))
                {
                    NativeBuffers.Set(pmsig, new MdSignature(Tok(Table.StandAloneSig, original + (uint)i + 1)));
                    return HResult.S_OK;
                }
            }

            var token = (int)_state.NextToken(Table.StandAloneSig, _state.StandAloneSigs);
            _state.StandAloneSigs.Add(signature);
            NativeBuffers.Set(pmsig, new MdSignature(token));
            return HResult.S_OK;
        }
    }

    public HResult GetTokenFromTypeSpec(IntPtr pvSig, int cbSig, MdTypeSpec* ptypespec)
    {
        NativeBuffers.Set(ptypespec, new MdTypeSpec(ImportTypeSpec(NativeBuffers.ReadBytes(pvSig, cbSig))));
        return HResult.S_OK;
    }

    public HResult DefineUserString(char* szString, int cchString, MdString* pstk)
    {
        var value = NativeBuffers.ReadString(szString, cchString);
        lock (_state.Sync)
        {
            foreach (var pair in _state.UserStrings)
            {
                if (pair.Value == value)
                {
                    NativeBuffers.Set(pstk, new MdString((int)(0x70000000 | pair.Key)));
                    return HResult.S_OK;
                }
            }

            var offset = _state.UserStringBase + _state.NextUserStringOffset;
            _state.NextUserStringOffset += (uint)(value.Length * 2) + 8;
            _state.UserStrings[offset] = value;
            NativeBuffers.Set(pstk, new MdString((int)(0x70000000 | offset)));
            return HResult.S_OK;
        }
    }

    public HResult DefineModuleRef(char* szName, MdModuleRef* pmur)
    {
        var name = NativeBuffers.ReadString(szName);
        lock (_state.Sync)
        {
            var token = (int)_state.NextToken(Table.ModuleRef, _state.ModuleRefs);
            _state.ModuleRefs.Add(name ?? string.Empty);
            NativeBuffers.Set(pmur, new MdModuleRef(token));
            return HResult.S_OK;
        }
    }

    public HResult DefineAssemblyRef(IntPtr pbPublicKeyOrToken, int cbPublicKeyOrToken, char* szName, ASSEMBLYMETADATA* pMetaData, IntPtr pbHashValue, int cbHashValue, int dwAssemblyRefFlags, MdToken* pmdar)
    {
        var name = NativeBuffers.ReadString(szName);
        var metadata = (AssemblyMetadataRaw*)pMetaData;
        var version = metadata == null ? new Version(0, 0, 0, 0) : new Version(metadata->MajorVersion, metadata->MinorVersion, metadata->BuildNumber, metadata->RevisionNumber);
        var culture = metadata == null || metadata->Locale == null ? string.Empty : new string(metadata->Locale);
        lock (_state.Sync)
        {
            var token = (int)_state.NextToken(Table.AssemblyRef, _state.AssemblyRefs);
            _state.AssemblyRefs.Add(new ModuleState.OverlayAssemblyRef
            {
                Name = name ?? string.Empty,
                Version = version,
                Culture = culture,
                PublicKeyOrToken = NativeBuffers.ReadBytes(pbPublicKeyOrToken, cbPublicKeyOrToken),
                Flags = (uint)dwAssemblyRefFlags,
            });
            NativeBuffers.Set(pmdar, new MdToken(token));
            AotLog.Debug($"[{_state.Module.Name}] DefineAssemblyRef {name} {version} -> 0x{token:x8}");
            return HResult.S_OK;
        }
    }

    // ---------------------------------------------------------------- cross-scope import (MetadataImporter)

    /// <summary>
    /// Whether the pointer is one of the interfaces of this scope.
    /// </summary>
    public bool Owns(IntPtr pointer)
        => pointer == (IntPtr)_import || pointer == (IntPtr)_emit || pointer == (IntPtr)_assemblyImport || pointer == (IntPtr)_assemblyEmit;

    /// <summary>
    /// The name and signature of a MethodDef, FieldDef or MemberRef of this scope.
    /// </summary>
    internal bool TryGetMember(int token, out string name, out byte[] signature)
    {
        lock (_state.Sync)
        {
            switch (TableOf(token))
            {
                case Table.Method when Tables.TryReadMethodRow(Rid(token), out var method):
                    (name, signature) = (Str(method.Name), Blob(method.Signature));
                    return true;
                case Table.Field when Tables.TryReadFieldRow(Rid(token), out var field):
                    (name, signature) = (Str(field.Name), Blob(field.Signature));
                    return true;
                case Table.MemberRef:
                    (_, name, signature) = MemberRefInfo(token);
                    return true;
                default:
                    (name, signature) = (string.Empty, Array.Empty<byte>());
                    return false;
            }
        }
    }

    internal (string FullName, uint EnclosingRid) GetTypeDef(uint rid)
    {
        lock (_state.Sync)
        {
            return (TypeDefFullName(rid), EnclosingTypeOf(rid));
        }
    }

    internal (uint Scope, string FullName) GetTypeRef(int token)
    {
        lock (_state.Sync)
        {
            return TypeRefInfo(token);
        }
    }

    internal byte[] GetTypeSpec(int token)
    {
        lock (_state.Sync)
        {
            if (_state.IsOverlay((uint)token))
            {
                return _state.TypeSpecs[_state.OverlayIndex((uint)token)];
            }

            Tables.TryReadTypeSpecRow(Rid(token), out var row);
            return Blob(row.Signature);
        }
    }

    internal AssemblyIdentity GetAssemblyIdentity()
    {
        var assembly = _state.Module.Assembly ?? throw new InvalidOperationException($"{_state.Module.Name} isn't an assembly manifest module.");
        return new AssemblyIdentity(assembly.Name, assembly.Version, assembly.Culture ?? string.Empty, assembly.PublicKeyToken?.Data ?? Array.Empty<byte>(), 0);
    }

    internal AssemblyIdentity GetAssemblyRefIdentity(int token)
    {
        lock (_state.Sync)
        {
            if (_state.IsOverlay((uint)token))
            {
                var overlay = _state.AssemblyRefs[_state.OverlayIndex((uint)token)];
                return new AssemblyIdentity(overlay.Name, overlay.Version, overlay.Culture, overlay.PublicKeyOrToken, overlay.Flags);
            }

            Tables.TryReadAssemblyRefRow(Rid(token), out var row);
            return new AssemblyIdentity(Str(row.Name), new Version(row.MajorVersion, row.MinorVersion, row.BuildNumber, row.RevisionNumber), Str(row.Locale), Blob(row.PublicKeyOrToken), row.Flags);
        }
    }

    /// <summary>
    /// The TypeDef of a top-level type when the assembly is this one.
    /// </summary>
    internal int? FindOwnTypeDef(AssemblyIdentity assembly, string fullName)
    {
        if (!string.Equals(assembly.Name, _state.Module.Assembly?.Name?.String, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return _state.Module.Find(fullName, isReflectionName: false) is { DeclaringType: null } type ? (int)type.MDToken.Raw : null;
    }

    internal int ImportAssemblyRef(AssemblyIdentity assembly)
    {
        lock (_state.Sync)
        {
            var original = _state.OriginalRows(Table.AssemblyRef);
            for (uint rid = 1; rid <= original; rid++)
            {
                Tables.TryReadAssemblyRefRow(rid, out var row);
                if (string.Equals(Str(row.Name), assembly.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return Tok(Table.AssemblyRef, rid);
                }
            }

            for (var i = 0; i < _state.AssemblyRefs.Count; i++)
            {
                if (string.Equals(_state.AssemblyRefs[i].Name, assembly.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return Tok(Table.AssemblyRef, original + (uint)i + 1);
                }
            }

            var token = (int)_state.NextToken(Table.AssemblyRef, _state.AssemblyRefs);
            _state.AssemblyRefs.Add(new ModuleState.OverlayAssemblyRef
            {
                Name = assembly.Name,
                Version = assembly.Version,
                Culture = assembly.Culture,
                PublicKeyOrToken = assembly.PublicKeyOrToken,
                Flags = assembly.Flags,
            });
            AotLog.Debug($"[{_state.Module.Name}] AssemblyRef {assembly.Name} {assembly.Version} imported -> 0x{token:x8}");
            return token;
        }
    }

    internal int ImportTypeRef(int scope, string fullName)
    {
        lock (_state.Sync)
        {
            foreach (var token in AllRows(Table.TypeRef, _state.TypeRefs.Count))
            {
                var info = TypeRefInfo(token);
                if (info.Scope == (uint)scope && info.FullName == fullName)
                {
                    return token;
                }
            }

            var (ns, simpleName) = Split(fullName);
            var created = (int)_state.NextToken(Table.TypeRef, _state.TypeRefs);
            _state.TypeRefs.Add(new ModuleState.OverlayTypeRef { ResolutionScope = (uint)scope, Namespace = ns, Name = simpleName });
            return created;
        }
    }

    internal int ImportTypeSpec(byte[] signature)
    {
        lock (_state.Sync)
        {
            var original = _state.OriginalRows(Table.TypeSpec);
            for (uint rid = 1; rid <= original; rid++)
            {
                Tables.TryReadTypeSpecRow(rid, out var row);
                if (Blob(row.Signature).AsSpan().SequenceEqual(signature))
                {
                    return Tok(Table.TypeSpec, rid);
                }
            }

            for (var i = 0; i < _state.TypeSpecs.Count; i++)
            {
                if (_state.TypeSpecs[i].AsSpan().SequenceEqual(signature))
                {
                    return Tok(Table.TypeSpec, original + (uint)i + 1);
                }
            }

            var token = (int)_state.NextToken(Table.TypeSpec, _state.TypeSpecs);
            _state.TypeSpecs.Add(signature);
            return token;
        }
    }

    internal int ImportMemberRef(uint parent, string name, byte[] signature)
    {
        lock (_state.Sync)
        {
            foreach (var existing in AllRows(Table.MemberRef, _state.MemberRefs.Count))
            {
                var info = MemberRefInfo(existing);
                if (info.Parent == parent && info.Name == name && info.Signature.AsSpan().SequenceEqual(signature))
                {
                    return existing;
                }
            }

            var token = (int)_state.NextToken(Table.MemberRef, _state.MemberRefs);
            _state.MemberRefs.Add(new ModuleState.OverlayMemberRef { Parent = parent, Name = name, Signature = signature });
            return token;
        }
    }

    // Changes the native tracer makes to Datadog.Trace (PInvoke maps, startup helpers) are not persisted.
    public HResult DefinePinvokeMap(MdToken tk, int dwMappingFlags, char* szImportName, MdModuleRef mrImportDLL) => HResult.S_OK;

    public HResult SetPinvokeMap(MdToken tk, int dwMappingFlags, char* szImportName, MdModuleRef mrImportDLL) => HResult.S_OK;

    public HResult DeletePinvokeMap(MdToken tk) => HResult.S_OK;

    public HResult SetMethodImplFlags(MdMethodDef md, int dwImplFlags) => HResult.S_OK;
}
#endif
