#include "instrumented_assembly_generator_metadata_interfaces.h"
#include "../log.h"
#include "instrumented_assembly_generator_helper.h"

namespace instrumented_assembly_generator
{
// TODO:
// The behaviour of the IMetadata interface when defining members is to create a new one if no one exist,
// and return the existing one if it's already exist, so by definition we may write to disk more than one identical member.
// To avoid this, we have to keep hashset of the
// created tokens.

MetadataInterfaces::MetadataInterfaces(
    const ComPtr<IUnknown>& metadataInterfaces) :
    m_metadataInterfaces(metadataInterfaces)
{
    AddRef();
    // attach from tests, we can do it also with ev and spin
    // std::this_thread::sleep_for(std::chrono::milliseconds(20000));
}

MetadataInterfaces::~MetadataInterfaces()
{
}

HRESULT STDMETHODCALLTYPE MetadataInterfaces::QueryInterface(REFIID riid, void** ppvObject)
{
    if (ppvObject == nullptr)
    {
        return E_POINTER;
    }

    if (riid == IID_IMetaDataError || riid == IID_IMapToken || riid == IID_IMetaDataDispenser ||
        riid == IID_IMetaDataDispenserEx || riid == IID_IMetaDataEmit || riid == IID_IMetaDataEmit2 ||
        riid == IID_IMetaDataImport || riid == IID_IMetaDataImport2 || riid == IID_IMetaDataFilter ||
        riid == IID_IHostFilter || riid == IID_IMetaDataAssemblyEmit || riid == IID_IMetaDataAssemblyImport ||
        riid == IID_IMetaDataValidate || riid == IID_ICeeGen || riid == IID_IMetaDataTables ||
        riid == IID_IMetaDataTables2 || riid == IID_IMetaDataInfo || 
        riid == IID_IUnknown)
    {
        ComPtr<IUnknown> temp;
        const HRESULT hr = m_metadataInterfaces->QueryInterface(riid, reinterpret_cast<void**>(temp.GetAddressOf()));
        if (FAILED(hr))
        {
            Log::Warn(
                "InstrumentedAssemblyGeneratorMetadataInterfaces::QueryInterface: Failed to get metadata X interface.");
            return hr;
        }
        if (temp.Get() != nullptr)
        {
            m_metadataInterfaces = temp;
        }

        if (riid == IID_IMetaDataError)
        {
            *ppvObject = static_cast<IMetaDataError*>(this);
        }
        else if (riid == IID_IMapToken)
        {
            *ppvObject = static_cast<IMapToken*>(this);
        }
        else if (riid == IID_IMetaDataDispenser)
        {
            *ppvObject = static_cast<IMetaDataDispenser*>(this);
        }
        else if (riid == IID_IMetaDataDispenserEx)
        {
            *ppvObject = static_cast<IMetaDataDispenserEx*>(this);
        }
        else if (riid == IID_IMetaDataEmit)
        {
            *ppvObject = static_cast<IMetaDataEmit*>(this);
        }
        else if (riid == IID_IMetaDataEmit2)
        {
            *ppvObject = static_cast<IMetaDataEmit2*>(this);
        }
        else if (riid == IID_IMetaDataImport)
        {
            *ppvObject = static_cast<IMetaDataImport*>(this);
        }
        else if (riid == IID_IMetaDataImport2)
        {
            *ppvObject = static_cast<IMetaDataImport2*>(this);
        }
        else if (riid == IID_IMetaDataFilter)
        {
            *ppvObject = static_cast<IMetaDataFilter*>(this);
        }
        else if (riid == IID_IHostFilter)
        {
            *ppvObject = static_cast<IHostFilter*>(this);
        }
        else if (riid == IID_IMetaDataAssemblyEmit)
        {
            *ppvObject = static_cast<IMetaDataAssemblyEmit*>(this);
        }
        else if (riid == IID_IMetaDataAssemblyImport)
        {
            *ppvObject = static_cast<IMetaDataAssemblyImport*>(this);
        }
        else if (riid == IID_IMetaDataValidate)
        {
            *ppvObject = static_cast<IMetaDataValidate*>(this);
        }
        else if (riid == IID_ICeeGen)
        {
            *ppvObject = static_cast<ICeeGen*>(this);
        }
        else if (riid == IID_IMetaDataTables)
        {
            *ppvObject = static_cast<IMetaDataTables*>(this);
        }
        else if (riid == IID_IMetaDataTables2)
        {
            *ppvObject = static_cast<IMetaDataTables2*>(this);
        }
        else if (riid == IID_IMetaDataInfo)
        {
            *ppvObject = static_cast<IMetaDataInfo*>(this);
        }
        else
        {
            *ppvObject = this;
        }

        this->AddRef();
        return hr;
    }

    *ppvObject = nullptr;
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE MetadataInterfaces::AddRef(void)
{
    return std::atomic_fetch_add(&this->m_refCount, 1) + 1;
}

ULONG STDMETHODCALLTYPE MetadataInterfaces::Release(void)
{
    int count = std::atomic_fetch_sub(&this->m_refCount, 1) - 1;

    if (count <= 0)
    {
        delete this;
    }

    return count;
}

void MetadataInterfaces::WriteMetadataChange(const mdToken* pToken,
                                                                          const shared::WSTRING& metadataName) const
{
    const auto metadataImport = m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport);
    auto [hr, moduleName, mvid] = GetModuleNameAndMvid(metadataImport);
    if (FAILED(hr)) return;

    shared::WSTRINGSTREAM fileNameStream;
    fileNameStream << mvid << FileNameSeparator << GetCleanedFileName(moduleName) << ModuleMembersFileExtension;

    shared::WSTRINGSTREAM stringStream;
    // each line in the file is: "token=name"
    // TODO: handle empty token
    stringStream << std::hex << *pToken << WStr("=") << metadataName << std::endl;

    WriteTextToFile(fileNameStream.str(), stringStream.str());
}

HRESULT MetadataInterfaces::DefineTypeDef(LPCWSTR szTypeDef, DWORD dwTypeDefFlags,
                                                                       mdToken tkExtends, mdToken rtkImplements[],
                                                                       mdTypeDef* ptd)
{
    const auto hr = m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
                        ->DefineTypeDef(szTypeDef, dwTypeDefFlags, tkExtends, rtkImplements, ptd);
    if (SUCCEEDED(hr)) WriteMetadataChange(ptd, szTypeDef);
    return hr;
}

HRESULT MetadataInterfaces::DefineMethod(mdTypeDef td, LPCWSTR szName, DWORD dwMethodFlags,
                                                                      PCCOR_SIGNATURE pvSigBlob, ULONG cbSigBlob,
                                                                      ULONG ulCodeRVA, DWORD dwImplFlags,
                                                                      mdMethodDef* pmd)
{
    const auto hr = m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
                        ->DefineMethod(td, szName, dwMethodFlags, pvSigBlob, cbSigBlob, ulCodeRVA, dwImplFlags, pmd);
    if (SUCCEEDED(hr))
    {
        const auto metadataImport = m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport);
        auto methodAndTypeInfo =
            MethodInfo::GetMethodInfo(metadataImport, *pmd, shared::WSTRING(szName), td, pvSigBlob, cbSigBlob);
        if (methodAndTypeInfo.methodSig.IsValid())
        {
            const auto fullName = methodAndTypeInfo.typeName + WStr(".") + methodAndTypeInfo.name + WStr("(") +
                                  methodAndTypeInfo.methodSig.ArgumentsNames(metadataImport) + WStr(")");
            WriteMetadataChange(pmd, fullName);
        }
        else
        {
            Log::Warn("InstrumentedAssemblyGeneratorMetadataInterfaces::DefineMethod: methodSig is not valid");
        }
    }
    return hr;
}

HRESULT MetadataInterfaces::DefineTypeRefByName(mdToken tkResolutionScope, LPCWSTR szName,
                                                                             mdTypeRef* ptr)
{
    const auto hr =
        m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->DefineTypeRefByName(tkResolutionScope, szName, ptr);
    if (SUCCEEDED(hr)) WriteMetadataChange(ptr, szName);
    return hr;
}

HRESULT MetadataInterfaces::DefineMemberRef(mdToken tkImport, LPCWSTR szName,
                                                                         PCCOR_SIGNATURE pvSigBlob, ULONG cbSigBlob,
                                                                         mdMemberRef* pmr)
{
    auto hr = m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
                  ->DefineMemberRef(tkImport, szName, pvSigBlob, cbSigBlob, pmr);

    IfFailRet(hr);

    const auto metadataImport = m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport);

    auto methodAndTypeInfo =
        MethodInfo::GetMethodInfo(metadataImport, *pmr, shared::WSTRING(szName), tkImport, pvSigBlob, cbSigBlob);
    if (methodAndTypeInfo.methodSig.IsValid())
    {
        const auto typeAndMethod = methodAndTypeInfo.methodSig.ReturnTypeName(metadataImport) + WStr(" ") +
                                   methodAndTypeInfo.typeName + WStr(".") + methodAndTypeInfo.name;
        const auto arguments = WStr("(") + methodAndTypeInfo.methodSig.ArgumentsNames(metadataImport) + WStr(")");
        shared::WSTRING fullName;
        if (methodAndTypeInfo.methodSig.NumberOfTypeArguments() == 0)
        {
            fullName = typeAndMethod + arguments;
        }
        else
        {
            fullName =
                typeAndMethod + WStr("<") + methodAndTypeInfo.methodSig.TypeArgumentsNames() + WStr(">") + arguments;
        }

        WriteMetadataChange(pmr, fullName);
    }
    else
    {
        Log::Warn("InstrumentedAssemblyGeneratorMetadataInterfaces::DefineMemberRef: methodSig is not valid");
    }

    return hr;
}

