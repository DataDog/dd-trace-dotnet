// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ExceptionsProvider.h"

#include "COMHelpers.h"
#include "FrameStore.h"
#include "HResultConverter.h"
#include "IConfiguration.h"
#include "Log.h"
#include "OsSpecificApi.h"
#include "ScopeFinalizer.h"
#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"


std::vector<SampleValueType> ExceptionsProvider::SampleTypeDefinitions(
    {
        {"exception", "count"}
    });

ExceptionsProvider::ExceptionsProvider(
    uint32_t valueOffset,
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    IConfiguration* pConfiguration,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    MetricsRegistry& metricsRegistry)
    :
    CollectorBase<RawExceptionSample>("ExceptionsProvider", valueOffset, pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore, pConfiguration),
    _pCorProfilerInfo(pCorProfilerInfo),
    _pManagedThreadList(pManagedThreadList),
    _pFrameStore(pFrameStore),
    _messageFieldOffset(),
    _stringLengthOffset(0),
    _stringBufferOffset(0),
    _mscorlibModuleId(0),
    _exceptionClassId(0),
    _loggedMscorlibError(false),
    _sampler(pConfiguration->ExceptionSampleLimit(), pConfiguration->GetUploadInterval(), true),
    _pConfiguration(pConfiguration)
{
    _exceptionsCountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_exceptions");
    _sampledExceptionsCountMetric = metricsRegistry.GetOrRegister<CounterMetric>("dotnet_sampled_exceptions");
}

bool ExceptionsProvider::OnModuleLoaded(const ModuleID moduleId)
{
    if (_mscorlibModuleId != 0)
    {
        return false;
    }

    // Check if it's mscorlib. In that case, locate the System.Exception type
    std::string assemblyName;

    if (!FrameStore::GetAssemblyName(_pCorProfilerInfo, moduleId, assemblyName))
    {
        Log::Warn("Failed to retrieve assembly name for module ", moduleId);
        return false;
    }

    if (assemblyName != "System.Private.CoreLib" && assemblyName != "mscorlib")
    {
        return false;
    }

    _mscorlibModuleId = moduleId;

    INVOKE(_pCorProfilerInfo->GetStringLayout2(&_stringLengthOffset, &_stringBufferOffset))

    return true;
}

bool ExceptionsProvider::OnExceptionThrown(ObjectID thrownObjectId)
{
    if (_mscorlibModuleId == 0)
    {
        if (!_loggedMscorlibError)
        {
            _loggedMscorlibError = true;
            Log::Warn("An exception has been thrown but mscorlib is not loaded");
        }
        return false;
    }

    if (_exceptionClassId == 0)
    {
        if (!LoadExceptionMetadata())
        {
            return false;
        }
    }

    ClassID classId;

    INVOKE(_pCorProfilerInfo->GetClassFromObject(thrownObjectId, &classId))

    std::string name;
    if (!GetExceptionType(classId, name))
    {
        return false;
    }

    _exceptionsCountMetric->Incr();
    if (!_sampler.Sample(name))
    {
        return true;
    }

    const auto messageAddress = *reinterpret_cast<UINT_PTR*>(thrownObjectId + _messageFieldOffset.ulOffset);

    std::string message;

    if (messageAddress == 0)
    {
        message = std::string();
    }
    else
    {
        const auto stringLength = *reinterpret_cast<ULONG*>(messageAddress + _stringLengthOffset);

        if (stringLength == 0)
        {
            message = std::string();
        }
        else
        {
            message = shared::ToString(reinterpret_cast<WCHAR*>(messageAddress + _stringBufferOffset), stringLength);
        }
    }

    std::shared_ptr<ManagedThreadInfo> threadInfo;

    INVOKE(_pManagedThreadList->TryGetCurrentThreadInfo(threadInfo))

    uint32_t hrCollectStack = E_FAIL;
    const auto pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(_pCorProfilerInfo, _pConfiguration);

    pStackFramesCollector->PrepareForNextCollection();
    const auto result = pStackFramesCollector->CollectStackSample(threadInfo.get(), &hrCollectStack);

    if (result->GetFramesCount() == 0)
    {
        Log::Warn("Failed to walk stack for thrown exception: ", HResultConverter::ToStringWithCode(hrCollectStack));
        return false;
    }

    result->SetUnixTimeUtc(GetCurrentTimestamp());
    result->DetermineAppDomain(threadInfo->GetClrThreadId(), _pCorProfilerInfo);

    RawExceptionSample rawSample;

    rawSample.Timestamp = result->GetUnixTimeUtc();
    rawSample.LocalRootSpanId = result->GetLocalRootSpanId();
    rawSample.SpanId = result->GetSpanId();
    rawSample.AppDomainId = result->GetAppDomainId();
    result->CopyInstructionPointers(rawSample.Stack);
    rawSample.ThreadInfo = threadInfo;
    rawSample.ExceptionMessage = std::move(message);
    rawSample.ExceptionType = std::move(name);
    Add(std::move(rawSample));
    _sampledExceptionsCountMetric->Incr();

    return true;
}

