#include "ExceptionsManager.h"

#include "FrameStore.h"
#include "HResultConverter.h"
#include "OsSpecificApi.h"
#include "shared/src/native-src/com_ptr.h"
#include "shared/src/native-src/string.h"
#include "Log.h"

#include <iostream>

#define INVOKE(x)                                                                                              \
    {                                                                                                          \
        HRESULT hr = x;                                                                                        \
        if (FAILED(hr))                                                                                        \
        {                                                                                                      \
            Log::Warn("Profiler call failed with result ", HResultConverter::ToStringWithCode(hr), ": ", #x);  \
            return;                                                                                            \
        }                                                                                                      \
    }                                                                                                          \

#define INVOKE(x, retval)                                                                                     \
    {                                                                                                         \
        HRESULT hr = x;                                                                                       \
        if (FAILED(hr))                                                                                       \
        {                                                                                                     \
            Log::Warn("Profiler call failed with result ", HResultConverter::ToStringWithCode(hr), ": ", #x); \
            return retval;                                                                                    \
        }                                                                                                     \
    }                                                                                                         \

ExceptionsManager::ExceptionsManager(ICorProfilerInfo4* pCorProfilerInfo, IManagedThreadList* pManagedThreadList, IFrameStore* pFrameStore) :
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

void ExceptionsManager::OnModuleLoaded(const ModuleID moduleId)
{
    if (_mscorlibModuleId != 0)
    {
        return;
    }

    // Check if it's mscorlib. In that case, locate the System.Exception type
    // TODO: it probably needs to be associated to the appdomain
    std::string assemblyName;

    if (!FrameStore::GetAssemblyName(_pCorProfilerInfo, moduleId, assemblyName))
    {
        Log::Warn("Failed to retrieve assembly name for module ", moduleId);
        return;
    }

    if (assemblyName != "System.Private.CoreLib" && assemblyName != "mscorlib")
    {
        return;
    }

    _mscorlibModuleId = moduleId;

    INVOKE(_pCorProfilerInfo->GetStringLayout2(&_stringLengthOffset, &_stringBufferOffset))

    return;
}

void ExceptionsManager::OnExceptionThrown(ObjectID thrownObjectId)
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

    // convert from UTF16 to UTF8
    const auto name = shared::ToString(shared::WSTRING(pBuffer));

    std::cout << "CorProfilerCallback::ExceptionThrown - " << name << std::endl;

    if (_mscorlibModuleId == 0)
    {
        Log::Warn("An exception has been thrown but mscorlib is not loaded");
        return;
    }

    if (_exceptionClassId == 0)
    {
        if (!LoadExceptionMetadata())
        {
            return;
        }
    }

    const auto messageAddress = *reinterpret_cast<UINT_PTR*>(thrownObjectId + _messageFieldOffset.ulOffset);

    if (messageAddress == 0)
    {
        std::cout << "Message is null" << std::endl;
    }
    else
    {
        const auto stringLength = *reinterpret_cast<ULONG*>(messageAddress + _stringLengthOffset);

        const auto message = shared::ToString(reinterpret_cast<WCHAR*>(messageAddress + _stringBufferOffset));
        std::cout << "Message: " << message << std::endl;
    }

    const auto collector = OsSpecificApi::CreateNewStackFramesCollectorInstance(_pCorProfilerInfo);

    ThreadID threadId;
    INVOKE(_pCorProfilerInfo->GetCurrentThreadID(&threadId))

    ManagedThreadInfo* threadInfo = nullptr;

    if (!_pManagedThreadList->GetOrCreateThread(threadId, &threadInfo))
    {
        Log::Warn("Failed to get thread info");
        return;
    }

    uint32_t hrCollectStack = E_FAIL;
    collector->PrepareForNextCollection();
    const auto result = collector->CollectStackSampleImplementation(threadInfo, &hrCollectStack);

    if (FAILED(hrCollectStack))
    {
        Log::Warn("Failed to walk stack with HRESULT = ", HResultConverter::ToStringWithCode(hrCollectStack));

        if (result == nullptr)
        {
            return;
        }
    }

    const auto frameCount = result->GetFramesCount();

    for (uint16_t i = 0; i < frameCount; i++)
    {
        auto& frame = result->GetFrameAtIndex(i);

        auto [isManaged, moduleName, method] = _pFrameStore->GetFrame(frame.GetNativeIP());

        if (isManaged)
        {
            std::cout << std::hex << frame.GetNativeIP() << " " << moduleName << " " << method << std::endl;
        }
    }
}

bool ExceptionsManager::LoadExceptionMetadata()
{
    // This is the first observed exception, lazy-load the exception metadata
    ComPtr<IMetaDataImport2> metadataImportMscorlib;

    INVOKE(
        _pCorProfilerInfo->GetModuleMetaData(_mscorlibModuleId, CorOpenFlags::ofRead, IID_IMetaDataImport2, reinterpret_cast<IUnknown**>(metadataImportMscorlib.GetAddressOf())),
        false)        

    mdTypeDef exceptionTypeDef;

    INVOKE(metadataImportMscorlib->FindTypeDefByName(WStr("System.Exception"), mdTokenNil, &exceptionTypeDef), false)
            
    ClassID exceptionClassId;

    INVOKE(_pCorProfilerInfo->GetClassFromTokenAndTypeArgs(_mscorlibModuleId, exceptionTypeDef, 0, nullptr, &exceptionClassId), false);

    ULONG numberOfFields;
    ULONG classSize;

    INVOKE(_pCorProfilerInfo->GetClassLayout(exceptionClassId, nullptr, 0, &numberOfFields, &classSize), false);
    
    const auto fields = std::make_unique<COR_FIELD_OFFSET[]>(numberOfFields);

    INVOKE(_pCorProfilerInfo->GetClassLayout(exceptionClassId, fields.get(), numberOfFields, &numberOfFields, &classSize), false);
    
    mdFieldDef messageFieldDef;

    constexpr COR_SIGNATURE signature[2] = {IMAGE_CEE_CS_CALLCONV_FIELD, ELEMENT_TYPE_STRING};

    metadataImportMscorlib->FindField(exceptionTypeDef, WStr("_message"), signature, 2, &messageFieldDef);

    bool found = false;

    for (ULONG i = 0; i < numberOfFields; i++)
    {
        if (fields[i].ridOfField == messageFieldDef)
        {
            _messageFieldOffset = fields[i];
            found = true;
            break;
        }
    }

    if (!found)
    {
        Log::Warn("Failed to find the field _message in the System.Exception class");
        return false;
    }

    return true;
}