HRESULT MetadataInterfaces::DefineModuleRef(LPCWSTR szName, mdModuleRef* pmur)
{
    const auto hr = m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->DefineModuleRef(szName, pmur);
    if (SUCCEEDED(hr)) WriteMetadataChange(pmur, szName);
    return hr;
}

HRESULT MetadataInterfaces::DefineUserString(LPCWSTR szString, ULONG cchString,
                                                                          mdString* pstk)
{
    const auto hr =
        m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->DefineUserString(szString, cchString, pstk);
    if (SUCCEEDED(hr)) WriteMetadataChange(pstk, szString);
    return hr;
}

HRESULT MetadataInterfaces::DefineField(mdTypeDef td, LPCWSTR szName, DWORD dwFieldFlags,
                                                                     PCCOR_SIGNATURE pvSigBlob, ULONG cbSigBlob,
                                                                     DWORD dwCPlusTypeFlag, void const* pValue,
                                                                     ULONG cchValue, mdFieldDef* pmd)
{
    const auto hr =
        m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
            ->DefineField(td, szName, dwFieldFlags, pvSigBlob, cbSigBlob, dwCPlusTypeFlag, pValue, cchValue, pmd);
    if (SUCCEEDED(hr))
    {
        shared::WSTRING fullName;
        const auto tempHr = MemberSignature::GetMemberFullName(
            td, szName, m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport), fullName);

        if (SUCCEEDED(tempHr))
        {
            const auto field = MemberSignature(pvSigBlob, cbSigBlob, 0);
            const auto fieldTypeSig =
                field.TypeSigToString(m_metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport2));
            WriteMetadataChange(pmd, fullName + WStr("?") + fieldTypeSig + WStr("?") + IntToHex(dwFieldFlags));
        }
    }
    return hr;
}

HRESULT MetadataInterfaces::DefineProperty(
    mdTypeDef td, LPCWSTR szProperty, DWORD dwPropFlags, PCCOR_SIGNATURE pvSig, ULONG cbSig, DWORD dwCPlusTypeFlag,
    void const* pValue, ULONG cchValue, mdMethodDef mdSetter, mdMethodDef mdGetter, mdMethodDef rmdOtherMethods[],
    mdProperty* pmdProp)
{
    const auto hr = m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
                        ->DefineProperty(td, szProperty, dwPropFlags, pvSig, cbSig, dwCPlusTypeFlag, pValue, cchValue,
                                         mdSetter, mdGetter, rmdOtherMethods, pmdProp);

    if (SUCCEEDED(hr))
    {
        shared::WSTRING fullName;
        const auto tempHr = MemberSignature::GetMemberFullName(
            td, szProperty, m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport), fullName);

        if (SUCCEEDED(tempHr)) WriteMetadataChange(pmdProp, fullName);
    }
    return hr;
}

HRESULT MetadataInterfaces::DefineMethodSpec(mdToken tkParent, PCCOR_SIGNATURE pvSigBlob,
                                                                          ULONG cbSigBlob, mdMethodSpec* pmi)
{
    auto hr = m_metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit2)
                  ->DefineMethodSpec(tkParent, pvSigBlob, cbSigBlob, pmi);

    IfFailRet(hr);

    mdTypeDef pClass = mdTypeDefNil;
    ULONG pchMethod;
    DWORD pdwAttr;
    WCHAR szMethod[name_length_limit]{};
    HRESULT tempHr;
    PCCOR_SIGNATURE methodSig = nullptr;
    ULONG methodSigLength;
    const auto metadataImport = m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport);
    switch (TypeFromToken(tkParent))
    {
        case mdtMethodDef:
            tempHr = m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
                         ->GetMethodProps(tkParent, &pClass, szMethod, name_length_limit, &pchMethod, &pdwAttr,
                                          &methodSig, &methodSigLength, nullptr, nullptr);
            break;
        case mdtMemberRef:
            mdToken ptk;
            tempHr = metadataImport->GetMemberRefProps(tkParent, &ptk, szMethod, name_length_limit, &pchMethod,
                                                       &methodSig, &methodSigLength);
            break;
        default:
            tempHr = E_FAIL;
    }

    if (SUCCEEDED(tempHr))
    {
        shared::WSTRING fullName;
        tempHr = MemberSignature::GetGenericsMemberFullName(
            *pmi, tkParent, szMethod, m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport), fullName);
        if (SUCCEEDED(tempHr))
        {
            auto methodAndTypeInfo = MethodInfo::GetMethodInfo(metadataImport, *pmi, shared::WSTRING(szMethod), tkParent,
                                                               methodSig, methodSigLength);
            if (methodAndTypeInfo.methodSig.IsValid())
            {
                fullName += WStr("(") + methodAndTypeInfo.methodSig.ArgumentsNames(metadataImport) + WStr(")");
                WriteMetadataChange(pmi, fullName);
            }
            else
            {
                Log::Warn(
                    "InstrumentedAssemblyGeneratorMetadataInterfaces::DefineMethodSpec: methodSig is not valid {}",
                     fullName);
            }
        }
        else
        {
            Log::Warn("InstrumentedAssemblyGeneratorMetadataInterfaces::DefineMethodSpec: methodSig is not valid {}",
                 fullName);
        }
    }
    else
    {
        Log::Warn("InstrumentedAssemblyGeneratorMetadataInterfaces::DefineMethodSpec: Fail to get method props");
    }
    return hr;
}

HRESULT MetadataInterfaces::GetTokenFromSig(PCCOR_SIGNATURE pvSig, ULONG cbSig,
                                                                         mdSignature* pmsig)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->GetTokenFromSig(pvSig, cbSig, pmsig);
}

HRESULT MetadataInterfaces::DefineAssembly(const void* pbPublicKey, ULONG cbPublicKey,
                                                                        ULONG ulHashAlgId, LPCWSTR szName,
                                                                        const ASSEMBLYMETADATA* pMetaData,
                                                                        DWORD dwAssemblyFlags, mdAssembly* pma)
{
    const auto hr =
        m_metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit)
            ->DefineAssembly(pbPublicKey, cbPublicKey, ulHashAlgId, szName, pMetaData, dwAssemblyFlags, pma);
    if (SUCCEEDED(hr)) WriteMetadataChange(pma, szName);
    return hr;
}

HRESULT MetadataInterfaces::DefineAssemblyRef(
    const void* pbPublicKeyOrToken, ULONG cbPublicKeyOrToken, LPCWSTR szName, const ASSEMBLYMETADATA* pMetaData,
    const void* pbHashValue, ULONG cbHashValue, DWORD dwAssemblyRefFlags, mdAssemblyRef* pmdar)
{
    const auto hr = m_metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit)
                        ->DefineAssemblyRef(pbPublicKeyOrToken, cbPublicKeyOrToken, szName, pMetaData, pbHashValue,
                                            cbHashValue, dwAssemblyRefFlags, pmdar);
    if (SUCCEEDED(hr)) WriteMetadataChange(pmdar, szName);
    return hr;
}

///////////////////////////////////////////////////////////
/// We will add support to the following functions ASAP ///
///////////////////////////////////////////////////////////
HRESULT MetadataInterfaces::DefineNestedType(LPCWSTR szTypeDef, DWORD dwTypeDefFlags,
                                                                          mdToken tkExtends, mdToken rtkImplements[],
                                                                          mdTypeDef tdEncloser, mdTypeDef* ptd)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->DefineNestedType(szTypeDef, dwTypeDefFlags, tkExtends, rtkImplements, tdEncloser, ptd);
}

HRESULT MetadataInterfaces::DefineMethodImpl(mdTypeDef td, mdToken tkBody, mdToken tkDecl)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->DefineMethodImpl(td, tkBody, tkDecl);
}

HRESULT MetadataInterfaces::DefineImportType(IMetaDataAssemblyImport* pAssemImport,
                                                                          const void* pbHashValue, ULONG cbHashValue,
                                                                          IMetaDataImport* pImport, mdTypeDef tdImport,
                                                                          IMetaDataAssemblyEmit* pAssemEmit,
                                                                          mdTypeRef* ptr)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->DefineImportType(pAssemImport, pbHashValue, cbHashValue, pImport, tdImport, pAssemEmit, ptr);
}

HRESULT MetadataInterfaces::DefineImportMember(IMetaDataAssemblyImport* pAssemImport,
                                                                            const void* pbHashValue, ULONG cbHashValue,
                                                                            IMetaDataImport* pImport, mdToken mbMember,
                                                                            IMetaDataAssemblyEmit* pAssemEmit,
                                                                            mdToken tkParent, mdMemberRef* pmr)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->DefineImportMember(pAssemImport, pbHashValue, cbHashValue, pImport, mbMember, pAssemEmit, tkParent, pmr);
}

