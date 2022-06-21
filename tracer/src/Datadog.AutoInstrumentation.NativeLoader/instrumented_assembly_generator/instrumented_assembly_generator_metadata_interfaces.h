#pragma once
#include "../cor_profiler.h"
#include "../util.h"

namespace instrumented_assembly_generator
{
class MetadataInterfaces : public IMetaDataError,
                                      IMapToken,
                                      IMetaDataEmit2,
                                      IMetaDataImport2,
                                      IMetaDataFilter,
                                      IHostFilter,
                                      IMetaDataAssemblyEmit,
                                      IMetaDataAssemblyImport,
                                      IMetaDataValidate,
                                      IMetaDataDispenserEx,
                                      ICeeGen,
                                      IMetaDataTables2,
                                      IMetaDataInfo
{
private:
    std::atomic<int> m_refCount;
    ComPtr<IUnknown> m_metadataInterfaces;

public:
    MetadataInterfaces(const ComPtr<IUnknown>& metadataInterfaces);
    ~MetadataInterfaces();

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override;
    ULONG STDMETHODCALLTYPE AddRef(void) override;
    ULONG STDMETHODCALLTYPE Release(void) override;
    void WriteMetadataChange(const mdToken* pToken, const shared::WSTRING& metadataName) const;
    HRESULT STDMETHODCALLTYPE OnError(HRESULT hrError, mdToken token) override;
    HRESULT STDMETHODCALLTYPE Map(mdToken tkImp, mdToken tkEmit) override;
    HRESULT STDMETHODCALLTYPE DefineScope(const IID& rclsid, DWORD dwCreateFlags, const IID& riid,
                                          IUnknown** ppIUnk) override;
    HRESULT STDMETHODCALLTYPE OpenScope(LPCWSTR szScope, DWORD dwOpenFlags, const IID& riid,
                                        IUnknown** ppIUnk) override;
    HRESULT STDMETHODCALLTYPE OpenScopeOnMemory(LPCVOID pData, ULONG cbData, DWORD dwOpenFlags, const IID& riid,
                                                IUnknown** ppIUnk) override;
    HRESULT STDMETHODCALLTYPE SetModuleProps(LPCWSTR szName) override;
    HRESULT STDMETHODCALLTYPE Save(LPCWSTR szFile, DWORD dwSaveFlags) override;
    HRESULT STDMETHODCALLTYPE SaveToStream(IStream* pIStream, DWORD dwSaveFlags) override;
    HRESULT STDMETHODCALLTYPE GetSaveSize(CorSaveSize fSave, DWORD* pdwSaveSize) override;
    HRESULT STDMETHODCALLTYPE DefineTypeDef(LPCWSTR szTypeDef, DWORD dwTypeDefFlags, mdToken tkExtends,
                                            mdToken rtkImplements[], mdTypeDef* ptd) override;
    HRESULT STDMETHODCALLTYPE DefineNestedType(LPCWSTR szTypeDef, DWORD dwTypeDefFlags, mdToken tkExtends,
                                               mdToken rtkImplements[], mdTypeDef tdEncloser, mdTypeDef* ptd) override;
    HRESULT STDMETHODCALLTYPE SetHandler(IUnknown* pUnk) override;
    HRESULT STDMETHODCALLTYPE DefineMethod(mdTypeDef td, LPCWSTR szName, DWORD dwMethodFlags, PCCOR_SIGNATURE pvSigBlob,
                                           ULONG cbSigBlob, ULONG ulCodeRVA, DWORD dwImplFlags,
                                           mdMethodDef* pmd) override;
    HRESULT STDMETHODCALLTYPE DefineMethodImpl(mdTypeDef td, mdToken tkBody, mdToken tkDecl) override;
    HRESULT STDMETHODCALLTYPE DefineTypeRefByName(mdToken tkResolutionScope, LPCWSTR szName, mdTypeRef* ptr) override;
    HRESULT STDMETHODCALLTYPE DefineImportType(IMetaDataAssemblyImport* pAssemImport, const void* pbHashValue,
                                               ULONG cbHashValue, IMetaDataImport* pImport, mdTypeDef tdImport,
                                               IMetaDataAssemblyEmit* pAssemEmit, mdTypeRef* ptr) override;
    HRESULT STDMETHODCALLTYPE DefineMemberRef(mdToken tkImport, LPCWSTR szName, PCCOR_SIGNATURE pvSigBlob,
                                              ULONG cbSigBlob, mdMemberRef* pmr) override;
    HRESULT STDMETHODCALLTYPE DefineImportMember(IMetaDataAssemblyImport* pAssemImport, const void* pbHashValue,
                                                 ULONG cbHashValue, IMetaDataImport* pImport, mdToken mbMember,
                                                 IMetaDataAssemblyEmit* pAssemEmit, mdToken tkParent,
                                                 mdMemberRef* pmr) override;
    HRESULT STDMETHODCALLTYPE DefineEvent(mdTypeDef td, LPCWSTR szEvent, DWORD dwEventFlags, mdToken tkEventType,
                                          mdMethodDef mdAddOn, mdMethodDef mdRemoveOn, mdMethodDef mdFire,
                                          mdMethodDef rmdOtherMethods[], mdEvent* pmdEvent) override;
    HRESULT STDMETHODCALLTYPE SetClassLayout(mdTypeDef td, DWORD dwPackSize, COR_FIELD_OFFSET rFieldOffsets[],
                                             ULONG ulClassSize) override;
    HRESULT STDMETHODCALLTYPE DeleteClassLayout(mdTypeDef td) override;
    HRESULT STDMETHODCALLTYPE SetFieldMarshal(mdToken tk, PCCOR_SIGNATURE pvNativeType, ULONG cbNativeType) override;
    HRESULT STDMETHODCALLTYPE DeleteFieldMarshal(mdToken tk) override;
    HRESULT STDMETHODCALLTYPE DefinePermissionSet(mdToken tk, DWORD dwAction, void const* pvPermission,
                                                  ULONG cbPermission, mdPermission* ppm) override;
    HRESULT STDMETHODCALLTYPE SetRVA(mdMethodDef md, ULONG ulRVA) override;
    HRESULT STDMETHODCALLTYPE GetTokenFromSig(PCCOR_SIGNATURE pvSig, ULONG cbSig, mdSignature* pmsig) override;
    HRESULT STDMETHODCALLTYPE DefineModuleRef(LPCWSTR szName, mdModuleRef* pmur) override;
    HRESULT STDMETHODCALLTYPE SetParent(mdMemberRef mr, mdToken tk) override;
    HRESULT STDMETHODCALLTYPE GetTokenFromTypeSpec(PCCOR_SIGNATURE pvSig, ULONG cbSig, mdTypeSpec* ptypespec) override;
    HRESULT STDMETHODCALLTYPE SaveToMemory(void* pbData, ULONG cbData) override;
    HRESULT STDMETHODCALLTYPE DefineUserString(LPCWSTR szString, ULONG cchString, mdString* pstk) override;
    HRESULT STDMETHODCALLTYPE DeleteToken(mdToken tkObj) override;
    HRESULT STDMETHODCALLTYPE SetMethodProps(mdMethodDef md, DWORD dwMethodFlags, ULONG ulCodeRVA,
                                             DWORD dwImplFlags) override;
    HRESULT STDMETHODCALLTYPE SetTypeDefProps(mdTypeDef td, DWORD dwTypeDefFlags, mdToken tkExtends,
                                              mdToken rtkImplements[]) override;
    HRESULT STDMETHODCALLTYPE SetEventProps(mdEvent ev, DWORD dwEventFlags, mdToken tkEventType, mdMethodDef mdAddOn,
                                            mdMethodDef mdRemoveOn, mdMethodDef mdFire,
                                            mdMethodDef rmdOtherMethods[]) override;
    HRESULT STDMETHODCALLTYPE SetPermissionSetProps(mdToken tk, DWORD dwAction, void const* pvPermission,
                                                    ULONG cbPermission, mdPermission* ppm) override;
    HRESULT STDMETHODCALLTYPE DefinePinvokeMap(mdToken tk, DWORD dwMappingFlags, LPCWSTR szImportName,
                                               mdModuleRef mrImportDLL) override;
    HRESULT STDMETHODCALLTYPE SetPinvokeMap(mdToken tk, DWORD dwMappingFlags, LPCWSTR szImportName,
                                            mdModuleRef mrImportDLL) override;
    HRESULT STDMETHODCALLTYPE DeletePinvokeMap(mdToken tk) override;
    HRESULT STDMETHODCALLTYPE DefineCustomAttribute(mdToken tkOwner, mdToken tkCtor, void const* pCustomAttribute,
                                                    ULONG cbCustomAttribute, mdCustomAttribute* pcv) override;
    HRESULT STDMETHODCALLTYPE SetCustomAttributeValue(mdCustomAttribute pcv, void const* pCustomAttribute,
                                                      ULONG cbCustomAttribute) override;
    HRESULT STDMETHODCALLTYPE DefineField(mdTypeDef td, LPCWSTR szName, DWORD dwFieldFlags, PCCOR_SIGNATURE pvSigBlob,
                                          ULONG cbSigBlob, DWORD dwCPlusTypeFlag, void const* pValue, ULONG cchValue,
                                          mdFieldDef* pmd) override;
    HRESULT STDMETHODCALLTYPE DefineProperty(mdTypeDef td, LPCWSTR szProperty, DWORD dwPropFlags, PCCOR_SIGNATURE pvSig,
                                             ULONG cbSig, DWORD dwCPlusTypeFlag, void const* pValue, ULONG cchValue,
                                             mdMethodDef mdSetter, mdMethodDef mdGetter, mdMethodDef rmdOtherMethods[],
                                             mdProperty* pmdProp) override;
    HRESULT STDMETHODCALLTYPE DefineParam(mdMethodDef md, ULONG ulParamSeq, LPCWSTR szName, DWORD dwParamFlags,
                                          DWORD dwCPlusTypeFlag, void const* pValue, ULONG cchValue,
                                          mdParamDef* ppd) override;
    HRESULT STDMETHODCALLTYPE SetFieldProps(mdFieldDef fd, DWORD dwFieldFlags, DWORD dwCPlusTypeFlag,
                                            void const* pValue, ULONG cchValue) override;
    HRESULT STDMETHODCALLTYPE SetPropertyProps(mdProperty pr, DWORD dwPropFlags, DWORD dwCPlusTypeFlag,
                                               void const* pValue, ULONG cchValue, mdMethodDef mdSetter,
                                               mdMethodDef mdGetter, mdMethodDef rmdOtherMethods[]) override;
    HRESULT STDMETHODCALLTYPE SetParamProps(mdParamDef pd, LPCWSTR szName, DWORD dwParamFlags, DWORD dwCPlusTypeFlag,
                                            void const* pValue, ULONG cchValue) override;
    HRESULT STDMETHODCALLTYPE DefineSecurityAttributeSet(mdToken tkObj, COR_SECATTR rSecAttrs[], ULONG cSecAttrs,
                                                         ULONG* pulErrorAttr) override;
    HRESULT STDMETHODCALLTYPE ApplyEditAndContinue(IUnknown* pImport) override;
    HRESULT STDMETHODCALLTYPE TranslateSigWithScope(IMetaDataAssemblyImport* pAssemImport, const void* pbHashValue,
                                                    ULONG cbHashValue, IMetaDataImport* import,
                                                    PCCOR_SIGNATURE pbSigBlob, ULONG cbSigBlob,
                                                    IMetaDataAssemblyEmit* pAssemEmit, IMetaDataEmit* emit,
                                                    PCOR_SIGNATURE pvTranslatedSig, ULONG cbTranslatedSigMax,
                                                    ULONG* pcbTranslatedSig) override;
    HRESULT STDMETHODCALLTYPE SetMethodImplFlags(mdMethodDef md, DWORD dwImplFlags) override;
    HRESULT STDMETHODCALLTYPE SetFieldRVA(mdFieldDef fd, ULONG ulRVA) override;
    HRESULT STDMETHODCALLTYPE Merge(IMetaDataImport* pImport, IMapToken* pHostMapToken, IUnknown* pHandler) override;
    HRESULT STDMETHODCALLTYPE MergeEnd() override;
    HRESULT STDMETHODCALLTYPE DefineMethodSpec(mdToken tkParent, PCCOR_SIGNATURE pvSigBlob, ULONG cbSigBlob,
                                               mdMethodSpec* pmi) override;
    HRESULT STDMETHODCALLTYPE GetDeltaSaveSize(CorSaveSize fSave, DWORD* pdwSaveSize) override;
    HRESULT STDMETHODCALLTYPE SaveDelta(LPCWSTR szFile, DWORD dwSaveFlags) override;
    HRESULT STDMETHODCALLTYPE SaveDeltaToStream(IStream* pIStream, DWORD dwSaveFlags) override;
    HRESULT STDMETHODCALLTYPE SaveDeltaToMemory(void* pbData, ULONG cbData) override;
    HRESULT STDMETHODCALLTYPE DefineGenericParam(mdToken tk, ULONG ulParamSeq, DWORD dwParamFlags, LPCWSTR szname,
                                                 DWORD reserved, mdToken rtkConstraints[],
                                                 mdGenericParam* pgp) override;
    HRESULT STDMETHODCALLTYPE SetGenericParamProps(mdGenericParam gp, DWORD dwParamFlags, LPCWSTR szName,
                                                   DWORD reserved, mdToken rtkConstraints[]) override;
    HRESULT STDMETHODCALLTYPE ResetENCLog() override;
    void STDMETHODCALLTYPE CloseEnum(HCORENUM hEnum) override;
    HRESULT STDMETHODCALLTYPE CountEnum(HCORENUM hEnum, ULONG* pulCount) override;
    HRESULT STDMETHODCALLTYPE ResetEnum(HCORENUM hEnum, ULONG ulPos) override;
    HRESULT STDMETHODCALLTYPE EnumTypeDefs(HCORENUM* phEnum, mdTypeDef rTypeDefs[], ULONG cMax,
                                           ULONG* pcTypeDefs) override;
    HRESULT STDMETHODCALLTYPE EnumInterfaceImpls(HCORENUM* phEnum, mdTypeDef td, mdInterfaceImpl rImpls[], ULONG cMax,
                                                 ULONG* pcImpls) override;
    HRESULT STDMETHODCALLTYPE EnumTypeRefs(HCORENUM* phEnum, mdTypeRef rTypeRefs[], ULONG cMax,
                                           ULONG* pcTypeRefs) override;
    HRESULT STDMETHODCALLTYPE FindTypeDefByName(LPCWSTR szTypeDef, mdToken tkEnclosingClass, mdTypeDef* ptd) override;
    HRESULT STDMETHODCALLTYPE GetScopeProps(LPWSTR szName, ULONG cchName, ULONG* pchName, GUID* pmvid) override;
    HRESULT STDMETHODCALLTYPE GetModuleFromScope(mdModule* pmd) override;
    HRESULT STDMETHODCALLTYPE GetTypeDefProps(mdTypeDef td, LPWSTR szTypeDef, ULONG cchTypeDef, ULONG* pchTypeDef,
                                              DWORD* pdwTypeDefFlags, mdToken* ptkExtends) override;
    HRESULT STDMETHODCALLTYPE GetInterfaceImplProps(mdInterfaceImpl iiImpl, mdTypeDef* pClass,
                                                    mdToken* ptkIface) override;
    HRESULT STDMETHODCALLTYPE GetTypeRefProps(mdTypeRef tr, mdToken* ptkResolutionScope, LPWSTR szName, ULONG cchName,
                                              ULONG* pchName) override;
    HRESULT STDMETHODCALLTYPE ResolveTypeRef(mdTypeRef tr, const IID& riid, IUnknown** ppIScope,
                                             mdTypeDef* ptd) override;
    HRESULT STDMETHODCALLTYPE EnumMembers(HCORENUM* phEnum, mdTypeDef cl, mdToken rMembers[], ULONG cMax,
                                          ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE EnumMembersWithName(HCORENUM* phEnum, mdTypeDef cl, LPCWSTR szName, mdToken rMembers[],
                                                  ULONG cMax, ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE EnumMethods(HCORENUM* phEnum, mdTypeDef cl, mdMethodDef rMethods[], ULONG cMax,
                                          ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE EnumMethodsWithName(HCORENUM* phEnum, mdTypeDef cl, LPCWSTR szName,
                                                  mdMethodDef rMethods[], ULONG cMax, ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE EnumFields(HCORENUM* phEnum, mdTypeDef cl, mdFieldDef rFields[], ULONG cMax,
                                         ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE EnumFieldsWithName(HCORENUM* phEnum, mdTypeDef cl, LPCWSTR szName, mdFieldDef rFields[],
                                                 ULONG cMax, ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE EnumParams(HCORENUM* phEnum, mdMethodDef mb, mdParamDef rParams[], ULONG cMax,
                                         ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE EnumMemberRefs(HCORENUM* phEnum, mdToken tkParent, mdMemberRef rMemberRefs[], ULONG cMax,
                                             ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE EnumMethodImpls(HCORENUM* phEnum, mdTypeDef td, mdToken rMethodBody[],
                                              mdToken rMethodDecl[], ULONG cMax, ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE EnumPermissionSets(HCORENUM* phEnum, mdToken tk, DWORD dwActions,
                                                 mdPermission rPermission[], ULONG cMax, ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE FindMember(mdTypeDef td, LPCWSTR szName, PCCOR_SIGNATURE pvSigBlob, ULONG cbSigBlob,
                                         mdToken* pmb) override;
    HRESULT STDMETHODCALLTYPE FindMethod(mdTypeDef td, LPCWSTR szName, PCCOR_SIGNATURE pvSigBlob, ULONG cbSigBlob,
                                         mdMethodDef* pmb) override;
    HRESULT STDMETHODCALLTYPE FindField(mdTypeDef td, LPCWSTR szName, PCCOR_SIGNATURE pvSigBlob, ULONG cbSigBlob,
                                        mdFieldDef* pmb) override;
    HRESULT STDMETHODCALLTYPE FindMemberRef(mdTypeRef td, LPCWSTR szName, PCCOR_SIGNATURE pvSigBlob, ULONG cbSigBlob,
                                            mdMemberRef* pmr) override;
    HRESULT STDMETHODCALLTYPE GetMethodProps(mdMethodDef mb, mdTypeDef* pClass, LPWSTR szMethod, ULONG cchMethod,
                                             ULONG* pchMethod, DWORD* pdwAttr, PCCOR_SIGNATURE* ppvSigBlob,
                                             ULONG* pcbSigBlob, ULONG* pulCodeRVA, DWORD* pdwImplFlags) override;
    HRESULT STDMETHODCALLTYPE GetMemberRefProps(mdMemberRef mr, mdToken* ptk, LPWSTR szMember, ULONG cchMember,
                                                ULONG* pchMember, PCCOR_SIGNATURE* ppvSigBlob, ULONG* pbSig) override;
    HRESULT STDMETHODCALLTYPE EnumProperties(HCORENUM* phEnum, mdTypeDef td, mdProperty rProperties[], ULONG cMax,
                                             ULONG* pcProperties) override;
    HRESULT STDMETHODCALLTYPE EnumEvents(HCORENUM* phEnum, mdTypeDef td, mdEvent rEvents[], ULONG cMax,
                                         ULONG* pcEvents) override;
    HRESULT STDMETHODCALLTYPE GetEventProps(mdEvent ev, mdTypeDef* pClass, LPCWSTR szEvent, ULONG cchEvent,
                                            ULONG* pchEvent, DWORD* pdwEventFlags, mdToken* ptkEventType,
                                            mdMethodDef* pmdAddOn, mdMethodDef* pmdRemoveOn, mdMethodDef* pmdFire,
                                            mdMethodDef rmdOtherMethod[], ULONG cMax, ULONG* pcOtherMethod) override;
    HRESULT STDMETHODCALLTYPE EnumMethodSemantics(HCORENUM* phEnum, mdMethodDef mb, mdToken rEventProp[], ULONG cMax,
                                                  ULONG* pcEventProp) override;
    HRESULT STDMETHODCALLTYPE GetMethodSemantics(mdMethodDef mb, mdToken tkEventProp,
                                                 DWORD* pdwSemanticsFlags) override;
    HRESULT STDMETHODCALLTYPE GetClassLayout(mdTypeDef td, DWORD* pdwPackSize, COR_FIELD_OFFSET rFieldOffset[],
                                             ULONG cMax, ULONG* pcFieldOffset, ULONG* pulClassSize) override;
    HRESULT STDMETHODCALLTYPE GetFieldMarshal(mdToken tk, PCCOR_SIGNATURE* ppvNativeType,
                                              ULONG* pcbNativeType) override;
    HRESULT STDMETHODCALLTYPE GetRVA(mdToken tk, ULONG* pulCodeRVA, DWORD* pdwImplFlags) override;
    HRESULT STDMETHODCALLTYPE GetPermissionSetProps(mdPermission pm, DWORD* pdwAction, void const** ppvPermission,
                                                    ULONG* pcbPermission) override;
    HRESULT STDMETHODCALLTYPE GetSigFromToken(mdSignature mdSig, PCCOR_SIGNATURE* ppvSig, ULONG* pcbSig) override;
    HRESULT STDMETHODCALLTYPE GetModuleRefProps(mdModuleRef mur, LPWSTR szName, ULONG cchName, ULONG* pchName) override;
    HRESULT STDMETHODCALLTYPE EnumModuleRefs(HCORENUM* phEnum, mdModuleRef rModuleRefs[], ULONG cmax,
                                             ULONG* pcModuleRefs) override;
    HRESULT STDMETHODCALLTYPE GetTypeSpecFromToken(mdTypeSpec typespec, PCCOR_SIGNATURE* ppvSig,
                                                   ULONG* pcbSig) override;
    HRESULT STDMETHODCALLTYPE GetNameFromToken(mdToken tk, MDUTF8CSTR* pszUtf8NamePtr) override;
    HRESULT STDMETHODCALLTYPE EnumUnresolvedMethods(HCORENUM* phEnum, mdToken rMethods[], ULONG cMax,
                                                    ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE GetUserString(mdString stk, LPWSTR szString, ULONG cchString, ULONG* pchString) override;
    HRESULT STDMETHODCALLTYPE GetPinvokeMap(mdToken tk, DWORD* pdwMappingFlags, LPWSTR szImportName,
                                            ULONG cchImportName, ULONG* pchImportName,
                                            mdModuleRef* pmrImportDLL) override;
    HRESULT STDMETHODCALLTYPE EnumSignatures(HCORENUM* phEnum, mdSignature rSignatures[], ULONG cmax,
                                             ULONG* pcSignatures) override;
    HRESULT STDMETHODCALLTYPE EnumTypeSpecs(HCORENUM* phEnum, mdTypeSpec rTypeSpecs[], ULONG cmax,
                                            ULONG* pcTypeSpecs) override;
    HRESULT STDMETHODCALLTYPE EnumUserStrings(HCORENUM* phEnum, mdString rStrings[], ULONG cmax,
                                              ULONG* pcStrings) override;
    HRESULT STDMETHODCALLTYPE GetParamForMethodIndex(mdMethodDef md, ULONG ulParamSeq, mdParamDef* ppd) override;
    HRESULT STDMETHODCALLTYPE EnumCustomAttributes(HCORENUM* phEnum, mdToken tk, mdToken tkType,
                                                   mdCustomAttribute rCustomAttributes[], ULONG cMax,
                                                   ULONG* pcCustomAttributes) override;
    HRESULT STDMETHODCALLTYPE GetCustomAttributeProps(mdCustomAttribute cv, mdToken* ptkObj, mdToken* ptkType,
                                                      void const** ppBlob, ULONG* pcbSize) override;
    HRESULT STDMETHODCALLTYPE FindTypeRef(mdToken tkResolutionScope, LPCWSTR szName, mdTypeRef* ptr) override;
    HRESULT STDMETHODCALLTYPE GetMemberProps(mdToken mb, mdTypeDef* pClass, LPWSTR szMember, ULONG cchMember,
                                             ULONG* pchMember, DWORD* pdwAttr, PCCOR_SIGNATURE* ppvSigBlob,
                                             ULONG* pcbSigBlob, ULONG* pulCodeRVA, DWORD* pdwImplFlags,
                                             DWORD* pdwCPlusTypeFlag, UVCP_CONSTANT* ppValue,
                                             ULONG* pcchValue) override;
    HRESULT STDMETHODCALLTYPE GetFieldProps(mdFieldDef mb, mdTypeDef* pClass, LPWSTR szField, ULONG cchField,
                                            ULONG* pchField, DWORD* pdwAttr, PCCOR_SIGNATURE* ppvSigBlob,
                                            ULONG* pcbSigBlob, DWORD* pdwCPlusTypeFlag, UVCP_CONSTANT* ppValue,
                                            ULONG* pcchValue) override;
    HRESULT STDMETHODCALLTYPE GetPropertyProps(mdProperty prop, mdTypeDef* pClass, LPCWSTR szProperty,
                                               ULONG cchProperty, ULONG* pchProperty, DWORD* pdwPropFlags,
                                               PCCOR_SIGNATURE* ppvSig, ULONG* pbSig, DWORD* pdwCPlusTypeFlag,
                                               UVCP_CONSTANT* ppDefaultValue, ULONG* pcchDefaultValue,
                                               mdMethodDef* pmdSetter, mdMethodDef* pmdGetter,
                                               mdMethodDef rmdOtherMethod[], ULONG cMax, ULONG* pcOtherMethod) override;
    HRESULT STDMETHODCALLTYPE GetParamProps(mdParamDef tk, mdMethodDef* pmd, ULONG* pulSequence, LPWSTR szName,
                                            ULONG cchName, ULONG* pchName, DWORD* pdwAttr, DWORD* pdwCPlusTypeFlag,
                                            UVCP_CONSTANT* ppValue, ULONG* pcchValue) override;
    HRESULT STDMETHODCALLTYPE GetCustomAttributeByName(mdToken tkObj, LPCWSTR szName, const void** ppData,
                                                       ULONG* pcbData) override;
    BOOL STDMETHODCALLTYPE IsValidToken(mdToken tk) override;
    HRESULT STDMETHODCALLTYPE GetNestedClassProps(mdTypeDef tdNestedClass, mdTypeDef* ptdEnclosingClass) override;
    HRESULT STDMETHODCALLTYPE GetNativeCallConvFromSig(void const* pvSig, ULONG cbSig, ULONG* pCallConv) override;
    HRESULT STDMETHODCALLTYPE IsGlobal(mdToken pd, int* pbGlobal) override;
    HRESULT STDMETHODCALLTYPE EnumGenericParams(HCORENUM* phEnum, mdToken tk, mdGenericParam rGenericParams[],
                                                ULONG cMax, ULONG* pcGenericParams) override;
    HRESULT STDMETHODCALLTYPE GetGenericParamProps(mdGenericParam gp, ULONG* pulParamSeq, DWORD* pdwParamFlags,
                                                   mdToken* ptOwner, DWORD* reserved, LPWSTR wzname, ULONG cchName,
                                                   ULONG* pchName) override;
    HRESULT STDMETHODCALLTYPE GetMethodSpecProps(mdMethodSpec mi, mdToken* tkParent, PCCOR_SIGNATURE* ppvSigBlob,
                                                 ULONG* pcbSigBlob) override;
    HRESULT STDMETHODCALLTYPE EnumGenericParamConstraints(HCORENUM* phEnum, mdGenericParam tk,
                                                          mdGenericParamConstraint rGenericParamConstraints[],
                                                          ULONG cMax, ULONG* pcGenericParamConstraints) override;
    HRESULT STDMETHODCALLTYPE GetGenericParamConstraintProps(mdGenericParamConstraint gpc,
                                                             mdGenericParam* ptGenericParam,
                                                             mdToken* ptkConstraintType) override;
    HRESULT STDMETHODCALLTYPE GetPEKind(DWORD* pdwPEKind, DWORD* pdwMAchine) override;
    HRESULT STDMETHODCALLTYPE GetVersionString(LPWSTR pwzBuf, DWORD ccBufSize, DWORD* pccBufSize) override;
    HRESULT STDMETHODCALLTYPE EnumMethodSpecs(HCORENUM* phEnum, mdToken tk, mdMethodSpec rMethodSpecs[], ULONG cMax,
                                              ULONG* pcMethodSpecs) override;
    HRESULT STDMETHODCALLTYPE UnmarkAll() override;
    HRESULT STDMETHODCALLTYPE MarkToken(mdToken tk) override;
    HRESULT STDMETHODCALLTYPE IsTokenMarked(mdToken tk, BOOL* pIsMarked) override;
    HRESULT STDMETHODCALLTYPE DefineAssembly(const void* pbPublicKey, ULONG cbPublicKey, ULONG ulHashAlgId,
                                             LPCWSTR szName, const ASSEMBLYMETADATA* pMetaData, DWORD dwAssemblyFlags,
                                             mdAssembly* pma) override;
    HRESULT STDMETHODCALLTYPE DefineAssemblyRef(const void* pbPublicKeyOrToken, ULONG cbPublicKeyOrToken,
                                                LPCWSTR szName, const ASSEMBLYMETADATA* pMetaData,
                                                const void* pbHashValue, ULONG cbHashValue, DWORD dwAssemblyRefFlags,
                                                mdAssemblyRef* pmdar) override;
    HRESULT STDMETHODCALLTYPE DefineFile(LPCWSTR szName, const void* pbHashValue, ULONG cbHashValue, DWORD dwFileFlags,
                                         mdFile* pmdf) override;
    HRESULT STDMETHODCALLTYPE DefineExportedType(LPCWSTR szName, mdToken tkImplementation, mdTypeDef tkTypeDef,
                                                 DWORD dwExportedTypeFlags, mdExportedType* pmdct) override;
    HRESULT STDMETHODCALLTYPE DefineManifestResource(LPCWSTR szName, mdToken tkImplementation, DWORD dwOffset,
                                                     DWORD dwResourceFlags, mdManifestResource* pmdmr) override;
    HRESULT STDMETHODCALLTYPE SetAssemblyProps(mdAssembly pma, const void* pbPublicKey, ULONG cbPublicKey,
                                               ULONG ulHashAlgId, LPCWSTR szName, const ASSEMBLYMETADATA* pMetaData,
                                               DWORD dwAssemblyFlags) override;
    HRESULT STDMETHODCALLTYPE SetAssemblyRefProps(mdAssemblyRef ar, const void* pbPublicKeyOrToken,
                                                  ULONG cbPublicKeyOrToken, LPCWSTR szName,
                                                  const ASSEMBLYMETADATA* pMetaData, const void* pbHashValue,
                                                  ULONG cbHashValue, DWORD dwAssemblyRefFlags) override;
    HRESULT STDMETHODCALLTYPE SetFileProps(mdFile file, const void* pbHashValue, ULONG cbHashValue,
                                           DWORD dwFileFlags) override;
    HRESULT STDMETHODCALLTYPE SetExportedTypeProps(mdExportedType ct, mdToken tkImplementation, mdTypeDef tkTypeDef,
                                                   DWORD dwExportedTypeFlags) override;
    HRESULT STDMETHODCALLTYPE SetManifestResourceProps(mdManifestResource mr, mdToken tkImplementation, DWORD dwOffset,
                                                       DWORD dwResourceFlags) override;
    HRESULT STDMETHODCALLTYPE GetAssemblyProps(mdAssembly mda, const void** ppbPublicKey, ULONG* pcbPublicKey,
                                               ULONG* pulHashAlgId, LPWSTR szName, ULONG cchName, ULONG* pchName,
                                               ASSEMBLYMETADATA* pMetaData, DWORD* pdwAssemblyFlags) override;
    HRESULT STDMETHODCALLTYPE GetAssemblyRefProps(mdAssemblyRef mdar, const void** ppbPublicKeyOrToken,
                                                  ULONG* pcbPublicKeyOrToken, LPWSTR szName, ULONG cchName,
                                                  ULONG* pchName, ASSEMBLYMETADATA* pMetaData,
                                                  const void** ppbHashValue, ULONG* pcbHashValue,
                                                  DWORD* pdwAssemblyRefFlags) override;
    HRESULT STDMETHODCALLTYPE GetFileProps(mdFile mdf, LPWSTR szName, ULONG cchName, ULONG* pchName,
                                           const void** ppbHashValue, ULONG* pcbHashValue,
                                           DWORD* pdwFileFlags) override;
    HRESULT STDMETHODCALLTYPE GetExportedTypeProps(mdExportedType mdct, LPWSTR szName, ULONG cchName, ULONG* pchName,
                                                   mdToken* ptkImplementation, mdTypeDef* ptkTypeDef,
                                                   DWORD* pdwExportedTypeFlags) override;
    HRESULT STDMETHODCALLTYPE GetManifestResourceProps(mdManifestResource mdmr, LPWSTR szName, ULONG cchName,
                                                       ULONG* pchName, mdToken* ptkImplementation, DWORD* pdwOffset,
                                                       DWORD* pdwResourceFlags) override;
    HRESULT STDMETHODCALLTYPE EnumAssemblyRefs(HCORENUM* phEnum, mdAssemblyRef rAssemblyRefs[], ULONG cMax,
                                               ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE EnumFiles(HCORENUM* phEnum, mdFile rFiles[], ULONG cMax, ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE EnumExportedTypes(HCORENUM* phEnum, mdExportedType rExportedTypes[], ULONG cMax,
                                                ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE EnumManifestResources(HCORENUM* phEnum, mdManifestResource rManifestResources[],
                                                    ULONG cMax, ULONG* pcTokens) override;
    HRESULT STDMETHODCALLTYPE GetAssemblyFromScope(mdAssembly* ptkAssembly) override;
    HRESULT STDMETHODCALLTYPE FindExportedTypeByName(LPCWSTR szName, mdToken mdtExportedType,
                                                     mdExportedType* ptkExportedType) override;
    HRESULT STDMETHODCALLTYPE FindManifestResourceByName(LPCWSTR szName,
                                                         mdManifestResource* ptkManifestResource) override;
    HRESULT STDMETHODCALLTYPE FindAssembliesByName(LPCWSTR szAppBase, LPCWSTR szPrivateBin, LPCWSTR szAssemblyName,
                                                   IUnknown* ppIUnk[], ULONG cMax, ULONG* pcAssemblies) override;
    HRESULT STDMETHODCALLTYPE ValidatorInit(DWORD dwModuleType, IUnknown* pUnk) override;
    HRESULT STDMETHODCALLTYPE ValidateMetaData() override;
    HRESULT STDMETHODCALLTYPE SetOption(const GUID& optionid, const VARIANT* value) override;
    HRESULT STDMETHODCALLTYPE GetOption(const GUID& optionid, VARIANT* pvalue) override;
    HRESULT STDMETHODCALLTYPE OpenScopeOnITypeInfo(ITypeInfo* pITI, DWORD dwOpenFlags, const IID& riid,
                                                   IUnknown** ppIUnk) override;
    HRESULT STDMETHODCALLTYPE GetCORSystemDirectory(LPWSTR szBuffer, DWORD cchBuffer, DWORD* pchBuffer) override;
    HRESULT STDMETHODCALLTYPE FindAssembly(LPCWSTR szAppBase, LPCWSTR szPrivateBin, LPCWSTR szGlobalBin,
                                           LPCWSTR szAssemblyName, LPCWSTR szName, ULONG cchName,
                                           ULONG* pcName) override;
    HRESULT STDMETHODCALLTYPE FindAssemblyModule(LPCWSTR szAppBase, LPCWSTR szPrivateBin, LPCWSTR szGlobalBin,
                                                 LPCWSTR szAssemblyName, LPCWSTR szModuleName, LPWSTR szName,
                                                 ULONG cchName, ULONG* pcName) override;
    HRESULT STDMETHODCALLTYPE EmitString(LPWSTR lpString, ULONG* RVA) override;
    HRESULT STDMETHODCALLTYPE GetString(ULONG RVA, LPWSTR* lpString) override;
    HRESULT STDMETHODCALLTYPE AllocateMethodBuffer(ULONG cchBuffer, UCHAR** lpBuffer, ULONG* RVA) override;
    HRESULT STDMETHODCALLTYPE GetMethodBuffer(ULONG RVA, UCHAR** lpBuffer) override;
    HRESULT STDMETHODCALLTYPE GetIMapTokenIface(IUnknown** pIMapToken) override;
    HRESULT STDMETHODCALLTYPE GenerateCeeFile() override;
    HRESULT STDMETHODCALLTYPE GetIlSection(HCEESECTION* section) override;
    HRESULT STDMETHODCALLTYPE GetStringSection(HCEESECTION* section) override;
    HRESULT STDMETHODCALLTYPE AddSectionReloc(HCEESECTION section, ULONG offset, HCEESECTION relativeTo,
                                              CeeSectionRelocType relocType) override;
    HRESULT STDMETHODCALLTYPE GetSectionCreate(const char* name, DWORD flags, HCEESECTION* section) override;
    HRESULT STDMETHODCALLTYPE GetSectionDataLen(HCEESECTION section, ULONG* dataLen) override;
    HRESULT STDMETHODCALLTYPE GetSectionBlock(HCEESECTION section, ULONG len, ULONG align, void** ppBytes) override;
    HRESULT STDMETHODCALLTYPE TruncateSection(HCEESECTION section, ULONG len) override;
    HRESULT STDMETHODCALLTYPE GenerateCeeMemoryImage(void** ppImage) override;
    HRESULT STDMETHODCALLTYPE ComputePointer(HCEESECTION section, ULONG RVA, UCHAR** lpBuffer) override;
    HRESULT STDMETHODCALLTYPE GetStringHeapSize(ULONG* pcbStrings) override;
    HRESULT STDMETHODCALLTYPE GetBlobHeapSize(ULONG* pcbBlobs) override;
    HRESULT STDMETHODCALLTYPE GetGuidHeapSize(ULONG* pcbGuids) override;
    HRESULT STDMETHODCALLTYPE GetUserStringHeapSize(ULONG* pcbBlobs) override;
    HRESULT STDMETHODCALLTYPE GetNumTables(ULONG* pcTables) override;
    HRESULT STDMETHODCALLTYPE GetTableIndex(ULONG token, ULONG* pixTbl) override;
    HRESULT STDMETHODCALLTYPE GetTableInfo(ULONG ixTbl, ULONG* pcbRow, ULONG* pcRows, ULONG* pcCols, ULONG* piKey,
                                           const char** ppName) override;
    HRESULT STDMETHODCALLTYPE GetColumnInfo(ULONG ixTbl, ULONG ixCol, ULONG* poCol, ULONG* pcbCol, ULONG* pType,
                                            const char** ppName) override;
    HRESULT STDMETHODCALLTYPE GetCodedTokenInfo(ULONG ixCdTkn, ULONG* pcTokens, ULONG** ppTokens,
                                                const char** ppName) override;
    HRESULT STDMETHODCALLTYPE GetRow(ULONG ixTbl, ULONG rid, void** ppRow) override;
    HRESULT STDMETHODCALLTYPE GetColumn(ULONG ixTbl, ULONG ixCol, ULONG rid, ULONG* pVal) override;
    HRESULT STDMETHODCALLTYPE GetString(ULONG ixString, const char** ppString) override;
    HRESULT STDMETHODCALLTYPE GetBlob(ULONG ixBlob, ULONG* pcbData, const void** ppData) override;
    HRESULT STDMETHODCALLTYPE GetGuid(ULONG ixGuid, const GUID** ppGUID) override;
    HRESULT STDMETHODCALLTYPE GetUserString(ULONG ixUserString, ULONG* pcbData, const void** ppData) override;
    HRESULT STDMETHODCALLTYPE GetNextString(ULONG ixString, ULONG* pNext) override;
    HRESULT STDMETHODCALLTYPE GetNextBlob(ULONG ixBlob, ULONG* pNext) override;
    HRESULT STDMETHODCALLTYPE GetNextGuid(ULONG ixGuid, ULONG* pNext) override;
    HRESULT STDMETHODCALLTYPE GetNextUserString(ULONG ixUserString, ULONG* pNext) override;
    HRESULT STDMETHODCALLTYPE GetMetaDataStorage(const void** ppvMd, ULONG* pcbMd) override;
    HRESULT STDMETHODCALLTYPE GetMetaDataStreamInfo(ULONG ix, const char** ppchName, const void** ppv,
                                                    ULONG* pcb) override;
    HRESULT STDMETHODCALLTYPE GetFileMapping(const void** ppvData, ULONGLONG* pcbData, DWORD* pdwMappingType) override;
};

} // namespace datadog::shared::nativeloader
