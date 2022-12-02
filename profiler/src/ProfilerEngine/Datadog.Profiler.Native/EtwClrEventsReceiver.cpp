// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#ifdef _WINDOWS

#include <iostream>
#include <string>
#include <wchar.h>

#include "EtwClrEventsReceiver.h"
#include "IClrEventsListener.h"
#include "Log.h"
#include "OpSysTools.h"
#include "shared/src/native-src/string.h"


// Microsoft-Windows-DotNETRuntime = {E13C0D23-CCBC-4E12-931B-D9CC2EEE27E4}
const GUID EtwClrEventsReceiver::ClrProviderGuid = {0xE13C0D23, 0xCCBC, 0x4E12, {0x93, 0x1B, 0xD9, 0xCC, 0x2E, 0xEE, 0x27, 0xE4}};

// used to enable stack sibling events in .NET
const uint64_t StackKeyword = 0x40000000;

EtwClrEventsReceiver* EtwClrEventsReceiver::_pThis = nullptr;


EtwClrEventsReceiver::EtwClrEventsReceiver(uint32_t pid, uint64_t keywords, uint8_t verbosity, IClrEventsListener* pEventsListener)
    :
    _pid{pid},
    _keywords{keywords},
    _verbosity{verbosity},
    _pEventsListener{pEventsListener},
    _pReceiverThread{nullptr},
    _hTrace{0},
    _hParse{0}
{
    // TODO: try this keyword to get call stacks sibling events
    // read https://medium.com/criteo-engineering/build-your-own-net-memory-profiler-in-c-call-stacks-2-2-1-f67b440a8cc?source=friends_link&sk=361c666b0b67de17e0e5d1bde6c3035a
    //keywords |= StackKeyword;

    _pThis = this;
}


void CALLBACK EtwClrEventsReceiver::OnEventReceived(PEVENT_RECORD rec)
{
    std::stringstream builder;
    builder
        << std::setw(6) << rec->EventHeader.ProcessId << " | "
        << std::setw(3) << rec->EventHeader.EventDescriptor.Id
        << "(v" << (int)rec->EventHeader.EventDescriptor.Version << ")";
    std::cout << builder.str() << std::endl;

    if (_pThis == nullptr)
    {
        return;
    }

    if (_pThis->_pEventsListener == nullptr)
    {
        return;
    }

    // need to filter per pid because it might not be supported by older Windows versions
    if (rec->EventHeader.ProcessId != _pThis->_pid)
    {
        return;
    }

    _pThis->_pEventsListener->OnEventReceived(
        rec->EventHeader.ThreadId,
        rec->EventHeader.EventDescriptor.Keyword,
        rec->EventHeader.EventDescriptor.Id,
        rec->EventHeader.EventDescriptor.Version,
        rec->UserDataLength,
        reinterpret_cast<byte*>(rec->UserData)
        );
}

void EtwClrEventsReceiver::MainLoop(void)
{
    if (_hParse == 0)
    {
        return;
    }

    FILETIME now;
    ::GetSystemTimeAsFileTime(&now);

    // the first parameter is an array of handles followed by the count of handles in the array
    auto status = ::ProcessTrace(&_hParse, 1, &now, nullptr);
    if (status != ERROR_SUCCESS)
    {
        Log::Error("ProcessTrace failed (status = ", status, ")");
    }
}

EVENT_TRACE_PROPERTIES* EtwClrEventsReceiver::GetSessionProperties()
{
    if (_pProperties == nullptr)
    {
        return nullptr;
    }

    auto props = reinterpret_cast<EVENT_TRACE_PROPERTIES*>(_pProperties.get());
    return props;
}

const char* EtwClrEventsReceiver::GetName()
{
    return _serviceName;
}