HRESULT MetadataInterfaces::DefineEvent(mdTypeDef td, LPCWSTR szEvent, DWORD dwEventFlags,
                                                                     mdToken tkEventType, mdMethodDef mdAddOn,
                                                                     mdMethodDef mdRemoveOn, mdMethodDef mdFire,
                                                                     mdMethodDef rmdOtherMethods[], mdEvent* pmdEvent)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->DefineEvent(td, szEvent, dwEventFlags, tkEventType, mdAddOn, mdRemoveOn, mdFire, rmdOtherMethods, pmdEvent);
}

HRESULT MetadataInterfaces::DefineCustomAttribute(mdToken tkOwner, mdToken tkCtor,
                                                                               void const* pCustomAttribute,
                                                                               ULONG cbCustomAttribute,
                                                                               mdCustomAttribute* pcv)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->DefineCustomAttribute(tkOwner, tkCtor, pCustomAttribute, cbCustomAttribute, pcv);
}

HRESULT MetadataInterfaces::DefineParam(mdMethodDef md, ULONG ulParamSeq, LPCWSTR szName,
                                                                     DWORD dwParamFlags, DWORD dwCPlusTypeFlag,
                                                                     void const* pValue, ULONG cchValue,
                                                                     mdParamDef* ppd)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->DefineParam(md, ulParamSeq, szName, dwParamFlags, dwCPlusTypeFlag, pValue, cchValue, ppd);
}

HRESULT MetadataInterfaces::DefineGenericParam(mdToken tk, ULONG ulParamSeq,
                                                                            DWORD dwParamFlags, LPCWSTR szname,
                                                                            DWORD reserved, mdToken rtkConstraints[],
                                                                            mdGenericParam* pgp)
{
    return m_metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit2)
        ->DefineGenericParam(tk, ulParamSeq, dwParamFlags, szname, reserved, rtkConstraints, pgp);
}

HRESULT MetadataInterfaces::DefineExportedType(LPCWSTR szName, mdToken tkImplementation,
                                                                            mdTypeDef tkTypeDef,
                                                                            DWORD dwExportedTypeFlags,
                                                                            mdExportedType* pmdct)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit)
        ->DefineExportedType(szName, tkImplementation, tkTypeDef, dwExportedTypeFlags, pmdct);
}

HRESULT MetadataInterfaces::EmitString(LPWSTR lpString, ULONG* RVA)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->EmitString(lpString, RVA);
}

//////////////////////////////////////////////////////////////////////
/// Delegate all the remaining functions to the "real" IMetaDataXXX //
//////////////////////////////////////////////////////////////////////

HRESULT MetadataInterfaces::DefineFile(LPCWSTR szName, const void* pbHashValue,
                                                                    ULONG cbHashValue, DWORD dwFileFlags, mdFile* pmdf)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit)
        ->DefineFile(szName, pbHashValue, cbHashValue, dwFileFlags, pmdf);
}

HRESULT MetadataInterfaces::DefineManifestResource(LPCWSTR szName,
                                                                                mdToken tkImplementation,
                                                                                DWORD dwOffset, DWORD dwResourceFlags,
                                                                                mdManifestResource* pmdmr)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit)
        ->DefineManifestResource(szName, tkImplementation, dwOffset, dwResourceFlags, pmdmr);
}

HRESULT MetadataInterfaces::OnError(HRESULT hrError, mdToken token)
{
    return m_metadataInterfaces.As<IMetaDataError>(IID_IMetaDataError)->OnError(hrError, token);
}

HRESULT MetadataInterfaces::Map(mdToken tkImp, mdToken tkEmit)
{
    return m_metadataInterfaces.As<IMapToken>(IID_IMapToken)->Map(tkImp, tkEmit);
}

HRESULT MetadataInterfaces::DefineScope(const IID& rclsid, DWORD dwCreateFlags,
                                                                     const IID& riid, IUnknown** ppIUnk)
{
    return m_metadataInterfaces.As<IMetaDataDispenser>(IID_IMetaDataDispenser)
        ->DefineScope(rclsid, dwCreateFlags, riid, ppIUnk);
}

HRESULT MetadataInterfaces::OpenScope(LPCWSTR szScope, DWORD dwOpenFlags, const IID& riid,
                                                                   IUnknown** ppIUnk)
{
    return m_metadataInterfaces.As<IMetaDataDispenser>(IID_IMetaDataDispenser)
        ->OpenScope(szScope, dwOpenFlags, riid, ppIUnk);
}

HRESULT MetadataInterfaces::OpenScopeOnMemory(LPCVOID pData, ULONG cbData,
                                                                           DWORD dwOpenFlags, const IID& riid,
                                                                           IUnknown** ppIUnk)
{
    return m_metadataInterfaces.As<IMetaDataDispenser>(IID_IMetaDataDispenser)
        ->OpenScopeOnMemory(pData, cbData, dwOpenFlags, riid, ppIUnk);
}

HRESULT MetadataInterfaces::SetModuleProps(LPCWSTR szName)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->SetModuleProps(szName);
}

HRESULT MetadataInterfaces::Save(LPCWSTR szFile, DWORD dwSaveFlags)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->Save(szFile, dwSaveFlags);
}

HRESULT MetadataInterfaces::SaveToStream(IStream* pIStream, DWORD dwSaveFlags)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->SaveToStream(pIStream, dwSaveFlags);
}

HRESULT MetadataInterfaces::GetSaveSize(CorSaveSize fSave, DWORD* pdwSaveSize)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->GetSaveSize(fSave, pdwSaveSize);
}

HRESULT MetadataInterfaces::SetHandler(IUnknown* pUnk)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->SetHandler(pUnk);
}

HRESULT MetadataInterfaces::SetClassLayout(mdTypeDef td, DWORD dwPackSize,
                                                                        COR_FIELD_OFFSET rFieldOffsets[],
                                                                        ULONG ulClassSize)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->SetClassLayout(td, dwPackSize, rFieldOffsets, ulClassSize);
}

HRESULT MetadataInterfaces::DeleteClassLayout(mdTypeDef td)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->DeleteClassLayout(td);
}

HRESULT MetadataInterfaces::SetFieldMarshal(mdToken tk, PCCOR_SIGNATURE pvNativeType,
                                                                         ULONG cbNativeType)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->SetFieldMarshal(tk, pvNativeType, cbNativeType);
}

HRESULT MetadataInterfaces::DeleteFieldMarshal(mdToken tk)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->DeleteFieldMarshal(tk);
}

HRESULT MetadataInterfaces::DefinePermissionSet(mdToken tk, DWORD dwAction,
                                                                             void const* pvPermission,
                                                                             ULONG cbPermission, mdPermission* ppm)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->DefinePermissionSet(tk, dwAction, pvPermission, cbPermission, ppm);
}

HRESULT MetadataInterfaces::SetRVA(mdMethodDef md, ULONG ulRVA)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->SetRVA(md, ulRVA);
}

HRESULT MetadataInterfaces::SetParent(mdMemberRef mr, mdToken tk)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->SetParent(mr, tk);
}

HRESULT MetadataInterfaces::GetTokenFromTypeSpec(PCCOR_SIGNATURE pvSig, ULONG cbSig,
                                                                              mdTypeSpec* ptypespec)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->GetTokenFromTypeSpec(pvSig, cbSig, ptypespec);
}

HRESULT MetadataInterfaces::SaveToMemory(void* pbData, ULONG cbData)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->SaveToMemory(pbData, cbData);
}

HRESULT MetadataInterfaces::DeleteToken(mdToken tkObj)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->DeleteToken(tkObj);
}

HRESULT MetadataInterfaces::SetMethodProps(mdMethodDef md, DWORD dwMethodFlags,
                                                                        ULONG ulCodeRVA, DWORD dwImplFlags)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->SetMethodProps(md, dwMethodFlags, ulCodeRVA, dwImplFlags);
}

HRESULT MetadataInterfaces::SetTypeDefProps(mdTypeDef td, DWORD dwTypeDefFlags,
                                                                         mdToken tkExtends, mdToken rtkImplements[])
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->SetTypeDefProps(td, dwTypeDefFlags, tkExtends, rtkImplements);
}

HRESULT MetadataInterfaces::SetEventProps(mdEvent ev, DWORD dwEventFlags,
                                                                       mdToken tkEventType, mdMethodDef mdAddOn,
                                                                       mdMethodDef mdRemoveOn, mdMethodDef mdFire,
                                                                       mdMethodDef rmdOtherMethods[])
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->SetEventProps(ev, dwEventFlags, tkEventType, mdAddOn, mdRemoveOn, mdFire, rmdOtherMethods);
}

HRESULT MetadataInterfaces::SetPermissionSetProps(mdToken tk, DWORD dwAction,
                                                                               void const* pvPermission,
                                                                               ULONG cbPermission, mdPermission* ppm)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->SetPermissionSetProps(tk, dwAction, pvPermission, cbPermission, ppm);
}

HRESULT MetadataInterfaces::DefinePinvokeMap(mdToken tk, DWORD dwMappingFlags,
                                                                          LPCWSTR szImportName, mdModuleRef mrImportDLL)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->DefinePinvokeMap(tk, dwMappingFlags, szImportName, mrImportDLL);
}

