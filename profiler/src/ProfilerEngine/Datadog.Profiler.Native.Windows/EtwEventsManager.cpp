// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.


#include "EtwEventsManager.h"
#include "IContentionListener.h"
#include "IAllocationsListener.h"
#include "IGCSuspensionsListener.h"
#include "Log.h"
#include "OpSysTools.h"

#include "Windows.h"

#include <memory>
#include <sstream>
#include <string>

const std::string NamedPipePrefix = "\\\\.\\pipe\\DD_ETW_CLIENT_";
const std::string NamedPipeAgent = "\\\\.\\pipe\\DD_ETW_DISPATCHER";
const uint32_t MaxInstances = 2;
const uint32_t TimeoutMS = 500;


EtwEventsManager::EtwEventsManager(
    IAllocationsListener* pAllocationListener,
    IContentionListener* pContentionListener,
    IGCSuspensionsListener* pGCSuspensionsListener,
    IConfiguration* pConfiguration)
    :
    _pAllocationListener(pAllocationListener),
    _pContentionListener(pContentionListener)
{
    _isDebugLogEnabled = pConfiguration->IsEtwLoggingEnabled();

    _threadsInfo.reserve(256);
    _parser = std::make_unique<ClrEventsParser>(
        nullptr,  // to avoid duplicates with what is done in EtwEventsHandler
        nullptr,  // to avoid duplicates with what is done in EtwEventsHandler
        pGCSuspensionsListener);
    _logger = std::make_unique<ProfilerLogger>();
}


uint64_t TimestampToEpochNS(uint64_t eventTimestamp)
{
    // the event timestamp is in 100ns units since 1601-01-01 in UTC
    FILETIME ft;
    ft.dwLowDateTime = eventTimestamp & 0xFFFFFFFF;
    ft.dwHighDateTime = eventTimestamp >> 32;

    // convert into system time
    SYSTEMTIME st;
    ::FileTimeToSystemTime(&ft, &st);

    // take GMT shift into account
    ::SystemTimeToTzSpecificLocalTime(nullptr, &st, &st);

    // convert in epoch time
    tm t = {0};
    t.tm_year = st.wYear - 1900;
    t.tm_mon = st.wMonth - 1;
    t.tm_mday = st.wDay;
    t.tm_hour = st.wHour;
    t.tm_min = st.wMinute;
    t.tm_sec = st.wSecond;
    t.tm_isdst = -1; // don't mess with daylight saving time (already done by SystemTimeToTzSpecificLocalTime)
    time_t timeSinceEpoch = mktime(&t);

    return timeSinceEpoch * 1000'000'000 + st.wMilliseconds * 1'000'000;  // don't loose the milliseconds accuracy
}

