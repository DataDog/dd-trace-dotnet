#include "ExceptionsProvider.h"

#include "FrameStore.h"
#include "HResultConverter.h"
#include "Log.h"
#include "OsSpecificApi.h"
#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"

#define INVOKE(x)                                                                                             \
    {                                                                                                         \
        HRESULT hr = x;                                                                                       \
        if (FAILED(hr))                                                                                       \
        {                                                                                                     \
            Log::Warn("Profiler call failed with result ", HResultConverter::ToStringWithCode(hr), ": ", #x); \
            return false;                                                                                     \
        }                                                                                                     \
    }

ExceptionsProvider::ExceptionsProvider(
    ICorProfilerInfo4* pCorProfilerInfo,
    IManagedThreadList* pManagedThreadList,
    IFrameStore* pFrameStore,
    IConfiguration* pConfiguration,
    IThreadsCpuManager* pThreadsCpuManager,
    IAppDomainStore* pAppDomainStore,
    IRuntimeIdStore* pRuntimeIdStore)
    :
    CollectorBase<RawExceptionSample>("ExceptionsProvider", pThreadsCpuManager, pFrameStore, pAppDomainStore, pRuntimeIdStore),
    _pCorProfilerInfo(pCorProfilerInfo),
    _pManagedThreadList(pManagedThreadList),
    _pFrameStore(pFrameStore),
    _messageFieldOffset(),
    _stringLengthOffset(0),
    _stringBufferOffset(0),
    _mscorlibModuleId(0),
    _exceptionClassId(0),
    _loggedMscorlibError(false),
    _sampler(pConfiguration)
{
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

    ManagedThreadInfo* threadInfo;

    INVOKE(_pManagedThreadList->TryGetCurrentThreadInfo(&threadInfo))

    uint32_t hrCollectStack = E_FAIL;
    const auto pStackFramesCollector = OsSpecificApi::CreateNewStackFramesCollectorInstance(_pCorProfilerInfo);

    pStackFramesCollector->PrepareForNextCollection();
    const auto result = pStackFramesCollector->CollectStackSample(threadInfo, &hrCollectStack);

    if (result->GetFramesCount() == 0)
    {
        Log::Warn("Failed to walk stack for thrown exception: ", HResultConverter::ToStringWithCode(hrCollectStack));
        return false;
    }

    result->DetermineAppDomain(threadInfo->GetClrThreadId(), _pCorProfilerInfo);

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
    exceptionType = shared::ToString(pBuffer, nameCharCount - 1);

    {
        std::lock_guard lock(_exceptionTypesLock);
        _exceptionTypes.insert_or_assign(classId, exceptionType);
    }

    return true;
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