HRESULT MetadataInterfaces::SetPinvokeMap(mdToken tk, DWORD dwMappingFlags,
                                                                       LPCWSTR szImportName, mdModuleRef mrImportDLL)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->SetPinvokeMap(tk, dwMappingFlags, szImportName, mrImportDLL);
}

HRESULT MetadataInterfaces::DeletePinvokeMap(mdToken tk)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->DeletePinvokeMap(tk);
}

HRESULT MetadataInterfaces::SetCustomAttributeValue(mdCustomAttribute pcv,
                                                                                 void const* pCustomAttribute,
                                                                                 ULONG cbCustomAttribute)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->SetCustomAttributeValue(pcv, pCustomAttribute, cbCustomAttribute);
}

HRESULT MetadataInterfaces::SetFieldProps(mdFieldDef fd, DWORD dwFieldFlags,
                                                                       DWORD dwCPlusTypeFlag, void const* pValue,
                                                                       ULONG cchValue)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->SetFieldProps(fd, dwFieldFlags, dwCPlusTypeFlag, pValue, cchValue);
}

HRESULT MetadataInterfaces::SetPropertyProps(mdProperty pr, DWORD dwPropFlags,
                                                                          DWORD dwCPlusTypeFlag, void const* pValue,
                                                                          ULONG cchValue, mdMethodDef mdSetter,
                                                                          mdMethodDef mdGetter,
                                                                          mdMethodDef rmdOtherMethods[])
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->SetPropertyProps(pr, dwPropFlags, dwCPlusTypeFlag, pValue, cchValue, mdSetter, mdGetter, rmdOtherMethods);
}

HRESULT MetadataInterfaces::SetParamProps(mdParamDef pd, LPCWSTR szName,
                                                                       DWORD dwParamFlags, DWORD dwCPlusTypeFlag,
                                                                       void const* pValue, ULONG cchValue)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->SetParamProps(pd, szName, dwParamFlags, dwCPlusTypeFlag, pValue, cchValue);
}

HRESULT MetadataInterfaces::DefineSecurityAttributeSet(mdToken tkObj,
                                                                                    COR_SECATTR rSecAttrs[],
                                                                                    ULONG cSecAttrs,
                                                                                    ULONG* pulErrorAttr)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->DefineSecurityAttributeSet(tkObj, rSecAttrs, cSecAttrs, pulErrorAttr);
}

HRESULT MetadataInterfaces::ApplyEditAndContinue(IUnknown* pImport)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->ApplyEditAndContinue(pImport);
}

HRESULT MetadataInterfaces::TranslateSigWithScope(
    IMetaDataAssemblyImport* pAssemImport, const void* pbHashValue, ULONG cbHashValue, IMetaDataImport* import,
    PCCOR_SIGNATURE pbSigBlob, ULONG cbSigBlob, IMetaDataAssemblyEmit* pAssemEmit, IMetaDataEmit* emit,
    PCOR_SIGNATURE pvTranslatedSig, ULONG cbTranslatedSigMax, ULONG* pcbTranslatedSig)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)
        ->TranslateSigWithScope(pAssemImport, pbHashValue, cbHashValue, import, pbSigBlob, cbSigBlob, pAssemEmit, emit,
                                pvTranslatedSig, cbTranslatedSigMax, pcbTranslatedSig);
}

HRESULT MetadataInterfaces::SetMethodImplFlags(mdMethodDef md, DWORD dwImplFlags)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->SetMethodImplFlags(md, dwImplFlags);
}

HRESULT MetadataInterfaces::SetFieldRVA(mdFieldDef fd, ULONG ulRVA)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->SetFieldRVA(fd, ulRVA);
}

HRESULT MetadataInterfaces::Merge(IMetaDataImport* pImport, IMapToken* pHostMapToken,
                                                               IUnknown* pHandler)
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->Merge(pImport, pHostMapToken, pHandler);
}

HRESULT MetadataInterfaces::MergeEnd()
{
    return m_metadataInterfaces.As<IMetaDataEmit>(IID_IMetaDataEmit)->MergeEnd();
}

HRESULT MetadataInterfaces::GetDeltaSaveSize(CorSaveSize fSave, DWORD* pdwSaveSize)
{
    return m_metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit2)->GetDeltaSaveSize(fSave, pdwSaveSize);
}

HRESULT MetadataInterfaces::SaveDelta(LPCWSTR szFile, DWORD dwSaveFlags)
{
    return m_metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit2)->SaveDelta(szFile, dwSaveFlags);
}

HRESULT MetadataInterfaces::SaveDeltaToStream(IStream* pIStream, DWORD dwSaveFlags)
{
    return m_metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit2)->SaveDeltaToStream(pIStream, dwSaveFlags);
}

HRESULT MetadataInterfaces::SaveDeltaToMemory(void* pbData, ULONG cbData)
{
    return m_metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit2)->SaveDeltaToMemory(pbData, cbData);
}

HRESULT MetadataInterfaces::SetGenericParamProps(mdGenericParam gp, DWORD dwParamFlags,
                                                                              LPCWSTR szName, DWORD reserved,
                                                                              mdToken rtkConstraints[])
{
    return m_metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit2)
        ->SetGenericParamProps(gp, dwParamFlags, szName, reserved, rtkConstraints);
}

HRESULT MetadataInterfaces::ResetENCLog()
{
    return m_metadataInterfaces.As<IMetaDataEmit2>(IID_IMetaDataEmit2)->ResetENCLog();
}

void MetadataInterfaces::CloseEnum(HCORENUM hEnum)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)->CloseEnum(hEnum);
}

HRESULT MetadataInterfaces::CountEnum(HCORENUM hEnum, ULONG* pulCount)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)->CountEnum(hEnum, pulCount);
}

HRESULT MetadataInterfaces::ResetEnum(HCORENUM hEnum, ULONG ulPos)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)->ResetEnum(hEnum, ulPos);
}

HRESULT MetadataInterfaces::EnumTypeDefs(HCORENUM* phEnum, mdTypeDef rTypeDefs[],
                                                                      ULONG cMax, ULONG* pcTypeDefs)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumTypeDefs(phEnum, rTypeDefs, cMax, pcTypeDefs);
}

HRESULT MetadataInterfaces::EnumInterfaceImpls(HCORENUM* phEnum, mdTypeDef td,
                                                                            mdInterfaceImpl rImpls[], ULONG cMax,
                                                                            ULONG* pcImpls)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumInterfaceImpls(phEnum, td, rImpls, cMax, pcImpls);
}

HRESULT MetadataInterfaces::EnumTypeRefs(HCORENUM* phEnum, mdTypeRef rTypeRefs[],
                                                                      ULONG cMax, ULONG* pcTypeRefs)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumTypeRefs(phEnum, rTypeRefs, cMax, pcTypeRefs);
}

HRESULT MetadataInterfaces::FindTypeDefByName(LPCWSTR szTypeDef, mdToken tkEnclosingClass,
                                                                           mdTypeDef* ptd)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->FindTypeDefByName(szTypeDef, tkEnclosingClass, ptd);
}

HRESULT MetadataInterfaces::GetScopeProps(LPWSTR szName, ULONG cchName, ULONG* pchName,
                                                                       GUID* pmvid)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetScopeProps(szName, cchName, pchName, pmvid);
}

HRESULT MetadataInterfaces::GetModuleFromScope(mdModule* pmd)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)->GetModuleFromScope(pmd);
}

HRESULT MetadataInterfaces::GetTypeDefProps(mdTypeDef td, LPWSTR szTypeDef,
                                                                         ULONG cchTypeDef, ULONG* pchTypeDef,
                                                                         DWORD* pdwTypeDefFlags, mdToken* ptkExtends)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetTypeDefProps(td, szTypeDef, cchTypeDef, pchTypeDef, pdwTypeDefFlags, ptkExtends);
}

HRESULT MetadataInterfaces::GetInterfaceImplProps(mdInterfaceImpl iiImpl,
                                                                               mdTypeDef* pClass, mdToken* ptkIface)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetInterfaceImplProps(iiImpl, pClass, ptkIface);
}

HRESULT MetadataInterfaces::GetTypeRefProps(mdTypeRef tr, mdToken* ptkResolutionScope,
                                                                         LPWSTR szName, ULONG cchName, ULONG* pchName)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetTypeRefProps(tr, ptkResolutionScope, szName, cchName, pchName);
}

HRESULT MetadataInterfaces::ResolveTypeRef(mdTypeRef tr, const IID& riid,
                                                                        IUnknown** ppIScope, mdTypeDef* ptd)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)->ResolveTypeRef(tr, riid, ppIScope, ptd);
}

HRESULT MetadataInterfaces::EnumMembers(HCORENUM* phEnum, mdTypeDef cl, mdToken rMembers[],
                                                                     ULONG cMax, ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumMembers(phEnum, cl, rMembers, cMax, pcTokens);
}

HRESULT MetadataInterfaces::EnumMembersWithName(HCORENUM* phEnum, mdTypeDef cl,
                                                                             LPCWSTR szName, mdToken rMembers[],
                                                                             ULONG cMax, ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumMembersWithName(phEnum, cl, szName, rMembers, cMax, pcTokens);
}