void EtwEventsManager::OnEvent(
    uint64_t systemTimestamp,
    uint32_t tid,
    uint32_t version,
    uint64_t keyword,
    uint8_t level,
    uint32_t id,
    uint32_t cbEventData,
    const uint8_t* pEventData)
{
    if (keyword == KEYWORD_STACKWALK)
    {
        if (_isDebugLogEnabled)
        {
            std::cout << "StackWalk for thread #" << tid;
        }

        auto pThreadInfo = GetOrCreate(tid);
        if (pThreadInfo->LastEventWasContentionStart())
        {
            if (_isDebugLogEnabled)
            {
                std::cout << " (contention start)" << std::endl;
            }

            AttachContentionCallstack(pThreadInfo, cbEventData, pEventData);
        }
        else if (pThreadInfo->LastEventWasAllocationTick())
        {
            if (_isDebugLogEnabled)
            {
                std::cout << " (allocation tick)" << std::endl;
            }

            if (_pAllocationListener != nullptr)
            {
                AttachAllocationCallstack(pThreadInfo, cbEventData, pEventData);
                _pAllocationListener->OnAllocation(
                    pThreadInfo->AllocationTickTimestamp,
                    tid,
                    pThreadInfo->AllocationKind,
                    pThreadInfo->AllocationClassId,
                    pThreadInfo->AllocatedType,
                    pThreadInfo->AllocationAmount,
                    pThreadInfo->AllocationCallStack);
            }
        }
        else
        {
            if (_isDebugLogEnabled)
            {
                std::cout << std::endl;
            }
        }

        pThreadInfo->ClearLastEventId();
    }
    else if (keyword == KEYWORD_CONTENTION)
    {
        if (_pContentionListener == nullptr)
        {
            return;
        }

        if (id == EVENT_CONTENTION_START)
        {
            if (_isDebugLogEnabled)
            {
                std::cout << "ContentionStart for thread #" << tid << std::endl;
            }

            auto pThreadInfo = GetOrCreate(tid);
            pThreadInfo->ContentionStartTimestamp = systemTimestamp;
            pThreadInfo->LastEventId = EventId::ContentionStart;
        }
        else if (id == EVENT_CONTENTION_STOP)
        {
            if (_isDebugLogEnabled)
            {
                std::cout << "ContentionStop for thread #" << tid << std::endl;
            }

            auto pThreadInfo = Find(tid);
            if (pThreadInfo != nullptr)
            {
                pThreadInfo->ClearLastEventId();
                if (pThreadInfo->ContentionStartTimestamp != 0)
                {
                    auto timestamp = TimestampToEpochNS(systemTimestamp); // systemTimestamp is in 100ns units
                    auto duration = (systemTimestamp - pThreadInfo->ContentionStartTimestamp) * 100;
                    pThreadInfo->ContentionStartTimestamp = 0;

                    _pContentionListener->OnContention(timestamp, tid, duration, pThreadInfo->ContentionCallStack);
                }
            }
            else
            {
                // this should never happen
                _logger->Error("Missing thread info...");

                if (_isDebugLogEnabled)
                {
                    std::cout << "Missing thread info..." << std::endl;
                }
            }
        }
    }
    else if (keyword == KEYWORD_GC)
    {
        if (id == EVENT_ALLOCATION_TICK)
        {
            if (_isDebugLogEnabled)
            {
                std::cout << "AllocationTick" << " v" << version << std::endl;
            }

            if (_pAllocationListener == nullptr)
            {
                return;
            }

            auto pThreadInfo = GetOrCreate(tid);
            pThreadInfo->LastEventId = EventId::AllocationTick;

            auto timestamp = TimestampToEpochNS(systemTimestamp);
            // Get the type name and ClassID from the payload
            //template tid = "GCAllocationTick_V3" >
            //    <data name = "AllocationAmount" inType = "win:UInt32" />
            //    <data name = "AllocationKind" inType = "win:UInt32" />
            //    <data name = "ClrInstanceID" inType = "win:UInt16" />
            //    <data name = "AllocationAmount64" inType = "win:UInt64"/>
            //    <data name = "TypeID" inType = "win:Pointer" />
            //    <data name = "TypeName" inType = "win:UnicodeString" />
            //    
            // !! the following 2 fields cannot be directly accessed because
            // !! they will be stored after the string TypeName
            //    <data name = "HeapIndex" inType = "win:UInt32" />
            //    <data name = "Address" inType = "win:Pointer" />
            // but no object size...
            // Since the events are received asynchronously, it is not even possible
            // to assume that the object that was stored at the received Adress field 
            // is still there: it could have been moved by a garbage collection
            AllocationTickV3Payload* pPayload = (AllocationTickV3Payload*)pEventData;
            pThreadInfo->AllocationTickTimestamp = timestamp;
            pThreadInfo->AllocationKind = pPayload->AllocationKind;
            pThreadInfo->AllocationClassId = (uintptr_t)pPayload->TypeId;
            pThreadInfo->AllocationAmount = pPayload->AllocationAmount64;
            
            // TODO: should we use a buffer allocated once and reused to avoid memory allocations due to std::string?
            pThreadInfo->AllocatedType = shared::ToString(shared::WSTRING(&(pPayload->FirstCharInName)));

            // wait for the sibling StackWalk event to create the sample
        }
        else
        {
            if (_isDebugLogEnabled)
            {
                std::cout << "GC event: " << keyword << " - " << id << std::endl;
            }

            // reuse GC events parser
            auto timestamp = TimestampToEpochNS(systemTimestamp);
            _parser->ParseEvent(timestamp, version, keyword, id, cbEventData, pEventData);
        }
    }
    else
    {
        if (_isDebugLogEnabled)
        {
            std::cout << "Unknown event: " << keyword << " - " << id << std::endl;
        }
    }
}

ThreadInfo* EtwEventsManager::GetOrCreate(uint32_t tid)
{
    auto pInfo = Find(tid);
    if (pInfo == nullptr)
    {
        ThreadInfo threadInfo;
        threadInfo.ThreadId = tid;
        pInfo = &(_threadsInfo[tid] = std::move(threadInfo));
    }

    return pInfo;
}

ThreadInfo* EtwEventsManager::Find(uint32_t tid)
{
    auto infoEntry = _threadsInfo.find(tid);
    if (infoEntry != _threadsInfo.end())
    {
        return &(infoEntry->second);
    }

    return nullptr;
}

void EtwEventsManager::AttachCallstack(std::vector<uintptr_t>& stack, uint16_t userDataLength, const uint8_t* pUserData)
{
    stack.clear();

    if (userDataLength == 0)
    {
        return;
    }

    if (userDataLength < sizeof(StackWalkPayload))
    {
        return;
    }

    StackWalkPayload* pPayload = (StackWalkPayload*)pUserData;
    //                    size of all frames                      + payload size             - size of the first frame not counted twice
    if (userDataLength < pPayload->FrameCount * sizeof(uintptr_t) + sizeof(StackWalkPayload) - sizeof(uintptr_t))
    {
        return;
    }

    for (uint32_t i = 0; i < pPayload->FrameCount; ++i)
    {
        stack.push_back(pPayload->Stack[i]);
    }
}

