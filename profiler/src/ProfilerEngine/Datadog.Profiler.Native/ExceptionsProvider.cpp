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
#include "SampleValueTypeProvider.h"
#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"


std::vector<SampleValueType> ExceptionsProvider::SampleTypeDefinitions(
    {
        {"exception", "count"}
    });

ExceptionsProvider::ExceptionsProvider(
    SampleValueTypeProvider& valueTypeProvider,
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    IConfiguration* pConfiguration,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore,
    MetricsRegistry& metricsRegistry,
    CallstackProvider callstackProvider,
    shared::pmr::memory_resource* memoryResource)
    :
    CollectorBase<RawExceptionSample>("ExceptionsProvider", valueTypeProvider.GetOrRegister(SampleTypeDefinitions), pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore, memoryResource),
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
    _pConfiguration(pConfiguration),
    _callstackProvider{std::move(callstackProvider)},
    _metricsRegistry{metricsRegistry}
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

bool ExceptionsProvider::OnExceptionThrown(ObjectID thrownObjectId, FrameInfoView throwingMethod)
{
    if (thrownObjectId != 0)
    {
        // For an unhandled exception, OnExceptionThrown is called with a non empty throwing method
        // AFTER the exception is actually thrown; so don't count it twice
        _exceptionsCountMetric->Incr();
    }

    // get the offset of the message field
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

    std::string name;

    auto threadInfo = ManagedThreadInfo::CurrentThreadInfo;
    if (threadInfo == nullptr)
    {
        LogOnce(Warn, "ExceptionsProvider::OnExceptionThrown: Profiler failed at getting the current managed thread info ");
        return false;
    }

    std::string message;
    if (thrownObjectId == 0)
    {
        // if the exception is not handled, we will always create the exception sample
        name = std::move(threadInfo->GetExceptionType());
        message = std::move(threadInfo->GetExceptionMessage());

        // today, we don't use ExceptionUnwindFunctionEnter to unwind the stack
        // so we need to reset the current thread exception
        threadInfo->ClearException();
    }
    else
    {
        // get the exception type name for sampling bucket (and the unhandled exception case)
        ClassID classId;
        INVOKE(_pCorProfilerInfo->GetClassFromObject(thrownObjectId, &classId))
        if (!GetExceptionType(classId, name))
        {
            return false;
        }

        const auto messageAddress = *reinterpret_cast<UINT_PTR*>(thrownObjectId + _messageFieldOffset.ulOffset);
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

        // still, we need to keep track of the exception type and message
        // for the current thread just in case it won't be handled
        threadInfo->SetException(name, message);

        if (!_sampler.Sample(name))
        {
            return true;
        }
    }

    // Create a fake call stack for unhandled exception
    // and get the current call stack for the others
    RawExceptionSample rawSample;
    auto timestamp = GetCurrentTimestamp();
    rawSample.Timestamp = timestamp;

    if (thrownObjectId == 0)
    {
        auto [localRootSpanId, spanId] = threadInfo->GetTracingContext();
        rawSample.LocalRootSpanId = localRootSpanId;
        rawSample.SpanId = spanId;

        // for unhandled exceptions, a fake callstack will be created in OnTransform
        // based on the throwing method
        rawSample.ThrowingMethod = throwingMethod;
    }
    else
    {
        uint32_t hrCollectStack = E_FAIL;
        const auto pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(
            _pCorProfilerInfo, _pConfiguration, &_callstackProvider, _metricsRegistry);

        pStackFramesCollector->PrepareForNextCollection();
        const auto result = pStackFramesCollector->CollectStackSample(threadInfo.get(), &hrCollectStack);

        static uint64_t failureCount = 0;
        if ((result->GetFramesCount() == 0) && (failureCount % 100 == 0))
        {
            // log every 100 failures
            failureCount++;
            Log::Warn("Failed to walk ", failureCount, " stacks for sampled exception: ", HResultConverter::ToStringWithCode(hrCollectStack));
            return false;
        }

        rawSample.LocalRootSpanId = result->GetLocalRootSpanId();
        rawSample.SpanId = result->GetSpanId();
        rawSample.Stack = result->GetCallstack();
    }

    rawSample.AppDomainId = threadInfo->GetAppDomainId();
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

UpscalingInfo ExceptionsProvider::GetInfo()
{
    return {GetValueOffsets(), Sample::ExceptionTypeLabel, _sampler.GetGroups()};
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