HRESULT MetadataInterfaces::EnumMethods(HCORENUM* phEnum, mdTypeDef cl,
                                                                     mdMethodDef rMethods[], ULONG cMax,
                                                                     ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumMethods(phEnum, cl, rMethods, cMax, pcTokens);
}

HRESULT MetadataInterfaces::EnumMethodsWithName(HCORENUM* phEnum, mdTypeDef cl,
                                                                             LPCWSTR szName, mdMethodDef rMethods[],
                                                                             ULONG cMax, ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumMethodsWithName(phEnum, cl, szName, rMethods, cMax, pcTokens);
}

HRESULT MetadataInterfaces::EnumFields(HCORENUM* phEnum, mdTypeDef cl,
                                                                    mdFieldDef rFields[], ULONG cMax, ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumFields(phEnum, cl, rFields, cMax, pcTokens);
}

HRESULT MetadataInterfaces::EnumFieldsWithName(HCORENUM* phEnum, mdTypeDef cl,
                                                                            LPCWSTR szName, mdFieldDef rFields[],
                                                                            ULONG cMax, ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumFieldsWithName(phEnum, cl, szName, rFields, cMax, pcTokens);
}

HRESULT MetadataInterfaces::EnumParams(HCORENUM* phEnum, mdMethodDef mb,
                                                                    mdParamDef rParams[], ULONG cMax, ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumParams(phEnum, mb, rParams, cMax, pcTokens);
}

HRESULT MetadataInterfaces::EnumMemberRefs(HCORENUM* phEnum, mdToken tkParent,
                                                                        mdMemberRef rMemberRefs[], ULONG cMax,
                                                                        ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumMemberRefs(phEnum, tkParent, rMemberRefs, cMax, pcTokens);
}

HRESULT MetadataInterfaces::EnumMethodImpls(HCORENUM* phEnum, mdTypeDef td,
                                                                         mdToken rMethodBody[], mdToken rMethodDecl[],
                                                                         ULONG cMax, ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumMethodImpls(phEnum, td, rMethodBody, rMethodDecl, cMax, pcTokens);
}

HRESULT MetadataInterfaces::EnumPermissionSets(HCORENUM* phEnum, mdToken tk,
                                                                            DWORD dwActions, mdPermission rPermission[],
                                                                            ULONG cMax, ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumPermissionSets(phEnum, tk, dwActions, rPermission, cMax, pcTokens);
}

HRESULT MetadataInterfaces::FindMember(mdTypeDef td, LPCWSTR szName,
                                                                    PCCOR_SIGNATURE pvSigBlob, ULONG cbSigBlob,
                                                                    mdToken* pmb)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->FindMember(td, szName, pvSigBlob, cbSigBlob, pmb);
}

HRESULT MetadataInterfaces::FindMethod(mdTypeDef td, LPCWSTR szName,
                                                                    PCCOR_SIGNATURE pvSigBlob, ULONG cbSigBlob,
                                                                    mdMethodDef* pmb)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->FindMethod(td, szName, pvSigBlob, cbSigBlob, pmb);
}

HRESULT MetadataInterfaces::FindField(mdTypeDef td, LPCWSTR szName,
                                                                   PCCOR_SIGNATURE pvSigBlob, ULONG cbSigBlob,
                                                                   mdFieldDef* pmb)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->FindField(td, szName, pvSigBlob, cbSigBlob, pmb);
}

HRESULT MetadataInterfaces::FindMemberRef(mdTypeRef td, LPCWSTR szName,
                                                                       PCCOR_SIGNATURE pvSigBlob, ULONG cbSigBlob,
                                                                       mdMemberRef* pmr)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->FindMemberRef(td, szName, pvSigBlob, cbSigBlob, pmr);
}

HRESULT MetadataInterfaces::GetMethodProps(mdMethodDef mb, mdTypeDef* pClass,
                                                                        LPWSTR szMethod, ULONG cchMethod,
                                                                        ULONG* pchMethod, DWORD* pdwAttr,
                                                                        PCCOR_SIGNATURE* ppvSigBlob, ULONG* pcbSigBlob,
                                                                        ULONG* pulCodeRVA, DWORD* pdwImplFlags)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetMethodProps(mb, pClass, szMethod, cchMethod, pchMethod, pdwAttr, ppvSigBlob, pcbSigBlob, pulCodeRVA,
                         pdwImplFlags);
}

HRESULT MetadataInterfaces::GetMemberRefProps(mdMemberRef mr, mdToken* ptk,
                                                                           LPWSTR szMember, ULONG cchMember,
                                                                           ULONG* pchMember,
                                                                           PCCOR_SIGNATURE* ppvSigBlob, ULONG* pbSig)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetMemberRefProps(mr, ptk, szMember, cchMember, pchMember, ppvSigBlob, pbSig);
}

HRESULT MetadataInterfaces::EnumProperties(HCORENUM* phEnum, mdTypeDef td,
                                                                        mdProperty rProperties[], ULONG cMax,
                                                                        ULONG* pcProperties)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumProperties(phEnum, td, rProperties, cMax, pcProperties);
}

HRESULT MetadataInterfaces::EnumEvents(HCORENUM* phEnum, mdTypeDef td, mdEvent rEvents[],
                                                                    ULONG cMax, ULONG* pcEvents)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumEvents(phEnum, td, rEvents, cMax, pcEvents);
}

HRESULT MetadataInterfaces::GetEventProps(
    mdEvent ev, mdTypeDef* pClass, LPCWSTR szEvent, ULONG cchEvent, ULONG* pchEvent, DWORD* pdwEventFlags,
    mdToken* ptkEventType, mdMethodDef* pmdAddOn, mdMethodDef* pmdRemoveOn, mdMethodDef* pmdFire,
    mdMethodDef rmdOtherMethod[], ULONG cMax, ULONG* pcOtherMethod)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetEventProps(ev, pClass, szEvent, cchEvent, pchEvent, pdwEventFlags, ptkEventType, pmdAddOn, pmdRemoveOn,
                        pmdFire, rmdOtherMethod, cMax, pcOtherMethod);
}

HRESULT MetadataInterfaces::EnumMethodSemantics(HCORENUM* phEnum, mdMethodDef mb,
                                                                             mdToken rEventProp[], ULONG cMax,
                                                                             ULONG* pcEventProp)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumMethodSemantics(phEnum, mb, rEventProp, cMax, pcEventProp);
}

HRESULT MetadataInterfaces::GetMethodSemantics(mdMethodDef mb, mdToken tkEventProp,
                                                                            DWORD* pdwSemanticsFlags)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetMethodSemantics(mb, tkEventProp, pdwSemanticsFlags);
}

HRESULT MetadataInterfaces::GetClassLayout(mdTypeDef td, DWORD* pdwPackSize,
                                                                        COR_FIELD_OFFSET rFieldOffset[], ULONG cMax,
                                                                        ULONG* pcFieldOffset, ULONG* pulClassSize)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetClassLayout(td, pdwPackSize, rFieldOffset, cMax, pcFieldOffset, pulClassSize);
}

HRESULT MetadataInterfaces::GetFieldMarshal(mdToken tk, PCCOR_SIGNATURE* ppvNativeType,
                                                                         ULONG* pcbNativeType)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetFieldMarshal(tk, ppvNativeType, pcbNativeType);
}

HRESULT MetadataInterfaces::GetRVA(mdToken tk, ULONG* pulCodeRVA, DWORD* pdwImplFlags)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)->GetRVA(tk, pulCodeRVA, pdwImplFlags);
}

HRESULT MetadataInterfaces::GetPermissionSetProps(mdPermission pm, DWORD* pdwAction,
                                                                               void const** ppvPermission,
                                                                               ULONG* pcbPermission)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetPermissionSetProps(pm, pdwAction, ppvPermission, pcbPermission);
}

HRESULT MetadataInterfaces::GetSigFromToken(mdSignature mdSig, PCCOR_SIGNATURE* ppvSig,
                                                                         ULONG* pcbSig)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)->GetSigFromToken(mdSig, ppvSig, pcbSig);
}

HRESULT MetadataInterfaces::GetModuleRefProps(mdModuleRef mur, LPWSTR szName,
                                                                           ULONG cchName, ULONG* pchName)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetModuleRefProps(mur, szName, cchName, pchName);
}

HRESULT MetadataInterfaces::EnumModuleRefs(HCORENUM* phEnum, mdModuleRef rModuleRefs[],
                                                                        ULONG cmax, ULONG* pcModuleRefs)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumModuleRefs(phEnum, rModuleRefs, cmax, pcModuleRefs);
}

HRESULT MetadataInterfaces::GetTypeSpecFromToken(mdTypeSpec typespec,
                                                                              PCCOR_SIGNATURE* ppvSig, ULONG* pcbSig)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetTypeSpecFromToken(typespec, ppvSig, pcbSig);
}

HRESULT MetadataInterfaces::GetNameFromToken(mdToken tk, MDUTF8CSTR* pszUtf8NamePtr)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)->GetNameFromToken(tk, pszUtf8NamePtr);
}

