#include "method_info.h"
#include "iast_util.h"
#include "module_info.h"
#include "dataflow_il_rewriter.h"
#include "dataflow_il_analysis.h"
#include "signature_info.h"
#include "aspect.h"
#include "dataflow.h"
#include <unordered_map>

namespace iast
{
    bool GetRestoreOnSecondJitConfigValue()
    {
        static bool restoreOnSecondJit = false; // HdivConfig::Instance.GetEnabled("hdiv.net.ast.jit.restore"_W);
        return restoreOnSecondJit;
    }

    //----------------------------

    TypeInfo::TypeInfo(ModuleInfo* pModuleInfo, mdTypeDef typeDef)
    {
        this->_module = pModuleInfo;
        this->_id = typeDef;
    }
    TypeInfo::~TypeInfo()
    {
        _module = nullptr;
    }

    mdTypeDef TypeInfo::GetTypeDef()
    {
        return _id;
    }
    WSTRING& TypeInfo::GetName()
    {
        if (_name.size() == 0)
        {
            _name = _module->GetTypeName(_id);
        }
        return _name;
    }


    //----------------------------

    MemberRefInfo::MemberRefInfo(ModuleInfo* pModuleInfo, mdMemberRef memberRef)
    {
        this->_module = pModuleInfo;
        this->_id = memberRef;

        if (TypeFromToken(memberRef) == mdtMemberRef)
        {
            WCHAR methodName[1024]; ULONG methodNameLength;
            pModuleInfo->_metadataImport->GetMemberRefProps(memberRef, &_typeDef, methodName, 1024, &methodNameLength, &_pSig, &_nSig);
            _name = methodName;
        }
    }
    MemberRefInfo::~MemberRefInfo()
    {
        _module = nullptr;
        _typeInfo = nullptr;
        _pSig = nullptr;
        DEL(_pSignature);
    }

    mdMemberRef MemberRefInfo::GetMemberId()
    {
        return _id;
    }

    TypeInfo* MemberRefInfo::GetTypeInfo()
    {
        if (!_typeInfo)
        {
            _typeInfo = _module->GetTypeInfo(_typeDef);
        }
        return _typeInfo;
    }
    mdTypeDef MemberRefInfo::GetTypeDef()
    {
        return _typeDef;
    }

    ModuleInfo* MemberRefInfo::GetModuleInfo()
    {
        return _module;
    }
    WSTRING& MemberRefInfo::GetName()
    {
        return _name;
    }

    WSTRING& MemberRefInfo::GetFullName()
    {
        if (_fullName.size() == 0)
        {
            CSGuard lock(&_module->_cs);
            if (_fullName.size() == 0)
            {
                auto fullName = GetTypeName() + WStr("::") + _name;
                auto signature = GetSignature();
                if (signature != nullptr)
                {
                    fullName = signature->CharacterizeMember(fullName);
                }
                _fullName = fullName;
            }
        }
        return _fullName;
    }
    WSTRING MemberRefInfo::GetFullNameWithReturnType()
    {
        auto res = GetFullName();
        auto signature = GetSignature();
        if (signature != nullptr)
        {
            res = signature->GetReturnTypeString() + WStr(" ") + res;
        }
        return res;
    }
    WSTRING& MemberRefInfo::GetTypeName()
    {
        auto type = GetTypeInfo();
        if (type) { return type->GetName(); }
        return EmptyWStr;
    }

    SignatureInfo* MemberRefInfo::GetSignature()
    {
        if (_pSignature == nullptr)
        {
            _pSignature = new SignatureInfo(_module, _pSig, _nSig);
        }
        return _pSignature;
    }
    ULONG MemberRefInfo::GetParameterCount()
    {
        auto signature = GetSignature();
        if (signature != nullptr)
        {
            return (ULONG)(signature->_params.size());
        }
        return 0;
    }
    CorElementType MemberRefInfo::GetReturnCorType()
    {
        auto signature = GetSignature();
        if (signature != nullptr && signature->_returnType != nullptr)
        {
            return  signature->_returnType->GetCorElementType();
        }
        return ELEMENT_TYPE_VOID;
    }

    std::vector<WSTRING> MemberRefInfo::GetCustomAttributes()
    {
        return _module->GetCustomAttributes(_id);
    }

    //----------------------------