void EtwEventsManager::AttachContentionCallstack(ThreadInfo* pThreadInfo, uint16_t userDataLength, const uint8_t* pUserData)
{
    AttachCallstack(pThreadInfo->ContentionCallStack, userDataLength, pUserData);
}

void EtwEventsManager::AttachAllocationCallstack(ThreadInfo* pThreadInfo, uint16_t userDataLength, const uint8_t* pUserData)
{
    AttachCallstack(pThreadInfo->AllocationCallStack, userDataLength, pUserData);
}

void EtwEventsManager::OnStop()
{
}

void EtwEventsManager::Register(IGarbageCollectionsListener* pGarbageCollectionsListener)
{
    _parser->Register(pGarbageCollectionsListener);
}


bool EtwEventsManager::Start()
{
    DWORD pid = ::GetCurrentProcessId();

    // start the server part to receive proxied ETW events
    std::stringstream buffer;
    buffer << NamedPipePrefix;
    buffer << pid;
    std::string pipeName = buffer.str();
    Log::Info("Exposing ", pipeName);

    _eventsHandler = std::make_unique<EtwEventsHandler>(_logger.get(), this);
    _IpcServer = IpcServer::StartAsync(
        _logger.get(),
        pipeName,
        _eventsHandler.get(),
        (1 << 16) + sizeof(IpcHeader),  // in buffer size = 64K + header
        sizeof(SuccessResponse),        // out buffer contains only the response
        1,                              // only one instance
        TimeoutMS);
    if (_IpcServer == nullptr)
    {
        Log::Error("Error creating the Named Pipe server to receive CLR events...");
        return false;
    }

    // create the client part to send the registration command
    pipeName = NamedPipeAgent;
    Log::Info("Contacting ", pipeName, "...");

    _IpcClient = IpcClient::Connect(_logger.get(), pipeName, TimeoutMS);
    if (_IpcClient == nullptr)
    {
        Log::Error("Impossible to connect to the Datadog Agent named pipe...");
        return false;
    }

    // register our process ID to the Datadog Agent
    return SendRegistrationCommand(true);
}

void EtwEventsManager::Stop()
{
    // unregister our process ID to the Datadog Agent
    if (SendRegistrationCommand(false))
    {
        Log::Info("Unregistered from the Datadog Agent");
    }
    else
    {
        Log::Warn("Fail to unregister from the Datadog Agent...");
    }

    if (_IpcClient != nullptr)
    {
        if (_IpcClient->Disconnect())
        {
            Log::Info("Disconnected from the Datadog Agent named pipe");
        }
        else
        {
            Log::Warn("Failed to disconnect from the Datadog Agent named pipe...");
        }
    }

    // stop listening to incoming ETW events
    if (_IpcServer != nullptr)
    {
        _IpcServer->Stop();
    }
}

static void LogLastError(const char* msg, DWORD error = ::GetLastError())
{
    Log::Error(msg, " (", error, ")");
}

bool EtwEventsManager::SendRegistrationCommand(bool add)
{
    if (_IpcClient == nullptr)
    {
        return false;
    }
    auto pClient = _IpcClient.get();
    DWORD pid = ::GetCurrentProcessId();

    RegistrationProcessMessage message;
    if (add)
    {
        SetupRegisterCommand(message, pid);
    }
    else
    {
        SetupUnregisterCommand(message, pid);
    }

    auto code = pClient->Send(&message, sizeof(message));
    if (code != NamedPipesCode::Success)
    {
        LogLastError("Failed to write to pipe", code);
        return false;
    }

    IpcHeader response;
    code = pClient->Read(&response, sizeof(response));
    if (code == NamedPipesCode::Success)
    {
        if (add)
        {
            Log::Info("Registered with the Agent");
        }
        else
        {
            Log::Info("Unregistered from the Agent");
        }

        return true;
    }

    if (code == NamedPipesCode::NotConnected)
    {
        // expected after unregistration (i.e. !add) because the pipe will be closed by the Agent
        if (add)
        {
            Log::Warn("Agent named pipe is no more connected");
        }
    }
    else if (code == NamedPipesCode::Broken)
    {
        // expected when the Agent crashes
        Log::Error("Agent named pipe is broken...");
    }
    else
    {
        LogLastError("Failed to read result", code);
    }

    if (add)
    {
        Log::Warn("Registration with the Agent failed...");
    }
    else
    {
        Log::Warn("Unregistration from the Agent failed...");
    }

    return false;
}