HRESULT MetadataInterfaces::EnumUnresolvedMethods(HCORENUM* phEnum, mdToken rMethods[],
                                                                               ULONG cMax, ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumUnresolvedMethods(phEnum, rMethods, cMax, pcTokens);
}

HRESULT MetadataInterfaces::GetUserString(mdString stk, LPWSTR szString, ULONG cchString,
                                                                       ULONG* pchString)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetUserString(stk, szString, cchString, pchString);
}

HRESULT MetadataInterfaces::GetPinvokeMap(mdToken tk, DWORD* pdwMappingFlags,
                                                                       LPWSTR szImportName, ULONG cchImportName,
                                                                       ULONG* pchImportName, mdModuleRef* pmrImportDLL)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetPinvokeMap(tk, pdwMappingFlags, szImportName, cchImportName, pchImportName, pmrImportDLL);
}

HRESULT MetadataInterfaces::EnumSignatures(HCORENUM* phEnum, mdSignature rSignatures[],
                                                                        ULONG cmax, ULONG* pcSignatures)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumSignatures(phEnum, rSignatures, cmax, pcSignatures);
}

HRESULT MetadataInterfaces::EnumTypeSpecs(HCORENUM* phEnum, mdTypeSpec rTypeSpecs[],
                                                                       ULONG cmax, ULONG* pcTypeSpecs)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumTypeSpecs(phEnum, rTypeSpecs, cmax, pcTypeSpecs);
}

HRESULT MetadataInterfaces::EnumUserStrings(HCORENUM* phEnum, mdString rStrings[],
                                                                         ULONG cmax, ULONG* pcStrings)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumUserStrings(phEnum, rStrings, cmax, pcStrings);
}

HRESULT MetadataInterfaces::GetParamForMethodIndex(mdMethodDef md, ULONG ulParamSeq,
                                                                                mdParamDef* ppd)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)->GetParamForMethodIndex(md, ulParamSeq, ppd);
}

HRESULT MetadataInterfaces::EnumCustomAttributes(HCORENUM* phEnum, mdToken tk,
                                                                              mdToken tkType,
                                                                              mdCustomAttribute rCustomAttributes[],
                                                                              ULONG cMax, ULONG* pcCustomAttributes)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->EnumCustomAttributes(phEnum, tk, tkType, rCustomAttributes, cMax, pcCustomAttributes);
}

HRESULT MetadataInterfaces::GetCustomAttributeProps(mdCustomAttribute cv, mdToken* ptkObj,
                                                                                 mdToken* ptkType, void const** ppBlob,
                                                                                 ULONG* pcbSize)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetCustomAttributeProps(cv, ptkObj, ptkType, ppBlob, pcbSize);
}

HRESULT MetadataInterfaces::FindTypeRef(mdToken tkResolutionScope, LPCWSTR szName,
                                                                     mdTypeRef* ptr)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)->FindTypeRef(tkResolutionScope, szName, ptr);
}

HRESULT MetadataInterfaces::GetMemberProps(mdToken mb, mdTypeDef* pClass, LPWSTR szMember,
                                                                        ULONG cchMember, ULONG* pchMember,
                                                                        DWORD* pdwAttr, PCCOR_SIGNATURE* ppvSigBlob,
                                                                        ULONG* pcbSigBlob, ULONG* pulCodeRVA,
                                                                        DWORD* pdwImplFlags, DWORD* pdwCPlusTypeFlag,
                                                                        UVCP_CONSTANT* ppValue, ULONG* pcchValue)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetMemberProps(mb, pClass, szMember, cchMember, pchMember, pdwAttr, ppvSigBlob, pcbSigBlob, pulCodeRVA,
                         pdwImplFlags, pdwCPlusTypeFlag, ppValue, pcchValue);
}

HRESULT MetadataInterfaces::GetFieldProps(mdFieldDef mb, mdTypeDef* pClass, LPWSTR szField,
                                                                       ULONG cchField, ULONG* pchField, DWORD* pdwAttr,
                                                                       PCCOR_SIGNATURE* ppvSigBlob, ULONG* pcbSigBlob,
                                                                       DWORD* pdwCPlusTypeFlag, UVCP_CONSTANT* ppValue,
                                                                       ULONG* pcchValue)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetFieldProps(mb, pClass, szField, cchField, pchField, pdwAttr, ppvSigBlob, pcbSigBlob, pdwCPlusTypeFlag,
                        ppValue, pcchValue);
}

HRESULT MetadataInterfaces::GetPropertyProps(
    mdProperty prop, mdTypeDef* pClass, LPCWSTR szProperty, ULONG cchProperty, ULONG* pchProperty, DWORD* pdwPropFlags,
    PCCOR_SIGNATURE* ppvSig, ULONG* pbSig, DWORD* pdwCPlusTypeFlag, UVCP_CONSTANT* ppDefaultValue,
    ULONG* pcchDefaultValue, mdMethodDef* pmdSetter, mdMethodDef* pmdGetter, mdMethodDef rmdOtherMethod[], ULONG cMax,
    ULONG* pcOtherMethod)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetPropertyProps(prop, pClass, szProperty, cchProperty, pchProperty, pdwPropFlags, ppvSig, pbSig,
                           pdwCPlusTypeFlag, ppDefaultValue, pcchDefaultValue, pmdSetter, pmdGetter, rmdOtherMethod,
                           cMax, pcOtherMethod);
}

HRESULT MetadataInterfaces::GetParamProps(mdParamDef tk, mdMethodDef* pmd,
                                                                       ULONG* pulSequence, LPWSTR szName, ULONG cchName,
                                                                       ULONG* pchName, DWORD* pdwAttr,
                                                                       DWORD* pdwCPlusTypeFlag, UVCP_CONSTANT* ppValue,
                                                                       ULONG* pcchValue)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetParamProps(tk, pmd, pulSequence, szName, cchName, pchName, pdwAttr, pdwCPlusTypeFlag, ppValue, pcchValue);
}

HRESULT MetadataInterfaces::GetCustomAttributeByName(mdToken tkObj, LPCWSTR szName,
                                                                                  const void** ppData, ULONG* pcbData)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetCustomAttributeByName(tkObj, szName, ppData, pcbData);
}

BOOL MetadataInterfaces::IsValidToken(mdToken tk)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)->IsValidToken(tk);
}

HRESULT MetadataInterfaces::GetNestedClassProps(mdTypeDef tdNestedClass,
                                                                             mdTypeDef* ptdEnclosingClass)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetNestedClassProps(tdNestedClass, ptdEnclosingClass);
}

HRESULT MetadataInterfaces::GetNativeCallConvFromSig(void const* pvSig, ULONG cbSig,
                                                                                  ULONG* pCallConv)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)
        ->GetNativeCallConvFromSig(pvSig, cbSig, pCallConv);
}

HRESULT MetadataInterfaces::IsGlobal(mdToken pd, int* pbGlobal)
{
    return m_metadataInterfaces.As<IMetaDataImport>(IID_IMetaDataImport)->IsGlobal(pd, pbGlobal);
}

HRESULT MetadataInterfaces::EnumGenericParams(HCORENUM* phEnum, mdToken tk,
                                                                           mdGenericParam rGenericParams[], ULONG cMax,
                                                                           ULONG* pcGenericParams)
{
    return m_metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport2)
        ->EnumGenericParams(phEnum, tk, rGenericParams, cMax, pcGenericParams);
}

HRESULT MetadataInterfaces::GetGenericParamProps(mdGenericParam gp, ULONG* pulParamSeq,
                                                                              DWORD* pdwParamFlags, mdToken* ptOwner,
                                                                              DWORD* reserved, LPWSTR wzname,
                                                                              ULONG cchName, ULONG* pchName)
{
    return m_metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport2)
        ->GetGenericParamProps(gp, pulParamSeq, pdwParamFlags, ptOwner, reserved, wzname, cchName, pchName);
}

HRESULT MetadataInterfaces::GetMethodSpecProps(mdMethodSpec mi, mdToken* tkParent,
                                                                            PCCOR_SIGNATURE* ppvSigBlob,
                                                                            ULONG* pcbSigBlob)
{
    return m_metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport2)
        ->GetMethodSpecProps(mi, tkParent, ppvSigBlob, pcbSigBlob);
}

HRESULT MetadataInterfaces::EnumGenericParamConstraints(
    HCORENUM* phEnum, mdGenericParam tk, mdGenericParamConstraint rGenericParamConstraints[], ULONG cMax,
    ULONG* pcGenericParamConstraints)
{
    return m_metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport2)
        ->EnumGenericParamConstraints(phEnum, tk, rGenericParamConstraints, cMax, pcGenericParamConstraints);
}

HRESULT MetadataInterfaces::GetGenericParamConstraintProps(mdGenericParamConstraint gpc,
                                                                                        mdGenericParam* ptGenericParam,
                                                                                        mdToken* ptkConstraintType)
{
    return m_metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport2)
        ->GetGenericParamConstraintProps(gpc, ptGenericParam, ptkConstraintType);
}

HRESULT MetadataInterfaces::GetPEKind(DWORD* pdwPEKind, DWORD* pdwMAchine)
{
    return m_metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport2)->GetPEKind(pdwPEKind, pdwMAchine);
}

