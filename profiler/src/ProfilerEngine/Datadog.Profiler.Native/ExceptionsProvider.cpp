#include "ExceptionsProvider.h"

#include "FrameStore.h"
#include "HResultConverter.h"
#include "OsSpecificApi.h"
#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"
#include "Log.h"

#define INVOKE(x)                                                                                              \
    {                                                                                                          \
        HRESULT hr = x;                                                                                        \
        if (FAILED(hr))                                                                                        \
        {                                                                                                      \
            Log::Warn("Profiler call failed with result ", HResultConverter::ToStringWithCode(hr), ": ", #x);  \
            return false;                                                                                      \
        }                                                                                                      \
    }                                                                                                          \

ExceptionsProvider::ExceptionsProvider(
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    IConfiguration* pConfiguration,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore) :
    CollectorBase<RawExceptionSample>(pConfiguration, pFrameStore, pAppDomainStore, pRuntimeIdStore),
    _pCorProfilerInfo(pCorProfilerInfo),
    _pManagedThreadList(pManagedThreadList),
    _pFrameStore(pFrameStore),
    _messageFieldOffset(),
    _stringLengthOffset(0),
    _stringBufferOffset(0),
    _mscorlibModuleId(0),
    _exceptionClassId(0)
{
}

const char* ExceptionsProvider::GetName()
{
    return _serviceName;
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
    ClassID classId;

    INVOKE(_pCorProfilerInfo->GetClassFromObject(thrownObjectId, &classId))

    ModuleID moduleId;
    mdTypeDef typeDefToken;

    INVOKE(_pCorProfilerInfo->GetClassIDInfo(classId, &moduleId, &typeDefToken))

    ComPtr<IMetaDataImport2> metadataImport;

    INVOKE(_pCorProfilerInfo->GetModuleMetaData(moduleId, ofRead, IID_IMetaDataImport2, reinterpret_cast<IUnknown**>(metadataImport.GetAddressOf())))

    ULONG nameCharCount = 0;

    INVOKE(metadataImport->GetTypeDefProps(typeDefToken, nullptr, 0, &nameCharCount, nullptr, nullptr))
            
    const auto buffer = std::make_unique<WCHAR[]>(nameCharCount);

    INVOKE(metadataImport->GetTypeDefProps(typeDefToken, buffer.get(), nameCharCount, &nameCharCount, nullptr, nullptr))
        
    const auto pBuffer = buffer.get();

    // Convert from UTF16 to UTF8
    auto name = shared::ToString(shared::WSTRING(pBuffer));

    if (_mscorlibModuleId == 0)
    {
        Log::Warn("An exception has been thrown but mscorlib is not loaded");
        return false;
    }

    if (_exceptionClassId == 0)
    {
        if (!LoadExceptionMetadata())
        {
            return false;
        }
    }

    const auto messageAddress = *reinterpret_cast<UINT_PTR*>(thrownObjectId + _messageFieldOffset.ulOffset);

    std::string message;

    if (messageAddress == 0)
    {
        std::cout << "Message is null" << std::endl;
        message = std::string();
    }
    else
    {
        const auto stringLength = *reinterpret_cast<ULONG*>(messageAddress + _stringLengthOffset);

        std::cout << "length of the string: " << stringLength << std::endl;
        // TODO: use stringLength to assign message

        message = shared::ToString(reinterpret_cast<WCHAR*>(messageAddress + _stringBufferOffset));
    }

    const auto collector = OsSpecificApi::CreateNewStackFramesCollectorInstance(_pCorProfilerInfo, _pManagedThreadList);

    ManagedThreadInfo* threadInfo;

    INVOKE(_pManagedThreadList->TryGetCurrentThreadInfo(&threadInfo))

    uint32_t hrCollectStack = E_FAIL;
    collector->PrepareForNextCollection();
    const auto result = collector->CollectStackSample(nullptr, &hrCollectStack);

    if (FAILED(hrCollectStack))
    {
        Log::Warn("Failed to walk stack with HRESULT = ", HResultConverter::ToStringWithCode(hrCollectStack));

        if (result == nullptr)
        {
            return false;
        }
    }

    DetermineAppDomain(threadInfo->GetClrThreadId(), result);

    RawExceptionSample rawSample;

    rawSample.Timestamp = result->GetUnixTimeUtc();
    rawSample.LocalRootSpanId = result->GetLocalRootSpanId();
    rawSample.SpanId = result->GetSpanId();
    rawSample.AppDomainId = result->GetAppDomainId();
    result->CopyInstructionPointers(rawSample.Stack);
    rawSample.ThreadInfo = threadInfo;
    threadInfo->AddRef();
    rawSample.ExceptionMessage = std::move(message);
    rawSample.ExceptionType = std::move(name);
    Add(std::move(rawSample));

    return true;
}

void ExceptionsProvider::OnTransformRawSample(const RawExceptionSample& rawSample, Sample& sample)
{
    sample.AddValue(1, SampleValue::ExceptionCount);
    sample.AddLabel(Label(Sample::ExceptionMessageLabel, rawSample.ExceptionMessage));
    sample.AddLabel(Label(Sample::ExceptionTypeLabel, rawSample.ExceptionType));
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
            return true;
        }
    }
    
    return false;
}

void ExceptionsProvider::DetermineAppDomain(ThreadID threadId, StackSnapshotResultBuffer* const pStackSnapshotResult)
{
    // Determine the AppDomain currently running the sampled thread:
    //
    // (Note: On Windows, the target thread is still suspended and the AddDomain ID will be correct.
    // However, on Linux the signal handler that performed the stack walk has finished and the target
    // thread is making progress again.
    // So, it is possible that since we walked the stack, the thread's AppDomain changed and the AppDomain ID we
    // read here does not correspond to the stack sample. In practice we expect this to occur very rarely,
    // so we accept this for now.
    // If, however, this is observed frequently enough to present a problem, we will need to move the AppDomain
    // ID read logic into _pStackFramesCollector->CollectStackSample(). Collectors that suspend the target thread
    // will be able to read the ID at any time, but non-suspending collectors (e.g. Linux) will need to do it from
    // within the signal handler. An example for this is the
    // StackFramesCollectorBase::TryApplyTraceContextDataFromCurrentCollectionThreadToSnapshot() API which exists
    // to address the same synchronization issue with TraceContextTracking-related data.
    // There is an additioal complexity with the AppDomain case, because it is likely not safe to call
    // _pCorProfilerInfo->GetThreadAppDomain() from the collector's signal handler directly (needs to be investigated).
    // To address this, we will need to do it via a SynchronousOffThreadWorkerBase-based mechanism, similar to how
    // the SymbolsResolver uses a Worker and synchronously waits for results to avoid calling
    // symbol resolution APIs on a CLR thread.)
    AppDomainID appDomainId;
    HRESULT hr = _pCorProfilerInfo->GetThreadAppDomain(threadId, &appDomainId);
    if (SUCCEEDED(hr))
    {
        pStackSnapshotResult->SetAppDomainId(appDomainId);
    }
}