    FieldInfo::FieldInfo(ModuleInfo* pModuleInfo, mdFieldDef fieldDef) :
        MemberRefInfo(pModuleInfo, fieldDef)
    {
        WCHAR fieldName[1024]; ULONG fieldNameLength;
        pModuleInfo->_metadataImport->GetFieldProps(fieldDef, &_typeDef, fieldName, 1024, &fieldNameLength, &_attributes,
                                                    &_pSig, &_nSig, nullptr, nullptr, nullptr);
        _name = fieldName;
    }

    mdFieldDef FieldInfo::GetFieldDef()
    {
        return _id;
    }


    //----------------------------

    PropertyInfo::PropertyInfo(ModuleInfo* pModuleInfo, mdProperty propId) :
        MemberRefInfo(pModuleInfo, propId)
    {
        WCHAR methodName[1024];
        ULONG methodNameLength;

        HRESULT hr = pModuleInfo->_metadataImport->GetPropertyProps(
            propId, &_typeDef, methodName, 1024, &methodNameLength, &_methodAttributes, &_pSig, &_nSig, nullptr,
            nullptr, nullptr, &_setter, &_getter, nullptr, 0, nullptr);

        _name = methodName;
    }

    //----------------------------

    MethodSpec::MethodSpec(ModuleInfo* pModuleInfo, mdMethodSpec methodSpec) :
        MemberRefInfo(pModuleInfo, methodSpec)
    {
        mdToken parent;
        pModuleInfo->_metadataImport->GetMethodSpecProps(methodSpec, &parent, &_pSig, &_nSig);
        genericMethod = pModuleInfo->GetMemberRefInfo(parent);
        _typeDef = genericMethod->GetTypeDef();
        _name = genericMethod->GetName();
    }

    mdMethodSpec MethodSpec::GetMethodSpecId()
    {
        return _id;
    }
    SignatureInfo* MethodSpec::GetMethodSpecSignature()
    {
        return MemberRefInfo::GetSignature();
    }
    SignatureInfo* MethodSpec::GetSignature()
    {
        return genericMethod->GetSignature();
    }
    MemberRefInfo* MethodSpec::GetGenericMethod()
    {
        return genericMethod;
    }


    //----------------------------

    MethodInfo::MethodInfo(ModuleInfo* pModuleInfo, mdMethodDef methodDef) :
        MemberRefInfo(pModuleInfo, methodDef)
    {
        WCHAR methodName[1024]; ULONG methodNameLength = 0;
        HRESULT hr = pModuleInfo->_metadataImport->GetMethodProps(methodDef, &_typeDef, methodName, 1024, &methodNameLength, &_methodAttributes, &_pSig, &_nSig, nullptr, nullptr);
        if (methodNameLength > 0 && _typeDef > 0)
        {
            _name = methodName;
        }
        _allowRestoreOnSecondJit = GetRestoreOnSecondJitConfigValue();

        _isExcluded = _module->_dataflow->IsMethodExcluded(GetFullName());
        if (!_isExcluded && _module->_dataflow->HasMethodAttributeExclusions())
        {
            for (auto methodAttribute : GetCustomAttributes())
            {
                if (_module->_dataflow->IsMethodAttributeExcluded(methodAttribute))
                {
                    _isExcluded = true;
                    break;
                }
            }
        }
    }
    MethodInfo::~MethodInfo()
    {
        FreeBuffer();
        DEL(_rewriter);
    }

    mdMethodDef MethodInfo::GetMethodDef()
    {
        return _id;
    }


    WSTRING MethodInfo::GetKey(FunctionID functionId)
    {
        std::stringstream methodKeyBuilder;
        methodKeyBuilder << shared::ToString(GetFullName()) << " " << shared::ToString(_module->GetModuleFullName()) << " (" << Hex(_id) << ") ";
        if (functionId > 0)
        {
            methodKeyBuilder << " Fid( " << Hex((ULONG)functionId) << " ) ";
        }
        if (IsProcessed())
        {
            methodKeyBuilder << " Processed ";
        }
        if (HasChanges())
        {
            methodKeyBuilder << " Changed ";
        }
        if (IsWritten())
        {
            methodKeyBuilder << " Written ";
        }
        return ToWSTRING(methodKeyBuilder.str());
    }

