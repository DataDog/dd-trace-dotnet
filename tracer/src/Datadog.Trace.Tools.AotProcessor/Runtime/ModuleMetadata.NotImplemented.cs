using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Tools.AotProcessor.Interfaces;
using Mono.Cecil;

namespace Datadog.Trace.Tools.AotProcessor.Runtime
{
    internal partial class ModuleMetadata
    {
        #region IMetadataImport2

        public unsafe HResult EnumGenericParams(HCORENUM* phEnum, MdToken tk, out MdGenericParam* rGenericParams, uint cMax, out uint pcGenericParams)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetGenericParamProps(MdGenericParam gp, out uint pulParamSeq, out int pdwParamFlags, MdToken* ptOwner, out int reserved, char* wzname, uint cchName, out uint pchName)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumGenericParamConstraints(HCORENUM* phEnum, MdGenericParam tk, out MdGenericParamConstraint* rGenericParamConstraints, uint cMax, out uint pcGenericParamConstraints)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetGenericParamConstraintProps(MdGenericParamConstraint gpc, MdGenericParam* ptGenericParam, out MdToken ptkConstraintType)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult GetPEKind(out int pdwPEKind, out int pdwMAchine)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetVersionString(char* pwzBuf, int ccBufSize, out int pccBufSize)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumMethodSpecs(HCORENUM* phEnum, MdToken tk, out MdMethodSpec* rMethodSpecs, uint cMax, out uint pcMethodSpecs)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumTypeDefs(HCORENUM* phEnum, MdTypeDef* rTypeDefs, uint cMax, uint* pcTypeDefs)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumInterfaceImpls(HCORENUM* phEnum, MdTypeDef td, MdInterfaceImpl* rImpls, uint cMax, uint* pcImpls)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetScopeProps(char* szName, uint cchName, out uint pchName, out Guid pmvid)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult GetModuleFromScope(out MdModule pmd)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult GetInterfaceImplProps(MdInterfaceImpl iiImpl, out MdTypeDef pClass, out MdToken ptkIface)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult ResolveTypeRef(MdTypeRef tr, in Guid riid, void** ppIScope, out MdTypeDef ptd)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumMembers(HCORENUM* phEnum, MdTypeDef cl, MdToken* rMembers, uint cMax, out uint pcTokens)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumMembersWithName(HCORENUM* phEnum, MdTypeDef cl, char* szName, MdToken* rMembers, uint cMax, out uint pcTokens)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumMethods(HCORENUM* phEnum, MdTypeDef cl, MdMethodDef* rMethods, uint cMax, out uint pcTokens)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumFields(HCORENUM* phEnum, MdTypeDef cl, MdFieldDef* rFields, uint cMax, out uint pcTokens)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumFieldsWithName(HCORENUM* phEnum, MdTypeDef cl, char* szName, MdFieldDef* rFields, uint cMax, out uint pcTokens)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumParams(HCORENUM* phEnum, MdMethodDef mb, MdParamDef* rParams, uint cMax, out uint pcTokens)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumMethodImpls(HCORENUM* phEnum, MdTypeDef td, MdToken* rMethodBody, MdToken* rMethodDecl, uint cMax, out uint pcTokens)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumPermissionSets(HCORENUM* phEnum, MdToken tk, int dwActions, MdPermission* rPermission, uint cMax, out uint pcTokens)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult FindMember(MdTypeDef td, char* szName, nint* pvSigBlob, uint cbSigBlob, out MdToken pmb)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult FindField(MdTypeDef td, char* szName, nint* pvSigBlob, uint cbSigBlob, out MdFieldDef pmb)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult FindMemberRef(MdTypeRef td, char* szName, nint* pvSigBlob, uint cbSigBlob, out MdMemberRef pmr)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumEvents(HCORENUM* phEnum, MdTypeDef td, MdEvent* rEvents, uint cMax, out uint pcEvents)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetEventProps(MdEvent ev, MdTypeDef* pClass, char* szEvent, uint cchEvent, uint* pchEvent, int* pdwEventFlags, MdToken* ptkEventType, out MdMethodDef pmdAddOn, out MdMethodDef pmdRemoveOn, out MdMethodDef pmdFire, out MdMethodDef* rmdOtherMethod, uint cMax, out uint pcOtherMethod)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumMethodSemantics(HCORENUM* phEnum, MdMethodDef mb, out MdToken* rEventProp, uint cMax, out uint pcEventProp)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult GetMethodSemantics(MdMethodDef mb, MdToken tkEventProp, out int pdwSemanticsFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetClassLayout(MdTypeDef td, out int pdwPackSize, COR_FIELD_OFFSET* rFieldOffset, uint cMax, out uint pcFieldOffset, out uint pulClassSize)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetFieldMarshal(MdToken tk, out nint* ppvNativeType, out uint pcbNativeType)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetRVA(MdToken tk, uint* pulCodeRVA, int* pdwImplFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetPermissionSetProps(MdPermission pm, out int pdwAction, out void* ppvPermission, out uint pcbPermission)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetSigFromToken(MdSignature mdSig, out nint* ppvSig, out uint pcbSig)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetModuleRefProps(MdModuleRef mur, char* szName, uint cchName, out uint pchName)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumModuleRefs(HCORENUM* phEnum, MdModuleRef* rModuleRefs, uint cmax, out uint pcModuleRefs)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetTypeSpecFromToken(MdTypeSpec typespec, out nint* ppvSig, out uint pcbSig)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetNameFromToken(MdToken tk, out byte* pszUtf8NamePtr)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumUnresolvedMethods(HCORENUM* phEnum, MdToken* rMethods, uint cMax, out uint pcTokens)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetPinvokeMap(MdToken tk, out int pdwMappingFlags, char* szImportName, uint cchImportName, out uint pchImportName, out MdModuleRef pmrImportDLL)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumSignatures(HCORENUM* phEnum, MdSignature* rSignatures, uint cmax, out uint pcSignatures)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumTypeSpecs(HCORENUM* phEnum, MdTypeSpec* rTypeSpecs, uint cmax, out uint pcTypeSpecs)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumUserStrings(HCORENUM* phEnum, MdString* rStrings, uint cmax, out uint pcStrings)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult GetParamForMethodIndex(MdMethodDef md, uint ulParamSeq, out MdParamDef ppd)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetFieldProps(MdFieldDef mb, MdTypeDef* pClass, char* szField, uint cchField, uint* pchField, int* pdwAttr, out nint* ppvSigBlob, out uint pcbSigBlob, out int pdwCPlusTypeFlag, out byte ppValue, out uint pcchValue)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetParamProps(MdParamDef tk, out MdMethodDef pmd, out uint pulSequence, char* szName, uint cchName, out uint pchName, out int pdwAttr, out int pdwCPlusTypeFlag, out byte ppValue, out uint pcchValue)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetCustomAttributeByName(MdToken tkObj, char* szName, out void* ppData, out uint pcbData)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public bool IsValidToken(MdToken tk)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetNativeCallConvFromSig(void* pvSig, uint cbSig, out uint pCallConv)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult IsGlobal(MdToken pd, out int pbGlobal)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        #endregion

        #region IMetadataEmit2
        public unsafe HResult DefineMethodSpec(MdToken tkParent, byte* pvSigBlob, int cbSigBlob, MdMethodSpec* pmi)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult GetDeltaSaveSize(CorSaveSize fSave, out int pdwSaveSize)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult SaveDelta(char* szFile, int dwSaveFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SaveDeltaToStream(IntPtr pIStream, int dwSaveFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SaveDeltaToMemory(IntPtr pbData, int cbData)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult DefineGenericParam(MdToken tk, int ulParamSeq, int dwParamFlags, char* szname, int reserved, MdToken* rtkConstraints, out MdGenericParam pgp)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult SetGenericParamProps(MdGenericParam gp, int dwParamFlags, char* szName, int reserved, MdToken* rtkConstraints)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult ResetENCLog()
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult SetModuleProps(char* szName)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult Save(char* szFile, int dwSaveFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SaveToStream(IntPtr pIStream, int dwSaveFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult GetSaveSize(CorSaveSize fSave, out int pdwSaveSize)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult DefineNestedType(char* szTypeDef, int dwTypeDefFlags, MdToken tkExtends, MdToken* rtkImplements, MdTypeDef tdEncloser, out MdTypeDef ptd)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SetHandler(IntPtr pUnk)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult DefineMethodImpl(MdTypeDef td, MdToken tkBody, MdToken tkDecl)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult DefineImportType(IntPtr pAssemImport, byte* pbHashValue, int cbHashValue, IntPtr pImport, MdTypeDef tdImport, IntPtr pAssemEmit, out MdTypeRef ptr)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult DefineEvent(MdTypeDef td, char* szEvent, int dwEventFlags, MdToken tkEventType, MdMethodDef MdAddOn, MdMethodDef MdRemoveOn, MdMethodDef MdFire, MdMethodDef* rmdOtherMethods, MdEvent* pmdEvent)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult SetClassLayout(MdTypeDef td, int dwPackSize, COR_FIELD_OFFSET* rFieldOffsets, int ulClassSize)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult DeleteClassLayout(MdTypeDef td)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult SetFieldMarshal(MdToken tk, byte* pvNativeType, int cbNativeType)
        {
            throw new NotImplementedException();
        }

        public HResult DeleteFieldMarshal(MdToken tk)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult DefinePermissionSet(MdToken tk, int dwAction, IntPtr pvPermission, int cbPermission, out MdPermission ppm)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SetRVA(MdMethodDef Md, int ulRVA)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SetParent(MdMemberRef mr, MdToken tk)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetTokenFromTypeSpec(byte* pvSig, int cbSig, out MdTypeSpec ptypespec)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SaveToMemory(IntPtr pbData, int cbData)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult DeleteToken(MdToken tkObj)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SetMethodProps(MdMethodDef Md, int dwMethodFlags, int ulCodeRVA, int dwImplFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult SetTypeDefProps(MdTypeDef td, int dwTypeDefFlags, MdToken tkExtends, MdToken* rtkImplements)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult SetEventProps(MdEvent ev, int dwEventFlags, MdToken tkEventType, MdMethodDef MdAddOn, MdMethodDef MdRemoveOn, MdMethodDef MdFire, MdMethodDef* rmdOtherMethods)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SetPermissionSetProps(MdToken tk, int dwAction, IntPtr pvPermission, int cbPermission, out MdPermission ppm)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult SetPinvokeMap(MdToken tk, int dwMappingFlags, char* szImportName, MdModuleRef mrImportDLL)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult DeletePinvokeMap(MdToken tk)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult DefineCustomAttribute(MdToken tkOwner, MdToken tkCtor, IntPtr pCustomAttribute, int cbCustomAttribute, MdCustomAttribute* pcv)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SetCustomAttributeValue(MdCustomAttribute pcv, IntPtr pCustomAttribute, int cbCustomAttribute)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult DefineProperty(MdTypeDef td, char* szProperty, int dwPropFlags, byte* pvSig, int cbSig, int dwCPlusTypeFlag, IntPtr pValue, int cchValue, MdMethodDef MdSetter, MdMethodDef MdGetter, MdMethodDef* rmdOtherMethods, out MdProperty pmdProp)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult DefineParam(MdMethodDef Md, int ulParamSeq, char* szName, int dwParamFlags, int dwCPlusTypeFlag, IntPtr pValue, int cchValue, MdParamDef* ppd)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SetFieldProps(MdFieldDef fd, int dwFieldFlags, int dwCPlusTypeFlag, IntPtr pValue, int cchValue)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult SetPropertyProps(MdProperty pr, int dwPropFlags, int dwCPlusTypeFlag, IntPtr pValue, int cchValue, MdMethodDef MdSetter, MdMethodDef MdGetter, MdMethodDef* rmdOtherMethods)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult SetParamProps(MdParamDef pd, char* szName, int dwParamFlags, int dwCPlusTypeFlag, IntPtr pValue, int cchValue)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult DefineSecurityAttributeSet(MdToken tkObj, COR_SECATTR* rSecAttrs, int cSecAttrs, out int pulErrorAttr)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult ApplyEditAndContinue(IntPtr pImport)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult TranslateSigWithScope(IntPtr pAssemImport, IntPtr pbHashValue, int cbHashValue, IntPtr import, byte* pbSigBlob, int cbSigBlob, IntPtr pAssemEmit, IntPtr emit, byte* pvTranslatedSig, int cbTranslatedSigMax, out int pcbTranslatedSig)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SetFieldRVA(MdFieldDef fd, int ulRVA)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult Merge(IntPtr pImport, IntPtr pHostMapToken, IntPtr pHandler)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult MergeEnd()
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        #endregion

        #region IMetadataAssemblyImport

        public unsafe HResult GetFileProps(MdFile mdf, char* szName, uint cchName, out uint pchName, byte* ppbHashValue, out int pcbHashValue, out int pdwFileFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetExportedTypeProps(MdExportedType mdct, char* szName, uint cchName, out uint pchName, MdToken* ptkImplementation, MdTypeDef* ptkTypeDef, out int pdwExportedTypeFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult GetManifestResourceProps(MdManifestResource mdmr, char* szName, uint cchName, out uint pchName, MdToken* ptkImplementation, out int pdwOffset, out int pdwResourceFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumFiles(HCORENUM* phEnum, MdFile* rFiles, uint cMax, out uint pcTokens)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumExportedTypes(HCORENUM* phEnum, MdExportedType* rExportedTypes, uint cMax, out uint pcTokens)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult EnumManifestResources(HCORENUM* phEnum, MdManifestResource* rManifestResources, uint cMax, out uint pcTokens)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult FindExportedTypeByName(char* szName, MdToken mdtExportedType, MdExportedType* ptkExportedType)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult FindManifestResourceByName(char* szName, MdManifestResource* ptkManifestResource)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult FindAssembliesByName(char* szAppBase, char* szPrivateBin, char* szAssemblyName, IntPtr* ppIUnk, uint cMax, out uint pcAssemblies)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        #endregion

        #region IMetadataAssemblyEmit

        public unsafe HResult DefineAssembly(IntPtr pbPublicKey, int cbPublicKey, int ulHashAlgId, char* szName, ASSEMBLYMETADATA* pMetaData, int dwAssemblyFlags, out MdAssembly pma)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult DefineFile(char* szName, IntPtr pbHashValue, int cbHashValue, int dwFileFlags, out MdFile pmdf)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult DefineExportedType(char* szName, MdToken tkImplementation, MdTypeDef tkTypeDef, int dwExportedTypeFlags, MdExportedType* pmdct)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult DefineManifestResource(char* szName, MdToken tkImplementation, int dwOffset, int dwResourceFlags, MdManifestResource* pmdmr)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult SetAssemblyProps(MdAssembly pma, IntPtr pbPublicKey, int cbPublicKey, int ulHashAlgId, char* szName, ASSEMBLYMETADATA* pMetaData, int dwAssemblyFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public unsafe HResult SetAssemblyRefProps(MdAssemblyRef ar, IntPtr pbPublicKeyOrToken, int cbPublicKeyOrToken, char* szName, ASSEMBLYMETADATA* pMetaData, IntPtr pbHashValue, int cbHashValue, int dwAssemblyRefFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SetFileProps(MdFile file, IntPtr pbHashValue, int cbHashValue, int dwFileFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SetExportedTypeProps(MdExportedType ct, MdToken tkImplementation, MdTypeDef tkTypeDef, int dwExportedTypeFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        public HResult SetManifestResourceProps(MdManifestResource mr, MdToken tkImplementation, int dwOffset, int dwResourceFlags)
        {
            System.Diagnostics.Debugger.Break();
            throw new NotImplementedException();
        }

        #endregion
    }
}