bool ExceptionsProvider::GetExceptionType(ClassID classId, std::string& exceptionType)
{
    {
        std::lock_guard lock(_exceptionTypesLock);

        const auto type = _exceptionTypes.find(classId);
        if (type != _exceptionTypes.end())
        {
            exceptionType = type->second;
            return true;
        }
    }

    if (!_pFrameStore->GetTypeName(classId, exceptionType))
    {
        return false;
    }

    {
        std::lock_guard lock(_exceptionTypesLock);
        _exceptionTypes.insert_or_assign(classId, exceptionType);
    }

    return true;
}

UpscalingInfo ExceptionsProvider::GetUpscalingInfo()
{
    return {GetValueOffsets(SampleTypeDefinitions.size()), Sample::ExceptionTypeLabel, _sampler.GetGroups()};
}

bool ExceptionsProvider::LoadExceptionMetadata()
{
    // This is the first observed exception, lazy-load the exception metadata
    ComPtr<IMetaDataImport2> metadataImportMscorlib;

    INVOKE(
        _pCorProfilerInfo->GetModuleMetaData(_mscorlibModuleId, CorOpenFlags::ofRead, IID_IMetaDataImport2, reinterpret_cast<IUnknown**>(metadataImportMscorlib.GetAddressOf())))

    mdTypeDef exceptionTypeDef;

    INVOKE(metadataImportMscorlib->FindTypeDefByName(WStr("System.Exception"), mdTokenNil, &exceptionTypeDef))

    ClassID exceptionClassId;

    INVOKE(_pCorProfilerInfo->GetClassFromTokenAndTypeArgs(_mscorlibModuleId, exceptionTypeDef, 0, nullptr, &exceptionClassId));

    ULONG numberOfFields;
    ULONG classSize;

    INVOKE(_pCorProfilerInfo->GetClassLayout(exceptionClassId, nullptr, 0, &numberOfFields, &classSize));

    const auto fields = std::make_unique<COR_FIELD_OFFSET[]>(numberOfFields);

    INVOKE(_pCorProfilerInfo->GetClassLayout(exceptionClassId, fields.get(), numberOfFields, &numberOfFields, &classSize));

    mdFieldDef messageFieldDef;

    constexpr COR_SIGNATURE signature[2] = {IMAGE_CEE_CS_CALLCONV_FIELD, ELEMENT_TYPE_STRING};

    metadataImportMscorlib->FindField(exceptionTypeDef, WStr("_message"), signature, 2, &messageFieldDef);

    for (ULONG i = 0; i < numberOfFields; i++)
    {
        if (fields[i].ridOfField == messageFieldDef)
        {
            _messageFieldOffset = fields[i];
            // Set the _exceptionClassId field to notify that we found
            // the message field offset.
            // So we do not enter this method anymore
            _exceptionClassId = exceptionClassId;
            return true;
        }
    }

    return false;
}