    bool MethodInfo::IsExcluded()
    {
        return _isExcluded;
    }
    bool MethodInfo::IsProcessed()
    {
        return _isProcessed;
    }
    void MethodInfo::SetProcessed()
    {
        _isProcessed = true;
    }
    void MethodInfo::SetInstrumented(bool instrumented)
    {
        _isInstrumented = instrumented;
    }
    bool MethodInfo::IsInstrumented()
    {
        return _isInstrumented;
    }
    bool MethodInfo::HasChanges()
    {
        return _pMethodIL;
    }
    bool MethodInfo::IsWritten()
    {
        return _isWritten;
    }
    void MethodInfo::DisableRestoreOnSecondJit()
    {
        _allowRestoreOnSecondJit = false;
    }
    bool MethodInfo::IsInlineEnabled()
    {
        return _module->IsInlineEnabled() && !_disableInlining && !IsInstrumented();
    }
    void MethodInfo::DisableInlining()
    {
        _disableInlining = true;
    }

    HRESULT MethodInfo::GetILRewriter(ILRewriter** pRewriter, ICorProfilerInfo* pCorProfilerInfo)
    {
        HRESULT hr = S_FALSE;
        if (_rewriter == nullptr)
        {
            hr = S_OK;
            _rewriter = new ILRewriter(this);
            if (!pCorProfilerInfo)
            {
                hr = _rewriter->Import();
            }
            else
            {
                LPCBYTE pMethodIL = nullptr;
                hr = pCorProfilerInfo->GetILFunctionBody(_module->_id, _id, &pMethodIL, nullptr);
                if (SUCCEEDED(hr))
                {
                    hr = _rewriter->Import(pMethodIL);
                }
            }
            if (FAILED(hr))
            {
                DEL(_rewriter);
            }
        }
        *pRewriter = _rewriter;
        return hr;
    }
    HRESULT MethodInfo::CommitILRewriter(bool abort)
    {
        HRESULT hr = S_FALSE;
        if (_rewriter != nullptr && !abort)
        {
            hr = _rewriter->Export();
        }
        DEL(_rewriter);
        return hr;
    }


    HRESULT MethodInfo::GetMethodIL(LPCBYTE* ppMethodIL, ULONG* pnMethodIL, bool original)
    {
        HRESULT hr = S_OK;
        if (!_pOriginalMehodIL)
        {
            auto pCorProfilerInfo = _module->_dataflow->GetCorProfilerInfo();
            hr = pCorProfilerInfo->GetILFunctionBody(_module->_id, _id, &_pOriginalMehodIL, &_nOriginalMehodIL);
        }

        if (ppMethodIL)
        {
            *ppMethodIL = (!original && _pMethodIL) ? (LPCBYTE)_pMethodIL : (LPCBYTE)_pOriginalMehodIL;
        }
        if (pnMethodIL)
        {
            *pnMethodIL = (!original && _pMethodIL) ? _nMethodIL : _nOriginalMehodIL;
        }
        return hr;
    }

    HRESULT MethodInfo::SetMethodIL(ULONG nSize, LPCBYTE pMethodIL, ICorProfilerFunctionControl* pFunctionControl)
    {
        bool isRejit = pFunctionControl != nullptr;
        trace::Logger::Debug("Setting temp IL ", isRejit ? "ReJit" : "  Jit", " for method ", GetKey());
        _isWritten = false;
        FreeBuffer();

        if (pMethodIL == _pOriginalMehodIL || pMethodIL == _pMethodIL)
        {
            trace::Logger::Debug("Same function body detected. Skipping SetMethodIL ", GetKey());
            return S_FALSE;
        }

        bool correct = true;
        std::string verificationFail = "";
        bool dump = trace::Logger::IsDebugEnabled();

        if (!_rewriter)
        {
            trace::Logger::Debug("MethodInfo::SetMethodIL -> No rewritter present. Creating one to verify new IL...");
                
            _rewriter = new ILRewriter(this);
            if (FAILED(_rewriter->Import(pMethodIL)))
            {
                DEL(_rewriter);
                verificationFail = "ILImport failed on new buffer";
                correct = false;
            }
        }
        else
        {
            trace::Logger::Debug("MethodInfo::SetMethodIL -> Rewritter present. Verify new IL...");
        }

        if (_rewriter)
        {
            ILAnalysis analysis(_rewriter);
            correct = analysis.IsStackValid();
            verificationFail = analysis.GetError();
            if (!correct || dump)
            {
                analysis.Dump(isRejit ? "ReJit " : "  Jit ");
            }
        }

        DEL(_rewriter);

        if (correct)
        {
            _nMethodIL = nSize;
            if (pFunctionControl)
            {
                //Copy buffers to our own memory we can later deallocate safely
                _pMethodIL = new BYTE[nSize];
                memcpy(_pMethodIL, pMethodIL, nSize);
            }
            else
            {
                _pMethodIL = (LPBYTE)pMethodIL;
            }

            if (pFunctionControl)
            {
                trace::Logger::Debug("MethodInfo::SetMethodIL -> ReJIT : Setting IL for ", GetFullName());
                ApplyFinalInstrumentation(pFunctionControl);
            }
            else
            {
                trace::Logger::Debug("MethodInfo::SetMethodIL ->   JIT : Setting IL for ", GetFullName());
            }
        }
        else
        {
            FreeBuffer();
            trace::Logger::Info("IL Verification FAILED for ", GetFullName(), " ( ", verificationFail, ")  !!! Discarding changes ...");
        }
        return S_OK;
    }

