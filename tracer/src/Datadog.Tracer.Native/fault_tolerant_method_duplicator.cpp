#include "fault_tolerant_method_duplicator.h"

#include "cor_profiler.h"
#include "dd_profiler_constants.h"
#include "fault_tolerant_envionrment_variables_util.h"
#include "fault_tolerant_tracker.h"
#include "logger.h"

fault_tolerant::FaultTolerantMethodDuplicator::FaultTolerantMethodDuplicator(CorProfiler* corProfiler,
    std::shared_ptr<trace::RejitHandler> rejit_handler, std::shared_ptr<trace::RejitWorkOffloader> work_offloader):
    m_corProfiler(corProfiler),
    m_rejit_handler(std::move(rejit_handler)),
    m_work_offloader(std::move(work_offloader)),
    is_fault_tolerant_instrumentation_enabled(IsFaultTolerantInstrumentationEnabled())
{
}

void fault_tolerant::FaultTolerantMethodDuplicator::DuplicateOne(const ModuleID moduleId, const trace::ModuleInfo& moduleInfo, ComPtr<IMetaDataImport2> metadataImport, ComPtr<IMetaDataEmit2> metadataEmit, mdTypeDef typeDef, mdMethodDef methodDef) const
{
    const auto caller = GetFunctionInfo(metadataImport, methodDef);
    if (!caller.IsValid())
    {
        Logger::Warn("    * The caller for the methoddef: ", shared::TokenStr(&methodDef), " is not valid!");
        return;
    }

    auto functionInfo = FunctionInfo(caller);
    auto hr = functionInfo.method_signature.TryParse();
    if (FAILED(hr))
    {
        Logger::Warn("    * The method signature: ", functionInfo.method_signature.str(), " cannot be parsed.");
        return;
    }

    if (functionInfo.name == WStr(".ctor") || functionInfo.name == WStr(".cctor"))
    {
        return;
    }

    if (functionInfo.type.extend_from != nullptr &&
        (functionInfo.type.extend_from->name == WStr("System.MulticastDelegate") ||
         functionInfo.type.extend_from->name == WStr("System.Delegate")))
    {
        return;
    }

    // TODO check for Enum and decide what to do

    if (caller.type.name.rfind(L'@') != std::wstring::npos)
    {
        return;
    }

    if (functionInfo.type.isAbstract && !functionInfo.type.IsStaticClass())
    {
        return;
    }

    WCHAR methodName[1024];
    ULONG methodNameLength = 0;
    mdTypeDef _typeDef = 0;
    DWORD _methodAttributes;
    PCCOR_SIGNATURE _pSig = nullptr;
    ULONG _nSig = 0;
    ULONG pulCodeRVA;
    DWORD pdwImplFlags;
    hr = metadataImport->GetMethodProps(methodDef, &_typeDef, methodName, 1024, &methodNameLength,
                                        &_methodAttributes, &_pSig, &_nSig, &pulCodeRVA, &pdwImplFlags);

    if (FAILED(hr))
    {
        Logger::Warn("    * GetMethodProps has failed. MethodDef: ", shared::TokenStr(&methodDef));
        return;
    }

    _methodAttributes |= mdSpecialName;
    _methodAttributes |= mdPrivate;
    _methodAttributes &= ~mdVirtual;
    _methodAttributes |= mdHideBySig;
    //_methodAttributes |= mdFinal;
        
    pdwImplFlags |= miNoInlining;

    mdMethodDef originalTargetMethodDef = mdMethodDefNil;

    auto newMethodName = functionInfo.name + WStr("<Original>");
    newMethodName.erase(std::remove(newMethodName.begin(), newMethodName.end(), L'.'), newMethodName.end());
    newMethodName.erase(std::remove(newMethodName.begin(), newMethodName.end(), L'_'), newMethodName.end());

    hr = metadataEmit->DefineMethod(typeDef, newMethodName.c_str(), _methodAttributes, _pSig, _nSig, pulCodeRVA,
                                    pdwImplFlags, &originalTargetMethodDef);
    if (FAILED(hr))
    {
        Logger::Warn("    * Failed to create new <Original> method. MethodDef: ", shared::TokenStr(&methodDef),
                     " Method Name:",
                     functionInfo.type.name + WStr(".") + newMethodName + WStr(", Module Path: ") +
                     moduleInfo.path);
        return;
    }
    else
    {
        Logger::Info("    * Succeeded in the creation of the new <Original> method. MethodDef: ",
                     shared::TokenStr(&methodDef), " Method Name:",
                     functionInfo.type.name + WStr(".") + newMethodName + WStr(", Module Path: ") +
                     moduleInfo.path);
    }

    mdMethodDef instrumentedTargetMethodDef = mdMethodDefNil;

    newMethodName = functionInfo.name + WStr("<Instrumented>");
    newMethodName.erase(std::remove(newMethodName.begin(), newMethodName.end(), L'.'), newMethodName.end());
    newMethodName.erase(std::remove(newMethodName.begin(), newMethodName.end(), L'_'), newMethodName.end());

    hr = metadataEmit->DefineMethod(typeDef, newMethodName.c_str(), _methodAttributes, _pSig, _nSig, pulCodeRVA,
                                    pdwImplFlags, &instrumentedTargetMethodDef);
    if (FAILED(hr))
    {
        Logger::Warn("    * Failed to create new <Instrumented> method. MethodDef: ", shared::TokenStr(&methodDef),
                     " Method Name:",
                     functionInfo.type.name + WStr(".") + newMethodName + WStr(", Module Path: ") +
                     moduleInfo.path);
        return;
    }
    else
    {
        Logger::Info("    * Succeeded in the creation of the new <Instrumented> method. MethodDef: ",
                     shared::TokenStr(&methodDef), " Method Name:",
                     functionInfo.type.name + WStr(".") + newMethodName + WStr(", Module Path: ") +
                     moduleInfo.path);
    }

    // Define generic params (if exist)
    if ((*_pSig & IMAGE_CEE_CS_CALLCONV_GENERIC) > 0)
    {
        std::vector<mdGenericParam> genericParams;

        auto enumGenericParams = trace::Enumerator<mdGenericParam>(
            [&metadataImport, methodDef](HCORENUM* ptr, mdGenericParam arr[], ULONG max, ULONG* cnt) -> HRESULT {
                return metadataImport->EnumGenericParams(ptr, methodDef, arr, max, cnt);
            },
            [&metadataImport](HCORENUM ptr) -> void { metadataImport->CloseEnum(ptr); });

        auto genericParamsIterator = enumGenericParams.begin();
        for (; genericParamsIterator != enumGenericParams.end(); genericParamsIterator = ++genericParamsIterator)
        {
            const auto genericParam = *genericParamsIterator;
            genericParams.push_back(genericParam);
        }

        bool shouldSkipToNextMethod = false;

        for (int genParam = 0; genParam < static_cast<int>(genericParams.size()); genParam++)
        {
            auto genericParam = genericParams[genParam];

            ULONG pulParamSeq;
            DWORD pdwParamFlags;
            mdToken ptOwner;
            DWORD reserved = 0;
            WCHAR genericParamName[1024];
            ULONG pchName;
            std::vector<mdToken> constraintTypes;
            mdGenericParam newGenericParam;

            auto enumGenericParamConstraints = trace::Enumerator<mdGenericParamConstraint>(
                [&metadataImport, genericParam](HCORENUM* ptr, mdGenericParamConstraint arr[], ULONG max,
                                                ULONG* cnt) -> HRESULT {
                    return metadataImport->EnumGenericParamConstraints(ptr, genericParam, arr, max, cnt);
                },
                [&metadataImport](HCORENUM ptr) -> void { metadataImport->CloseEnum(ptr); });

            auto genericParamConstraintsIterator = enumGenericParamConstraints.begin();
            for (; genericParamConstraintsIterator != enumGenericParamConstraints.end();
                   genericParamConstraintsIterator = ++genericParamConstraintsIterator)
            {
                const auto genericParamConstraint = *genericParamConstraintsIterator;

                mdGenericParam genericParam;
                mdToken constraintType;
                hr = metadataImport->GetGenericParamConstraintProps(genericParamConstraint, &genericParam,
                                                                    &constraintType);

                if (FAILED(hr))
                {
                    Logger::Warn("    * GetGenericParamConstraintProps has failed. MethodDef: ",
                                 shared::TokenStr(&methodDef));
                    shouldSkipToNextMethod = true;
                    break;
                }

                constraintTypes.push_back(constraintType);
            }

            if (shouldSkipToNextMethod)
            {
                break;
            }

            hr = metadataImport->GetGenericParamProps(genericParam, &pulParamSeq, &pdwParamFlags, &ptOwner,
                                                      &reserved, genericParamName, 1024, &pchName);

            if (FAILED(hr))
            {
                Logger::Warn("    * GetGenericParamProps has failed. MethodDef: ", shared::TokenStr(&methodDef));
                shouldSkipToNextMethod = true;
                break;
            }

            if (!constraintTypes.empty())
            {
                std::unique_ptr<mdToken[]> rtkConstraints = std::make_unique<mdToken[]>(constraintTypes.size() + 1);
                std::copy(constraintTypes.begin(), constraintTypes.end(), rtkConstraints.get());
                rtkConstraints[constraintTypes.size()] = 0;
                hr = metadataEmit->DefineGenericParam(instrumentedTargetMethodDef, pulParamSeq, pdwParamFlags,
                                                      genericParamName, reserved, rtkConstraints.get(),
                                                      &newGenericParam);
            }
            else
            {
                hr = metadataEmit->DefineGenericParam(instrumentedTargetMethodDef, pulParamSeq, pdwParamFlags,
                                                      genericParamName, reserved, nullptr, &newGenericParam);
            }

            if (FAILED(hr))
            {
                Logger::Warn("    * DefineGenericParam has failed for instrumentedTargetMethodDef. MethodDef: ",
                             shared::TokenStr(&methodDef));
                shouldSkipToNextMethod = true;
                break;
            }
        }

        if (shouldSkipToNextMethod)
        {
            return;
        }

        for (int genParam = 0; genParam < static_cast<int>(genericParams.size()); genParam++)
        {
            auto genericParam = genericParams[genParam];

            ULONG pulParamSeq;
            DWORD pdwParamFlags;
            mdToken ptOwner;
            DWORD reserved = 0;
            WCHAR genericParamName[1024];
            ULONG pchName;
            std::vector<mdToken> constraintTypes;
            mdGenericParam newGenericParam;

            auto enumGenericParamConstraints = trace::Enumerator<mdGenericParamConstraint>(
                [&metadataImport, genericParam](HCORENUM* ptr, mdGenericParamConstraint arr[], ULONG max,
                                                ULONG* cnt) -> HRESULT {
                    return metadataImport->EnumGenericParamConstraints(ptr, genericParam, arr, max, cnt);
                },
                [&metadataImport](HCORENUM ptr) -> void { metadataImport->CloseEnum(ptr); });

            auto genericParamConstraintsIterator = enumGenericParamConstraints.begin();
            for (; genericParamConstraintsIterator != enumGenericParamConstraints.end();
                   genericParamConstraintsIterator = ++genericParamConstraintsIterator)
            {
                const auto genericParamConstraint = *genericParamConstraintsIterator;

                mdGenericParam genericParam;
                mdToken constraintType;
                hr = metadataImport->GetGenericParamConstraintProps(genericParamConstraint, &genericParam,
                                                                    &constraintType);

                if (FAILED(hr))
                {
                    Logger::Warn("    * GetGenericParamConstraintProps has failed. MethodDef: ",
                                 shared::TokenStr(&methodDef));
                    shouldSkipToNextMethod = true;
                    break;
                }

                constraintTypes.push_back(constraintType);
            }

            if (shouldSkipToNextMethod)
            {
                break;
            }

            hr = metadataImport->GetGenericParamProps(genericParam, &pulParamSeq, &pdwParamFlags, &ptOwner,
                                                      &reserved, genericParamName, 1024, &pchName);

            if (FAILED(hr))
            {
                Logger::Warn("    * GetGenericParamProps has failed. MethodDef: ", shared::TokenStr(&methodDef));
                shouldSkipToNextMethod = true;
                break;
            }

            if (!constraintTypes.empty())
            {
                std::unique_ptr<mdToken[]> rtkConstraints = std::make_unique<mdToken[]>(constraintTypes.size() + 1);
                std::copy(constraintTypes.begin(), constraintTypes.end(), rtkConstraints.get());
                rtkConstraints[constraintTypes.size()] = 0;

                hr = metadataEmit->DefineGenericParam(originalTargetMethodDef, pulParamSeq, pdwParamFlags,
                                                      genericParamName, reserved, rtkConstraints.get(),
                                                      &newGenericParam);
            }
            else
            {
                hr = metadataEmit->DefineGenericParam(originalTargetMethodDef, pulParamSeq, pdwParamFlags,
                                                      genericParamName, reserved, nullptr, &newGenericParam);
            }

            if (FAILED(hr))
            {
                Logger::Warn("    * DefineGenericParam has failed for originalTargetMethodDef. MethodDef: ",
                             shared::TokenStr(&methodDef));
            }
        }
    }

    FaultTolerantTracker::Instance()->AddFaultTolerant(moduleId, methodDef, originalTargetMethodDef,
                                                       instrumentedTargetMethodDef);
}