HRESULT MetadataInterfaces::GetVersionString(LPWSTR pwzBuf, DWORD ccBufSize,
                                                                          DWORD* pccBufSize)
{
    return m_metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport2)
        ->GetVersionString(pwzBuf, ccBufSize, pccBufSize);
}

HRESULT MetadataInterfaces::EnumMethodSpecs(HCORENUM* phEnum, mdToken tk,
                                                                         mdMethodSpec rMethodSpecs[], ULONG cMax,
                                                                         ULONG* pcMethodSpecs)
{
    return m_metadataInterfaces.As<IMetaDataImport2>(IID_IMetaDataImport2)
        ->EnumMethodSpecs(phEnum, tk, rMethodSpecs, cMax, pcMethodSpecs);
}

HRESULT MetadataInterfaces::UnmarkAll()
{
    return m_metadataInterfaces.As<IMetaDataFilter>(IID_IMetaDataFilter)->UnmarkAll();
}

HRESULT MetadataInterfaces::MarkToken(mdToken tk)
{
    return m_metadataInterfaces.As<IMetaDataFilter>(IID_IMetaDataFilter)->MarkToken(tk);
}

HRESULT MetadataInterfaces::IsTokenMarked(mdToken tk, BOOL* pIsMarked)
{
    return m_metadataInterfaces.As<IMetaDataFilter>(IID_IMetaDataFilter)->IsTokenMarked(tk, pIsMarked);
}

HRESULT MetadataInterfaces::SetAssemblyProps(mdAssembly pma, const void* pbPublicKey,
                                                                          ULONG cbPublicKey, ULONG ulHashAlgId,
                                                                          LPCWSTR szName,
                                                                          const ASSEMBLYMETADATA* pMetaData,
                                                                          DWORD dwAssemblyFlags)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit)
        ->SetAssemblyProps(pma, pbPublicKey, cbPublicKey, ulHashAlgId, szName, pMetaData, dwAssemblyFlags);
}

HRESULT MetadataInterfaces::SetAssemblyRefProps(
    mdAssemblyRef ar, const void* pbPublicKeyOrToken, ULONG cbPublicKeyOrToken, LPCWSTR szName,
    const ASSEMBLYMETADATA* pMetaData, const void* pbHashValue, ULONG cbHashValue, DWORD dwAssemblyRefFlags)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit)
        ->SetAssemblyRefProps(ar, pbPublicKeyOrToken, cbPublicKeyOrToken, szName, pMetaData, pbHashValue, cbHashValue,
                              dwAssemblyRefFlags);
}

HRESULT MetadataInterfaces::SetFileProps(mdFile file, const void* pbHashValue,
                                                                      ULONG cbHashValue, DWORD dwFileFlags)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit)
        ->SetFileProps(file, pbHashValue, cbHashValue, dwFileFlags);
}

HRESULT MetadataInterfaces::SetExportedTypeProps(mdExportedType ct,
                                                                              mdToken tkImplementation,
                                                                              mdTypeDef tkTypeDef,
                                                                              DWORD dwExportedTypeFlags)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit)
        ->SetExportedTypeProps(ct, tkImplementation, tkTypeDef, dwExportedTypeFlags);
}

HRESULT MetadataInterfaces::SetManifestResourceProps(mdManifestResource mr,
                                                                                  mdToken tkImplementation,
                                                                                  DWORD dwOffset, DWORD dwResourceFlags)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyEmit>(IID_IMetaDataAssemblyEmit)
        ->SetManifestResourceProps(mr, tkImplementation, dwOffset, dwResourceFlags);
}

HRESULT MetadataInterfaces::GetAssemblyProps(mdAssembly mda, const void** ppbPublicKey,
                                                                          ULONG* pcbPublicKey, ULONG* pulHashAlgId,
                                                                          LPWSTR szName, ULONG cchName, ULONG* pchName,
                                                                          ASSEMBLYMETADATA* pMetaData,
                                                                          DWORD* pdwAssemblyFlags)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport)
        ->GetAssemblyProps(mda, ppbPublicKey, pcbPublicKey, pulHashAlgId, szName, cchName, pchName, pMetaData,
                           pdwAssemblyFlags);
}

HRESULT MetadataInterfaces::GetAssemblyRefProps(
    mdAssemblyRef mdar, const void** ppbPublicKeyOrToken, ULONG* pcbPublicKeyOrToken, LPWSTR szName, ULONG cchName,
    ULONG* pchName, ASSEMBLYMETADATA* pMetaData, const void** ppbHashValue, ULONG* pcbHashValue,
    DWORD* pdwAssemblyRefFlags)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport)
        ->GetAssemblyRefProps(mdar, ppbPublicKeyOrToken, pcbPublicKeyOrToken, szName, cchName, pchName, pMetaData,
                              ppbHashValue, pcbHashValue, pdwAssemblyRefFlags);
}

HRESULT MetadataInterfaces::GetFileProps(mdFile mdf, LPWSTR szName, ULONG cchName,
                                                                      ULONG* pchName, const void** ppbHashValue,
                                                                      ULONG* pcbHashValue, DWORD* pdwFileFlags)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport)
        ->GetFileProps(mdf, szName, cchName, pchName, ppbHashValue, pcbHashValue, pdwFileFlags);
}

HRESULT MetadataInterfaces::GetExportedTypeProps(mdExportedType mdct, LPWSTR szName,
                                                                              ULONG cchName, ULONG* pchName,
                                                                              mdToken* ptkImplementation,
                                                                              mdTypeDef* ptkTypeDef,
                                                                              DWORD* pdwExportedTypeFlags)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport)
        ->GetExportedTypeProps(mdct, szName, cchName, pchName, ptkImplementation, ptkTypeDef, pdwExportedTypeFlags);
}

HRESULT MetadataInterfaces::GetManifestResourceProps(
    mdManifestResource mdmr, LPWSTR szName, ULONG cchName, ULONG* pchName, mdToken* ptkImplementation, DWORD* pdwOffset,
    DWORD* pdwResourceFlags)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport)
        ->GetManifestResourceProps(mdmr, szName, cchName, pchName, ptkImplementation, pdwOffset, pdwResourceFlags);
}

HRESULT MetadataInterfaces::EnumAssemblyRefs(HCORENUM* phEnum,
                                                                          mdAssemblyRef rAssemblyRefs[], ULONG cMax,
                                                                          ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport)
        ->EnumAssemblyRefs(phEnum, rAssemblyRefs, cMax, pcTokens);
}

HRESULT MetadataInterfaces::EnumFiles(HCORENUM* phEnum, mdFile rFiles[], ULONG cMax,
                                                                   ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport)
        ->EnumFiles(phEnum, rFiles, cMax, pcTokens);
}

HRESULT MetadataInterfaces::EnumExportedTypes(HCORENUM* phEnum,
                                                                           mdExportedType rExportedTypes[], ULONG cMax,
                                                                           ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport)
        ->EnumExportedTypes(phEnum, rExportedTypes, cMax, pcTokens);
}

HRESULT MetadataInterfaces::EnumManifestResources(HCORENUM* phEnum,
                                                                               mdManifestResource rManifestResources[],
                                                                               ULONG cMax, ULONG* pcTokens)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport)
        ->EnumManifestResources(phEnum, rManifestResources, cMax, pcTokens);
}

HRESULT MetadataInterfaces::GetAssemblyFromScope(mdAssembly* ptkAssembly)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport)
        ->GetAssemblyFromScope(ptkAssembly);
}

HRESULT MetadataInterfaces::FindExportedTypeByName(LPCWSTR szName, mdToken mdtExportedType,
                                                                                mdExportedType* ptkExportedType)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport)
        ->FindExportedTypeByName(szName, mdtExportedType, ptkExportedType);
}

HRESULT
MetadataInterfaces::FindManifestResourceByName(LPCWSTR szName,
                                                                            mdManifestResource* ptkManifestResource)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport)
        ->FindManifestResourceByName(szName, ptkManifestResource);
}

HRESULT MetadataInterfaces::FindAssembliesByName(LPCWSTR szAppBase, LPCWSTR szPrivateBin,
                                                                              LPCWSTR szAssemblyName,
                                                                              IUnknown* ppIUnk[], ULONG cMax,
                                                                              ULONG* pcAssemblies)
{
    return m_metadataInterfaces.As<IMetaDataAssemblyImport>(IID_IMetaDataAssemblyImport)
        ->FindAssembliesByName(szAppBase, szPrivateBin, szAssemblyName, ppIUnk, cMax, pcAssemblies);
}

HRESULT MetadataInterfaces::ValidatorInit(DWORD dwModuleType, IUnknown* pUnk)
{
    return m_metadataInterfaces.As<IMetaDataValidate>(IID_IMetaDataValidate)->ValidatorInit(dwModuleType, pUnk);
}

HRESULT MetadataInterfaces::ValidateMetaData()
{
    return m_metadataInterfaces.As<IMetaDataValidate>(IID_IMetaDataValidate)->ValidateMetaData();
}

HRESULT MetadataInterfaces::SetOption(const GUID& optionid, const VARIANT* value)
{
    return m_metadataInterfaces.As<IMetaDataDispenserEx>(IID_IMetaDataDispenserEx)->SetOption(optionid, value);
}

