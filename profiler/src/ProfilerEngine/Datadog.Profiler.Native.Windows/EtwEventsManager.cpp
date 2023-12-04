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
    IGCSuspensionsListener* pGCSuspensionsListener)
    :
    _pAllocationListener(pAllocationListener),
    _pContentionListener(pContentionListener)
{
    _threadsInfo.reserve(256);
    _parser = std::make_unique<ClrEventsParser>(
        nullptr,  // to avoid duplicates with what is done in EtwEventsHandler
        nullptr,  // to avoid duplicates with what is done in EtwEventsHandler
        pGCSuspensionsListener);
    _logger = std::make_unique<ProfilerLogger>();
}

void EtwEventsManager::OnEvent(
    uint64_t timestamp,
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
        auto pThreadInfo = GetOrCreate(tid);
        if (pThreadInfo->LastEventWasContentionStart())
        {
            AttachContentionCallstack(pThreadInfo, cbEventData, pEventData);
        }
        else if (pThreadInfo->LastEventWasAllocationTick())
        {
            AttachAllocationCallstack(pThreadInfo, cbEventData, pEventData);
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
            auto pThreadInfo = GetOrCreate(tid);
            pThreadInfo->ContentionStartTimestamp = OpSysTools::ConvertTicks(timestamp);
            pThreadInfo->LastEventId = EventId::ContentionStart;
        }
        else if (id == EVENT_CONTENTION_STOP)
        {
            auto pThreadInfo = Find(tid);
            if (pThreadInfo != nullptr)
            {
                pThreadInfo->ClearLastEventId();
                if (pThreadInfo->ContentionStartTimestamp != 0)
                {
                    auto ticks = OpSysTools::ConvertTicks(timestamp);
                    auto duration = ticks - pThreadInfo->ContentionStartTimestamp;
                    pThreadInfo->ContentionStartTimestamp = 0;

                    _pContentionListener->OnContention(ticks, tid, duration, pThreadInfo->ContentionCallStack);
                }
            }
            else
            {
                // this should never happen
            }
        }
    }
    else if (keyword == KEYWORD_GC)
    {
        auto pThreadInfo = GetOrCreate(tid);

        if (id == EVENT_ALLOCATION_TICK)
        {
            if (_pAllocationListener == nullptr)
            {
                return;
            }

            pThreadInfo->LastEventId = EventId::AllocationTick;

            // TODO: get the type name and ClassID from the payload
            // TODO: update IAllocationListener to take the type name, ClassID and stack
        }
        else
        {
            // TODO: call only for no call stack GC events
            //_parser.get()->ParseEvent(version, keyword, id, cbEventData, pEventData);
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
        pInfo = &(_threadsInfo[tid] = threadInfo);
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

    // create the client part to send the registration command
    _eventsHandler = std::make_unique<EtwEventsHandler>(_logger.get(), this);
    _IpcServer = IpcServer::StartAsync(
        _logger.get(),
        pipeName,
        _eventsHandler.get(),
        (1 << 16) + sizeof(IpcHeader),  // in buffer size = 64K + header
        sizeof(SuccessResponse),        // out buffer contains only the response
        MaxInstances,                   // max number of instances (2 = the Agent + one pending)
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
    SendRegistrationCommand(true);

    return true;
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

    if (_IpcClient->Disconnect())
    {
        Log::Info("Disconnected from the Datadog Agent named pipe");
    }
    else
    {
        Log::Warn("Failed to disconnect from the Datadog Agent named pipe...");
    }

    // stop listening to incoming ETW events
    _IpcServer->Stop();
}

void LogLastError(const char* msg, DWORD error = ::GetLastError())
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