bool EtwClrEventsReceiver::Start()
{
    static const ULONG USE_QUERY_PERFORMANCE_COUNTER_TIMESTAMPS = 1;

    Log::Info("Starting the ETW CLR events receiver");
    if (_pEventsListener == nullptr)
    {
        Log::Error("Missing ETW events listener...");
        return false;
    }

    // each session MUST have a different GUID and since several processes could be profiled
    // on the same machine, a new GUID has to be generated
    GUID sessionGuid {0};
    auto hr = ::CoCreateGuid(&sessionGuid);
    if (FAILED(hr))
    {
        Log::Error("CoCreateGuid failed (hr = ", hr, ") ");
        return false;
    }

    // build session name based on pid to also have a different name per process
    // that could be easily spotted with "logman -ets" command
    shared::WSTRINGSTREAM builder;
    builder << WStr("DD_ETWReceiverSession_") << _pid;
    _sessionName = builder.str();

    // the buffer contains the session name at the end of the properties defined in the structure
    uint32_t size = (uint32_t)(sizeof(EVENT_TRACE_PROPERTIES) + (_sessionName.size() + 1) * sizeof(wchar_t));
    _pProperties = std::make_unique<byte[]>(size);
    if (_pProperties == nullptr)
    {
        return false;
    }

    auto props = GetSessionProperties();
    // for more details about each field, see https://learn.microsoft.com/en-us/windows/win32/api/evntrace/ns-evntrace-event_trace_properties_v2
    ::ZeroMemory(props, size);
    props->Wnode.BufferSize = size;
    props->Wnode.ClientContext = USE_QUERY_PERFORMANCE_COUNTER_TIMESTAMPS;
    // TODO: read https://learn.microsoft.com/en-us/windows/win32/etw/wnode-header to see how to
    // convert eventRecord.EventHeader.TimeStamp.QuadPart into timestamp

    props->Wnode.Flags = WNODE_FLAG_TRACED_GUID;  // madatory
    props->Wnode.Guid = sessionGuid;  // TODO: check if we can have several sessions with the same guid
    // see https://learn.microsoft.com/en-us/windows/win32/etw/wnode-header

    // create a private logger session not counted into the 64 sessions Windows limits
    // https://learn.microsoft.com/en-us/windows/win32/etw/configuring-and-starting-a-private-logger-session
    // it does not seem possible to use EVENT_TRACE_PRIVATE_IN_PROC - https://learn.microsoft.com/en-us/windows/win32/etw/logging-mode-constants
    props->LogFileMode = EVENT_TRACE_REAL_TIME_MODE;
    props->LogFileNameOffset = 0; // no filename
    props->LoggerNameOffset = sizeof(EVENT_TRACE_PROPERTIES);
    // session name is stored after all defined fields of the property structure
    wcscpy_s((PWSTR)(props + 1), _sessionName.size() + 1, _sessionName.c_str());

    props->BufferSize = 32; // 32 KB
    props->MinimumBuffers = 4;  // 4 x BufferSize (= 4 x 32KB = 128 KB)
    props->MaximumBuffers = 16; // 16 x BufferSize (= 16 x 32KB = 512 KB)
    props->MaximumFileSize = 100; // in MB but why do we need this in case of real time???
    props->FlushTimer = 0; // default to flush every 1 second

    // ETW sessions could survive to the processes that created them (if not stopped).
    // In case of unexpected crash, the session with the same pid could still be there
    // so close it before trying to start it again
    // TODO: check that both name and GUID must be different  to avoid already exists
    DWORD status = ::StartTraceW(&_hTrace, _sessionName.c_str(), props);
    if (status == ERROR_ALREADY_EXISTS)
    {
        status = ::ControlTraceW(_hTrace, _sessionName.c_str(), props, EVENT_TRACE_CONTROL_STOP);
        if (status != ERROR_SUCCESS)
        {
            Log::Error("Existing ETW session cannot be stopped (status = ", status, ")");
            return false;
        }
        status = ::StartTraceW(&_hTrace, _sessionName.c_str(), props);
    }
    if (status != ERROR_SUCCESS)
    {
        Log::Error("ETW session cannot be started (status = ", status, ")");
        return false;
    }

    // prepare the events processing
    EVENT_TRACE_LOGFILE etl {0};
    etl.LoggerName = (PWSTR)_sessionName.c_str();
    etl.ProcessTraceMode = PROCESS_TRACE_MODE_EVENT_RECORD | PROCESS_TRACE_MODE_REAL_TIME;
    etl.EventRecordCallback = OnEventReceived;
    _hParse = ::OpenTraceW(&etl);
    if (_hParse == INVALID_PROCESSTRACE_HANDLE)
    {
        Log::Error("OpenTrace failed (status = ", status, ")\n ");
        return false;
    }
    else
    {
        // it is needed to create a new thread to call the blocking ProcessTrace() method
        // --> OnEventReceived() will be called from that thread
        _pReceiverThread = new std::thread(&EtwClrEventsReceiver::MainLoop, this);
        OpSysTools::SetNativeThreadName(_pReceiverThread, WorkerThreadName);
    }

    // filter by process ID
    EVENT_FILTER_DESCRIPTOR descriptor;
    descriptor.Type = EVENT_FILTER_TYPE_PID;
    descriptor.Size = sizeof(uint32_t);
    descriptor.Ptr = (ULONG_PTR)&_pid;

    ENABLE_TRACE_PARAMETERS parameters {0};
    parameters.Version = ENABLE_TRACE_PARAMETERS_VERSION_2; // Windows 8.1+
    parameters.EnableProperty = 0;  // we should not need EVENT_ENABLE_PROPERTY_STACK_TRACE thanks to the Stack keyword in .NET
    parameters.ControlFlags = 0;    // reserved: should be 0
    parameters.SourceId = GUID_NULL;
    parameters.FilterDescCount = 1; // number of filters (only 1 for the pid here)
    parameters.EnableFilterDesc = &descriptor;

    status = ::EnableTraceEx2(
        _hTrace,
        &ClrProviderGuid,
        EVENT_CONTROL_CODE_ENABLE_PROVIDER,
        _verbosity, _keywords, 0,
        0,
        &parameters
        );
    if (ERROR_SUCCESS != status)
    {
        Log::Error("EnableTraceEx2 failed (status = ", status, ")");
        ::StopTraceW(_hTrace, _sessionName.c_str(), props);
        return false;
    }

    return true;
}

bool EtwClrEventsReceiver::Stop()
{
    // stop ETW session
    if (_hTrace != 0)
    {
        DWORD status = ::StopTraceW(_hTrace, _sessionName.c_str(), GetSessionProperties());
        if (status != ERROR_SUCCESS)
        {
            Log::Error("StopTraceW failed (status = ", status, ")");
        }
        _hTrace = 0;
    }

    return true;
}

EtwClrEventsReceiver::~EtwClrEventsReceiver()
{
    // just in case something bad happened and Stop would not be called
    Stop();
}

#endif