void fault_tolerant::FaultTolerantMethodDuplicator::DuplicateAll(const ModuleID moduleId, const trace::ModuleInfo& moduleInfo,
                                                              ComPtr<IMetaDataImport2> metadataImport, ComPtr<IMetaDataEmit2> metadataEmit) const

{
    if (!is_fault_tolerant_instrumentation_enabled)
    {
        return;
    }

    // Enumerate the types of the module, for each type enumerate all the methods and duplicate them accordingly to the Fault-Tolerant Instrumentation

    auto typeDefEnum = EnumTypeDefs(metadataImport);
    auto typeDefIterator = typeDefEnum.begin();
    for (; typeDefIterator != typeDefEnum.end(); typeDefIterator = ++typeDefIterator)
    {
        const auto typeDef = *typeDefIterator;

        auto enumMethods = trace::Enumerator<mdMethodDef>(
        [&metadataImport, typeDef](HCORENUM* ptr, mdMethodDef arr[], ULONG max, ULONG* cnt) -> HRESULT {
            return metadataImport->EnumMethods(ptr, typeDef, arr, max, cnt);
        },
        [&metadataImport](HCORENUM ptr) -> void { metadataImport->CloseEnum(ptr); });

        auto methodDefIterator = enumMethods.begin();
        for (; methodDefIterator != enumMethods.end(); methodDefIterator = ++methodDefIterator)
        {
            const auto methodDef = *methodDefIterator;
            DuplicateOne(moduleId, moduleInfo, metadataImport, metadataEmit, typeDef, methodDef);
        }
    }

    if (FAILED(this->m_corProfiler->info_->ApplyMetaData(moduleId)))
    {
        trace::Logger::Warn("    * Failed to call ApplyMetadata.");
    }
}
