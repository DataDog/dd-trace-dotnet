// <copyright file="IMetaDataImport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface IMetaDataImport : IUnknown
{
    public static readonly new Guid Guid = new("7DAC8207-D3AE-4c75-9B67-92801A497D44");

    void CloseEnum(HCORENUM hEnum)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.CloseEnum");
    }
    HResult CountEnum(HCORENUM hEnum, uint* pulCount)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.CountEnum");
        return HResult.E_NOTIMPL;
    }
    HResult ResetEnum(HCORENUM hEnum, uint ulPos)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.ResetEnum");
        return HResult.E_NOTIMPL;
    }
    HResult EnumTypeDefs(HCORENUM* phEnum, MdTypeDef* rTypeDefs, uint cMax, uint* pcTypeDefs)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumTypeDefs");
        return HResult.E_NOTIMPL;
    }
    HResult EnumInterfaceImpls(HCORENUM* phEnum, MdTypeDef td, MdInterfaceImpl* rImpls, uint cMax, uint* pcImpls)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumInterfaceImpls");
        return HResult.E_NOTIMPL;
    }
    HResult EnumTypeRefs(HCORENUM* phEnum, MdTypeRef* rTypeRefs, uint cMax, uint* pcTypeRefs)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumTypeRefs");
        return HResult.E_NOTIMPL;
    }

    HResult FindTypeDefByName(
        char* szTypeDef,              // [IN] Name of the Type.
        MdToken tkEnclosingClass,       // [IN] TypeDef/TypeRef for Enclosing class.
        MdTypeDef* ptd)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.FindTypeDefByName");
        return HResult.E_NOTIMPL;
    }             // [OUT] Put the TypeDef token here.

    HResult GetScopeProps(
        char* szName,                 // [OUT] Put the name here.
        uint cchName,                // [IN] Size of name buffer in wide chars.
        out uint pchName,               // [OUT] Put size of name (wide chars) here.
        out Guid pmvid)
    {
        pchName = default;
        pmvid = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetScopeProps");
        return HResult.E_NOTIMPL;
    }           // [OUT, OPTIONAL] Put MVID here.

    HResult GetModuleFromScope(
        MdModule* pmd)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetModuleFromScope");
        return HResult.E_NOTIMPL;
    }             // [OUT] Put mdModule token here.

    HResult GetTypeDefProps(
        MdTypeDef td,                   // [IN] TypeDef token for inquiry.
        char* szTypeDef,                // [OUT] Put name here.
        uint cchTypeDef,                // [IN] size of name buffer in wide chars.
        uint* pchTypeDef,               // [OUT] put size of name (wide chars) here.
        int* pdwTypeDefFlags,           // [OUT] Put flags here.
        MdToken* ptkExtends)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetTypeDefProps");
        return HResult.E_NOTIMPL;
    }           // [OUT] Put base class TypeDef/TypeRef here.

    HResult GetInterfaceImplProps(
        MdInterfaceImpl iiImpl,             // [IN] InterfaceImpl token.
        out MdTypeDef pClass,                // [OUT] Put implementing class token here.
        out MdToken ptkIface)
    {
        pClass = default;
        ptkIface = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetInterfaceImplProps");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put implemented interface token here.

    HResult GetTypeRefProps(
        MdTypeRef tr,                       // [IN] TypeRef token.
        MdToken* ptkResolutionScope,     // [OUT] Resolution scope, ModuleRef or AssemblyRef.
        char* szName,                       // [OUT] Name of the TypeRef.
        uint cchName,                       // [IN] Size of buffer.
        uint* pchName)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetTypeRefProps");
        return HResult.E_NOTIMPL;
    }                  // [OUT] Size of Name.

    HResult ResolveTypeRef(MdTypeRef tr, in Guid riid, void** ppIScope, out MdTypeDef ptd)
    {
        ptd = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.ResolveTypeRef");
        return HResult.E_NOTIMPL;
    }

    HResult EnumMembers(                 // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef cl,                     // [IN] TypeDef to scope the enumeration.
        MdToken* rMembers,             // [OUT] Put MemberDefs here.
        uint cMax,                   // [IN] Max MemberDefs to put.
        out uint pcTokens)
    {
        pcTokens = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumMembers");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put # put here.

    HResult EnumMembersWithName(         // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef cl,                     // [IN] TypeDef to scope the enumeration.
        char* szName,                 // [IN] Limit results to those with this name.
        MdToken* rMembers,             // [OUT] Put MemberDefs here.
        uint cMax,                   // [IN] Max MemberDefs to put.
        out uint pcTokens)
    {
        pcTokens = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumMembersWithName");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put # put here.

    HResult EnumMethods(                 // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef cl,                     // [IN] TypeDef to scope the enumeration.
        MdMethodDef* rMethods,             // [OUT] Put MethodDefs here.
        uint cMax,                   // [IN] Max MethodDefs to put.
        out uint pcTokens)
    {
        pcTokens = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumMethods");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put # put here.

    HResult EnumMethodsWithName(         // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef cl,                     // [IN] TypeDef to scope the enumeration.
        char* szName,                 // [IN] Limit results to those with this name.
        MdMethodDef* rMethods,             // [OU] Put MethodDefs here.
        uint cMax,                   // [IN] Max MethodDefs to put.
        uint* pcTokens)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumMethodsWithName");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put # put here.

    HResult EnumFields(                  // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef cl,                     // [IN] TypeDef to scope the enumeration.
        MdFieldDef* rFields,              // [OUT] Put FieldDefs here.
        uint cMax,                   // [IN] Max FieldDefs to put.
        out uint pcTokens)
    {
        pcTokens = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumFields");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put # put here.

    HResult EnumFieldsWithName(          // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef cl,                     // [IN] TypeDef to scope the enumeration.
        char* szName,                 // [IN] Limit results to those with this name.
        MdFieldDef* rFields,              // [OUT] Put MemberDefs here.
        uint cMax,                   // [IN] Max MemberDefs to put.
        out uint pcTokens)
    {
        pcTokens = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumFieldsWithName");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put # put here.

    HResult EnumParams(                  // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdMethodDef mb,                     // [IN] MethodDef to scope the enumeration.
        MdParamDef* rParams,              // [OUT] Put ParamDefs here.
        uint cMax,                   // [IN] Max ParamDefs to put.
        out uint pcTokens)
    {
        pcTokens = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumParams");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put # put here.

    HResult EnumMemberRefs(              // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdToken tkParent,               // [IN] Parent token to scope the enumeration.
        MdMemberRef* rMemberRefs,          // [OUT] Put MemberRefs here.
        uint cMax,                   // [IN] Max MemberRefs to put.
        uint* pcTokens)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumMemberRefs");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put # put here.

    HResult EnumMethodImpls(             // S_OK, S_FALSE, or error
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef td,                     // [IN] TypeDef to scope the enumeration.
        MdToken* rMethodBody,          // [OUT] Put Method Body tokens here.
        MdToken* rMethodDecl,          // [OUT] Put Method Declaration tokens here.
        uint cMax,                   // [IN] Max tokens to put.
        out uint pcTokens)
    {
        pcTokens = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumMethodImpls");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put # put here.

    HResult EnumPermissionSets(          // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdToken tk,                     // [IN] if !NIL, token to scope the enumeration.
        int dwActions,              // [IN] if !0, return only these actions.
        MdPermission* rPermission,         // [OUT] Put Permissions here.
        uint cMax,                   // [IN] Max Permissions to put.
        out uint pcTokens)
    {
        pcTokens = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumPermissionSets");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put # put here.

    HResult FindMember(
        MdTypeDef td,                     // [IN] given typedef
        char* szName,                 // [IN] member name
        byte* pvSigBlob,          // [IN] point to a blob value of CLR signature
        uint cbSigBlob,              // [IN] count of bytes in the signature blob
        out MdToken pmb)
    {
        pmb = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.FindMember");
        return HResult.E_NOTIMPL;
    }             // [OUT] matching memberdef

    HResult FindMethod(
        MdTypeDef td,                     // [IN] given typedef
        char* szName,                 // [IN] member name
        byte* pvSigBlob,          // [IN] point to a blob value of CLR signature
        uint cbSigBlob,              // [IN] count of bytes in the signature blob
        MdMethodDef* pmb)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.FindMethod");
        return HResult.E_NOTIMPL;
    }             // [OUT] matching memberdef

    HResult FindField(
        MdTypeDef td,                     // [IN] given typedef
        char* szName,                 // [IN] member name
        byte* pvSigBlob,          // [IN] point to a blob value of CLR signature
        uint cbSigBlob,              // [IN] count of bytes in the signature blob
        out MdFieldDef pmb)
    {
        pmb = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.FindField");
        return HResult.E_NOTIMPL;
    }             // [OUT] matching memberdef

    HResult FindMemberRef(
        MdTypeRef td,                     // [IN] given typeRef
        char* szName,                 // [IN] member name
        byte* pvSigBlob,          // [IN] point to a blob value of CLR signature
        uint cbSigBlob,              // [IN] count of bytes in the signature blob
        out MdMemberRef pmr)
    {
        pmr = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.FindMemberRef");
        return HResult.E_NOTIMPL;
    }             // [OUT] matching memberref

    HResult GetMethodProps(
        MdMethodDef mb,                     // The method for which to get props.
        MdToken* pClass,                // Put method's class here.
        char* szMethod,               // Put method's name here.
        uint cchMethod,              // Size of szMethod buffer in wide chars.
        uint* pchMethod,             // Put actual size here
        int* pdwAttr,               // Put flags here.
        IntPtr* ppvSigBlob,        // [OUT] point to the blob value of meta data
        uint* pcbSigBlob,            // [OUT] actual size of signature blob
        uint* pulCodeRVA,            // [OUT] codeRVA
        int* pdwImplFlags)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetMethodProps");
        return HResult.E_NOTIMPL;
    }    // [OUT] Impl. Flags

    HResult GetMemberRefProps(
        MdMemberRef mr,                     // [IN] given memberref
        MdToken* ptk,                   // [OUT] Put classref or classdef here.
        char* szMember,               // [OUT] buffer to fill for member's name
        uint cchMember,              // [IN] the count of char of szMember
        uint* pchMember,             // [OUT] actual count of char in member name
        IntPtr* ppvSigBlob,        // [OUT] point to meta data blob value
        uint* pbSig)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetMemberRefProps");
        return HResult.E_NOTIMPL;
    }           // [OUT] actual size of signature blob

    HResult EnumProperties(              // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef td,                     // [IN] TypeDef to scope the enumeration.
        MdProperty* rProperties,          // [OUT] Put Properties here.
        uint cMax,                   // [IN] Max properties to put.
        uint* pcProperties)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumProperties");
        return HResult.E_NOTIMPL;
    }    // [OUT] Put # put here.

    HResult EnumEvents(                  // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdTypeDef td,                     // [IN] TypeDef to scope the enumeration.
        MdEvent* rEvents,              // [OUT] Put events here.
        uint cMax,                   // [IN] Max events to put.
        out uint pcEvents)
    {
        pcEvents = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumEvents");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put # put here.

    HResult GetEventProps(                  // S_OK, S_FALSE, or error.
        MdEvent ev,                         // [IN] event token
        MdTypeDef* pClass,                  // [OUT] typedef containing the event declarion.
        char* szEvent,                      // [OUT] Event name
        uint cchEvent,                      // [IN] the count of wchar of szEvent
        uint* pchEvent,                     // [OUT] actual count of wchar for event's name
        int* pdwEventFlags,                 // [OUT] Event flags.
        MdToken* ptkEventType,              // [OUT] EventType class
        out MdMethodDef pmdAddOn,           // [OUT] AddOn method of the event
        out MdMethodDef pmdRemoveOn,        // [OUT] RemoveOn method of the event
        out MdMethodDef pmdFire,            // [OUT] Fire method of the event
        MdMethodDef* rmdOtherMethod,    // [OUT] other method of the event
        uint cMax,                          // [IN] size of rmdOtherMethod
        out uint pcOtherMethod)
    {
        pmdAddOn = default;
        pmdRemoveOn = default;
        pmdFire = default;
        pcOtherMethod = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetEventProps");
        return HResult.E_NOTIMPL;
    }            // [OUT] total number of other method of this event

    HResult EnumMethodSemantics(            // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                   // [IN|OUT] Pointer to the enum.
        MdMethodDef mb,                     // [IN] MethodDef to scope the enumeration.
        MdToken* rEventProp,            // [OUT] Put Event/Property here.
        uint cMax,                          // [IN] Max properties to put.
        out uint pcEventProp)
    {
        pcEventProp = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumMethodSemantics");
        return HResult.E_NOTIMPL;
    }              // [OUT] Put # put here.

    HResult GetMethodSemantics(             // S_OK, S_FALSE, or error.
        MdMethodDef mb,                     // [IN] method token
        MdToken tkEventProp,                // [IN] event/property token.
        out int pdwSemanticsFlags)
    {
        pdwSemanticsFlags = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetMethodSemantics");
        return HResult.E_NOTIMPL;
    }         // [OUT] the role flags for the method/propevent pair

    HResult GetClassLayout(
        MdTypeDef td,                     // [IN] give typedef
        out int pdwPackSize,           // [OUT] 1, 2, 4, 8, or 16
        COR_FIELD_OFFSET* rFieldOffset,    // [OUT] field offset array
        uint cMax,                   // [IN] size of the array
        out uint pcFieldOffset,         // [OUT] needed array size
        out uint pulClassSize)
    {
        pdwPackSize = default;
        pcFieldOffset = default;
        pulClassSize = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetClassLayout");
        return HResult.E_NOTIMPL;
    }        // [OUT] the size of the class

    HResult GetFieldMarshal(
        MdToken tk,                     // [IN] given a field's memberdef
        out nint* ppvNativeType,     // [OUT] native type of this field
        out uint pcbNativeType)
    {
        ppvNativeType = default;
        pcbNativeType = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetFieldMarshal");
        return HResult.E_NOTIMPL;
    }   // [OUT] the count of bytes of *ppvNativeType

    HResult GetRVA(
        MdToken tk,                     // Member for which to set offset
        uint* pulCodeRVA,            // The offset
        int* pdwImplFlags)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetRVA");
        return HResult.E_NOTIMPL;
    }    // the implementation flags

    HResult GetPermissionSetProps(
        MdPermission pm,                    // [IN] the permission token.
        out int pdwAction,             // [OUT] CorDeclSecurity.
        out void* ppvPermission,        // [OUT] permission blob.
        out uint pcbPermission)
    {
        pdwAction = default;
        ppvPermission = default;
        pcbPermission = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetPermissionSetProps");
        return HResult.E_NOTIMPL;
    }   // [OUT] count of bytes of pvPermission.

    HResult GetSigFromToken(
        MdSignature mdSig,                  // [IN] Signature token.
        IntPtr* ppvSig,            // [OUT] return pointer to token.
        uint* pcbSig)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetSigFromToken");
        return HResult.E_NOTIMPL;
    }          // [OUT] return size of signature.

    HResult GetModuleRefProps(
        MdModuleRef mur,                    // [IN] moduleref token.
        char* szName,                 // [OUT] buffer to fill with the moduleref name.
        uint cchName,                // [IN] size of szName in wide characters.
        out uint pchName)
    {
        pchName = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetModuleRefProps");
        return HResult.E_NOTIMPL;
    }         // [OUT] actual count of characters in the name.

    HResult EnumModuleRefs(
        HCORENUM* phEnum,                // [IN|OUT] pointer to the enum.
        MdModuleRef* rModuleRefs,          // [OUT] put modulerefs here.
        uint cmax,                   // [IN] max memberrefs to put.
        out uint pcModuleRefs)
    {
        pcModuleRefs = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumModuleRefs");
        return HResult.E_NOTIMPL;
    }    // [OUT] put # put here.

    HResult GetTypeSpecFromToken(
        MdTypeSpec typespec,                // [IN] TypeSpec token.
        IntPtr* ppvSig,            // [OUT] return pointer to TypeSpec signature
        uint* pcbSig)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetTypeSpecFromToken");
        return HResult.E_NOTIMPL;
    }          // [OUT] return size of signature.

    HResult GetNameFromToken(            // Not Recommended! May be removed!
        MdToken tk,                     // [IN] Token to get name from.  Must have a name.
        out byte* pszUtf8NamePtr)
    {
        pszUtf8NamePtr = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetNameFromToken");
        return HResult.E_NOTIMPL;
    }  // [OUT] Return pointer to UTF8 name in heap.

    HResult EnumUnresolvedMethods(       // S_OK, S_FALSE, or error.
        HCORENUM* phEnum,                // [IN|OUT] Pointer to the enum.
        MdToken* rMethods,             // [OUT] Put MemberDefs here.
        uint cMax,                   // [IN] Max MemberDefs to put.
        out uint pcTokens)
    {
        pcTokens = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumUnresolvedMethods");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put # put here.

    HResult GetUserString(
        MdString stk,                    // [IN] String token.
        char* szString,               // [OUT] Copy of string.
        uint cchString,              // [IN] Max chars of room in szString.
        uint* pchString)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetUserString");
        return HResult.E_NOTIMPL;
    }       // [OUT] How many chars in actual string.

    HResult GetPinvokeMap(
        MdToken tk,                     // [IN] FieldDef or MethodDef.
        out int pdwMappingFlags,       // [OUT] Flags used for mapping.
        char* szImportName,           // [OUT] Import name.
        uint cchImportName,          // [IN] Size of the name buffer.
        out uint pchImportName,         // [OUT] Actual number of characters stored.
        out MdModuleRef pmrImportDLL)
    {
        pdwMappingFlags = default;
        pchImportName = default;
        pmrImportDLL = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetPinvokeMap");
        return HResult.E_NOTIMPL;
    }    // [OUT] ModuleRef token for the target DLL.

    HResult EnumSignatures(
        HCORENUM* phEnum,                // [IN|OUT] pointer to the enum.
        MdSignature* rSignatures,          // [OUT] put signatures here.
        uint cmax,                   // [IN] max signatures to put.
        out uint pcSignatures)
    {
        pcSignatures = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumSignatures");
        return HResult.E_NOTIMPL;
    }    // [OUT] put # put here.

    HResult EnumTypeSpecs(
        HCORENUM* phEnum,                // [IN|OUT] pointer to the enum.
        MdTypeSpec* rTypeSpecs,           // [OUT] put TypeSpecs here.
        uint cmax,                   // [IN] max TypeSpecs to put.
        out uint pcTypeSpecs)
    {
        pcTypeSpecs = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumTypeSpecs");
        return HResult.E_NOTIMPL;
    }     // [OUT] put # put here.

    HResult EnumUserStrings(
        HCORENUM* phEnum,                // [IN/OUT] pointer to the enum.
        MdString* rStrings,             // [OUT] put Strings here.
        uint cmax,                   // [IN] max Strings to put.
        out uint pcStrings)
    {
        pcStrings = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumUserStrings");
        return HResult.E_NOTIMPL;
    }       // [OUT] put # put here.

    HResult GetParamForMethodIndex(
        MdMethodDef md,                     // [IN] Method token.
        uint ulParamSeq,             // [IN] Parameter sequence.
        out MdParamDef ppd)
    {
        ppd = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetParamForMethodIndex");
        return HResult.E_NOTIMPL;
    }             // [IN] Put Param token here.

    HResult EnumCustomAttributes(
        HCORENUM* phEnum,                // [IN, OUT] COR enumerator.
        MdToken tk,                     // [IN] Token to scope the enumeration, 0 for all.
        MdToken tkType,                 // [IN] Type of interest, 0 for all.
        MdCustomAttribute* rCustomAttributes, // [OUT] Put custom attribute tokens here.
        uint cMax,                   // [IN] Size of rCustomAttributes.
        uint* pcCustomAttributes)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.EnumCustomAttributes");
        return HResult.E_NOTIMPL;
    }  // [OUT, OPTIONAL] Put count of token values here.

    HResult GetCustomAttributeProps(
        MdCustomAttribute cv,               // [IN] CustomAttribute token.
        MdToken* ptkObj,                // [OUT, OPTIONAL] Put object token here.
        MdToken* ptkType,               // [OUT, OPTIONAL] Put AttrType token here.
        IntPtr* ppBlob,               // [OUT, OPTIONAL] Put pointer to data here.
        uint* pcbSize)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetCustomAttributeProps");
        return HResult.E_NOTIMPL;
    }         // [OUT, OPTIONAL] Put size of date here.

    HResult FindTypeRef(
        MdToken tkResolutionScope,      // [IN] ModuleRef, AssemblyRef or TypeRef.
        char* szName,                 // [IN] TypeRef Name.
        MdTypeRef* ptr)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.FindTypeRef");
        return HResult.E_NOTIMPL;
    }             // [OUT] matching TypeRef.

    HResult GetMemberProps(
        MdToken mb,                     // The member for which to get props.
        MdToken* pClass,              // Put member's class here.
        char* szMember,                 // Put member's name here.
        uint cchMember,                 // Size of szMember buffer in wide chars.
        uint* pchMember,                // Put actual size here
        int* pdwAttr,                   // Put flags here.
        IntPtr* ppvSigBlob,             // [OUT] point to the blob value of meta data
        uint* pcbSigBlob,               // [OUT] actual size of signature blob
        uint* pulCodeRVA,               // [OUT] codeRVA
        int* pdwImplFlags,              // [OUT] Impl. Flags
        int* pdwCPlusTypeFlag,          // [OUT] flag for value type. selected ELEMENT_TYPE_*
        IntPtr* ppValue,                  // [OUT] constant value
        uint* pcchValue)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetMemberProps");
        return HResult.E_NOTIMPL;
    }               // [OUT] size of constant string in chars, 0 for non-strings.

    HResult GetFieldProps(
        MdFieldDef mb,                     // The field for which to get props.
        MdTypeDef* pClass,                // Put field's class here.
        char* szField,                // Put field's name here.
        uint cchField,               // Size of szField buffer in wide chars.
        uint* pchField,              // Put actual size here
        int* pdwAttr,               // Put flags here.
        IntPtr* ppvSigBlob, uint* pcbSigBlob, int* pdwCPlusTypeFlag, IntPtr* ppValue, uint* pcchValue)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetFieldProps");
        return HResult.E_NOTIMPL;
    }       // [OUT] size of constant string in chars, 0 for non-strings.

    HResult GetPropertyProps(            // S_OK, S_FALSE, or error.
        MdProperty prop,                   // [IN] property token
        MdTypeDef* pClass,                // [OUT] typedef containing the property declarion.
        char* szProperty,             // [OUT] Property name
        uint cchProperty,            // [IN] the count of wchar of szProperty
        uint* pchProperty,           // [OUT] actual count of wchar for property name
        int* pdwPropFlags,          // [OUT] property flags.
        IntPtr* ppvSig,            // [OUT] property type. pointing to meta data internal blob
        uint* pbSig,                 // [OUT] count of bytes in *ppvSig
        int* pdwCPlusTypeFlag,      // [OUT] flag for value type. selected ELEMENT_TYPE_*
        IntPtr* ppDefaultValue,      // [OUT] constant value
        uint* pcchDefaultValue,      // [OUT] size of constant string in chars, 0 for non-strings.
        MdMethodDef* pmdSetter,             // [OUT] setter method of the property
        MdMethodDef* pmdGetter,             // [OUT] getter method of the property
        MdMethodDef* rmdOtherMethod,       // [OUT] other method of the property
        uint cMax,                   // [IN] size of rmdOtherMethod
        uint* pcOtherMethod)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetPropertyProps");
        return HResult.E_NOTIMPL;
    }   // [OUT] total number of other method of this property

    HResult GetParamProps(
        MdParamDef tk,                     // [IN]The Parameter.
        out MdMethodDef pmd,                   // [OUT] Parent Method token.
        out uint pulSequence,           // [OUT] Parameter sequence.
        char* szName,                 // [OUT] Put name here.
        uint cchName,                // [OUT] Size of name buffer.
        out uint pchName,               // [OUT] Put actual size of name here.
        out int pdwAttr,               // [OUT] Put flags here.
        out int pdwCPlusTypeFlag,      // [OUT] Flag for value type. selected ELEMENT_TYPE_*.
        out byte ppValue,             // [OUT] Constant value.
        out uint pcchValue)
    {
        pmd = default;
        pulSequence = default;
        pchName = default;
        pdwAttr = default;
        pdwCPlusTypeFlag = default;
        ppValue = default;
        pcchValue = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetParamProps");
        return HResult.E_NOTIMPL;
    }       // [OUT] size of constant string in chars, 0 for non-strings.

    HResult GetCustomAttributeByName(
        MdToken tkObj,                  // [IN] Object with Custom Attribute.
        char* szName,                 // [IN] Name of desired Custom Attribute.
        out void* ppData,               // [OUT] Put pointer to data here.
        out uint pcbData)
    {
        ppData = default;
        pcbData = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetCustomAttributeByName");
        return HResult.E_NOTIMPL;
    }         // [OUT] Put size of data here.

    bool IsValidToken(
        MdToken tk)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.IsValidToken");
        return default;
    }               // [IN] Given token.

    HResult GetNestedClassProps(
        MdTypeDef tdNestedClass,          // [IN] NestedClass token.
        MdTypeDef* ptdEnclosingClass)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetNestedClassProps");
        return HResult.E_NOTIMPL;
    } // [OUT] EnclosingClass token.

    HResult GetNativeCallConvFromSig(
        void* pvSig,                 // [IN] Pointer to signature.
        uint cbSig,                  // [IN] Count of signature bytes.
        out uint pCallConv)
    {
        pCallConv = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.GetNativeCallConvFromSig");
        return HResult.E_NOTIMPL;
    }       // [OUT] Put calling conv here (see CorPinvokemap).

    HResult IsGlobal(
        MdToken pd,                     // [IN] Type, Field, or Method token.
        out int pbGlobal)
    {
        pbGlobal = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataImport.IsGlobal");
        return HResult.E_NOTIMPL;
    }        // [OUT] Put 1 if global, 0 otherwise.
}
#endif