HRESULT MetadataInterfaces::GetOption(const GUID& optionid, VARIANT* pvalue)
{
    return m_metadataInterfaces.As<IMetaDataDispenserEx>(IID_IMetaDataDispenserEx)->GetOption(optionid, pvalue);
}

HRESULT MetadataInterfaces::OpenScopeOnITypeInfo(ITypeInfo* pITI, DWORD dwOpenFlags,
                                                                              const IID& riid, IUnknown** ppIUnk)
{
    return m_metadataInterfaces.As<IMetaDataDispenserEx>(IID_IMetaDataDispenserEx)
        ->OpenScopeOnITypeInfo(pITI, dwOpenFlags, riid, ppIUnk);
}

HRESULT MetadataInterfaces::GetCORSystemDirectory(LPWSTR szBuffer, DWORD cchBuffer,
                                                                               DWORD* pchBuffer)
{
    return m_metadataInterfaces.As<IMetaDataDispenserEx>(IID_IMetaDataDispenserEx)
        ->GetCORSystemDirectory(szBuffer, cchBuffer, pchBuffer);
}

HRESULT MetadataInterfaces::FindAssembly(LPCWSTR szAppBase, LPCWSTR szPrivateBin,
                                                                      LPCWSTR szGlobalBin, LPCWSTR szAssemblyName,
                                                                      LPCWSTR szName, ULONG cchName, ULONG* pcName)
{
    return m_metadataInterfaces.As<IMetaDataDispenserEx>(IID_IMetaDataDispenserEx)
        ->FindAssembly(szAppBase, szPrivateBin, szGlobalBin, szAssemblyName, szName, cchName, pcName);
}

HRESULT MetadataInterfaces::FindAssemblyModule(LPCWSTR szAppBase, LPCWSTR szPrivateBin,
                                                                            LPCWSTR szGlobalBin, LPCWSTR szAssemblyName,
                                                                            LPCWSTR szModuleName, LPWSTR szName,
                                                                            ULONG cchName, ULONG* pcName)
{
    return m_metadataInterfaces.As<IMetaDataDispenserEx>(IID_IMetaDataDispenserEx)
        ->FindAssemblyModule(szAppBase, szPrivateBin, szGlobalBin, szAssemblyName, szModuleName, szName, cchName,
                             pcName);
}

HRESULT MetadataInterfaces::GetString(ULONG RVA, LPWSTR* lpString)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->GetString(RVA, lpString);
}

HRESULT MetadataInterfaces::AllocateMethodBuffer(ULONG cchBuffer, UCHAR** lpBuffer,
                                                                              ULONG* RVA)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->AllocateMethodBuffer(cchBuffer, lpBuffer, RVA);
}

HRESULT MetadataInterfaces::GetMethodBuffer(ULONG RVA, UCHAR** lpBuffer)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->GetMethodBuffer(RVA, lpBuffer);
}

HRESULT MetadataInterfaces::GetIMapTokenIface(IUnknown** pIMapToken)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->GetIMapTokenIface(pIMapToken);
}

HRESULT MetadataInterfaces::GenerateCeeFile()
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->GenerateCeeFile();
}

HRESULT MetadataInterfaces::GetIlSection(HCEESECTION* section)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->GetIlSection(section);
}

HRESULT MetadataInterfaces::GetStringSection(HCEESECTION* section)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->GetStringSection(section);
}

HRESULT MetadataInterfaces::AddSectionReloc(HCEESECTION section, ULONG offset,
                                                                         HCEESECTION relativeTo,
                                                                         CeeSectionRelocType relocType)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->AddSectionReloc(section, offset, relativeTo, relocType);
}

HRESULT MetadataInterfaces::GetSectionCreate(const char* name, DWORD flags,
                                                                          HCEESECTION* section)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->GetSectionCreate(name, flags, section);
}

HRESULT MetadataInterfaces::GetSectionDataLen(HCEESECTION section, ULONG* dataLen)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->GetSectionDataLen(section, dataLen);
}

HRESULT MetadataInterfaces::GetSectionBlock(HCEESECTION section, ULONG len, ULONG align,
                                                                         void** ppBytes)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->GetSectionBlock(section, len, align, ppBytes);
}

HRESULT MetadataInterfaces::TruncateSection(HCEESECTION section, ULONG len)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->TruncateSection(section, len);
}

HRESULT MetadataInterfaces::GenerateCeeMemoryImage(void** ppImage)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->GenerateCeeMemoryImage(ppImage);
}

HRESULT MetadataInterfaces::ComputePointer(HCEESECTION section, ULONG RVA,
                                                                        UCHAR** lpBuffer)
{
    return m_metadataInterfaces.As<ICeeGen>(IID_ICeeGen)->ComputePointer(section, RVA, lpBuffer);
}

HRESULT MetadataInterfaces::GetStringHeapSize(ULONG* pcbStrings)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetStringHeapSize(pcbStrings);
}

HRESULT MetadataInterfaces::GetBlobHeapSize(ULONG* pcbBlobs)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetBlobHeapSize(pcbBlobs);
}

HRESULT MetadataInterfaces::GetGuidHeapSize(ULONG* pcbGuids)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetGuidHeapSize(pcbGuids);
}

HRESULT MetadataInterfaces::GetUserStringHeapSize(ULONG* pcbBlobs)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetUserStringHeapSize(pcbBlobs);
}

HRESULT MetadataInterfaces::GetNumTables(ULONG* pcTables)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetNumTables(pcTables);
}

HRESULT MetadataInterfaces::GetTableIndex(ULONG token, ULONG* pixTbl)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetTableIndex(token, pixTbl);
}

HRESULT MetadataInterfaces::GetTableInfo(ULONG ixTbl, ULONG* pcbRow, ULONG* pcRows,
                                                                      ULONG* pcCols, ULONG* piKey, const char** ppName)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)
        ->GetTableInfo(ixTbl, pcbRow, pcRows, pcCols, piKey, ppName);
}

HRESULT MetadataInterfaces::GetColumnInfo(ULONG ixTbl, ULONG ixCol, ULONG* poCol,
                                                                       ULONG* pcbCol, ULONG* pType, const char** ppName)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)
        ->GetColumnInfo(ixTbl, ixCol, poCol, pcbCol, pType, ppName);
}

HRESULT MetadataInterfaces::GetCodedTokenInfo(ULONG ixCdTkn, ULONG* pcTokens,
                                                                           ULONG** ppTokens, const char** ppName)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)
        ->GetCodedTokenInfo(ixCdTkn, pcTokens, ppTokens, ppName);
}

HRESULT MetadataInterfaces::GetRow(ULONG ixTbl, ULONG rid, void** ppRow)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetRow(ixTbl, rid, ppRow);
}

HRESULT MetadataInterfaces::GetColumn(ULONG ixTbl, ULONG ixCol, ULONG rid, ULONG* pVal)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetColumn(ixTbl, ixCol, rid, pVal);
}

HRESULT MetadataInterfaces::GetString(ULONG ixString, const char** ppString)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetString(ixString, ppString);
}

HRESULT MetadataInterfaces::GetBlob(ULONG ixBlob, ULONG* pcbData, const void** ppData)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetBlob(ixBlob, pcbData, ppData);
}

HRESULT MetadataInterfaces::GetGuid(ULONG ixGuid, const GUID** ppGUID)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetGuid(ixGuid, ppGUID);
}

HRESULT MetadataInterfaces::GetUserString(ULONG ixUserString, ULONG* pcbData,
                                                                       const void** ppData)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetUserString(ixUserString, pcbData, ppData);
}

HRESULT MetadataInterfaces::GetNextString(ULONG ixString, ULONG* pNext)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetNextString(ixString, pNext);
}

HRESULT MetadataInterfaces::GetNextBlob(ULONG ixBlob, ULONG* pNext)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetNextBlob(ixBlob, pNext);
}

HRESULT MetadataInterfaces::GetNextGuid(ULONG ixGuid, ULONG* pNext)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetNextGuid(ixGuid, pNext);
}

HRESULT MetadataInterfaces::GetNextUserString(ULONG ixUserString, ULONG* pNext)
{
    return m_metadataInterfaces.As<IMetaDataTables>(IID_IMetaDataTables)->GetNextUserString(ixUserString, pNext);
}

HRESULT MetadataInterfaces::GetMetaDataStorage(const void** ppvMd, ULONG* pcbMd)
{
    return m_metadataInterfaces.As<IMetaDataTables2>(IID_IMetaDataTables2)->GetMetaDataStorage(ppvMd, pcbMd);
}

HRESULT MetadataInterfaces::GetMetaDataStreamInfo(ULONG ix, const char** ppchName,
                                                                               const void** ppv, ULONG* pcb)
{
    return m_metadataInterfaces.As<IMetaDataTables2>(IID_IMetaDataTables2)
        ->GetMetaDataStreamInfo(ix, ppchName, ppv, pcb);
}

HRESULT MetadataInterfaces::GetFileMapping(const void** ppvData, ULONGLONG* pcbData,
                                                                        DWORD* pdwMappingType)
{
    return m_metadataInterfaces.As<IMetaDataInfo>(IID_IMetaDataInfo)->GetFileMapping(ppvData, pcbData, pdwMappingType);
}

} // namespace instrumented_assembly_generator