    HRESULT MethodInfo::ApplyFinalInstrumentation(ICorProfilerFunctionControl* pFunctionControl)
    {
        HRESULT hr = S_OK;
        if (pFunctionControl)
        {
            if (HasChanges())
            {
                hr = pFunctionControl->SetILFunctionBody(_nMethodIL, _pMethodIL);
                trace::Logger::Debug("MethodInfo::ApplyFinalInstrumentation ReJIT from ", _nOriginalMehodIL, " to ",
                                     _nMethodIL, " on ", GetKey(), " hr=", Hex(hr));
            }
            else
            {
                trace::Logger::Debug("MethodInfo::ApplyFinalInstrumentation ReJIT SKIPPED (method did not change)");
            }
        }
        else
        {
            if (IsWritten())
            {
                if (_pOriginalMehodIL)
                {
                    trace::Logger::Debug("MethodInfo::ApplyFinalInstrumentation WARNING, Reinstrumentation detected. Restore DISABLED ", GetKey());
                }
                return hr;
            }
            if (!HasChanges())
            {
                trace::Logger::Error("ERROR: MethodInfo::ApplyFinalInstrumentation should only be called if a method body has been set for this function");
                return E_FAIL;
            }

            auto pCorProfilerInfo = _module->_dataflow->GetCorProfilerInfo();
            IMethodMalloc* pMalloc = nullptr;
            hr = pCorProfilerInfo->GetILFunctionBodyAllocator(_module->_id, &pMalloc);
            if (FAILED(hr))
            {
                trace::Logger::Error("Error on GetILFunctionBodyAllocator");
                return hr;
            }

            PVOID pFunction = pMalloc->Alloc(_nMethodIL);
            memcpy(pFunction, _pMethodIL, _nMethodIL);
            hr = pCorProfilerInfo->SetILFunctionBody(_module->_id, _id, (LPCBYTE)pFunction);
            if (FAILED(hr))
            {
                trace::Logger::Error("Error on SetILFunctionBody");
                return hr;
            }
            _isWritten = true;

            trace::Logger::Debug("MethodInfo::ApplyFinalInstrumentation   JIT from ", _nOriginalMehodIL, " to ", _nMethodIL, " on ", GetKey());
        }

        FreeBuffer(); //To save memory
        return hr;
    }

    void MethodInfo::ReJITCompilationStarted()
    {
        FreeBuffer();
        LPCBYTE originalIL;
        ULONG originalSize;
        GetMethodIL(&originalIL, &originalSize, true);
        _nMethodIL = originalSize;
        _pMethodIL = new BYTE[_nMethodIL];
        memcpy(_pMethodIL, originalIL, originalSize);
    }
    void MethodInfo::ReJITCompilationFinished()
    {
        FreeBuffer();
    }

    void MethodInfo::FreeBuffer()
    {
        _nMethodIL = 0;
        DEL_ARR(_pMethodIL);
    }

    bool MethodInfo::IsPropertyAccessor()
    {
        auto name = shared::ToString(_name);
        return StartsWith(name, "get_") || StartsWith(name, "set_");
    }

    std::vector<WSTRING> MethodInfo::GetCustomAttributes()
    {
        auto res = MemberRefInfo::GetCustomAttributes();
        if (IsPropertyAccessor())
        {
            // Retrieve property Attributes
            auto properties = _module->GetProperties(_typeDef);
            for (auto property : properties)
            {
                if (property->GetGetterId() != _id && property->GetSetterId() != _id)
                {
                    continue;
                }
                AddRange(res, _module->GetCustomAttributes(property->GetPropertyId()));
            }
        }
        return res;
    }

}
