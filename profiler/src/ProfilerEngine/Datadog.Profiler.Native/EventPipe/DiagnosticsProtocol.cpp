#include <iostream>
#include <iomanip>

#include "DiagnosticsProtocol.h"

// dump the buffer every 16 bytes + corresponding ASCII characters
// ex:  44 4F 54 4E 45 54 5F 49  50 43 5F 56 31 00 77 00    DOTNET_IPC_V1.w.
const DWORD LineWidth = 16;

char GetCharFromBinary(uint8_t byte)
{
    if ((byte >= 0x21) && (byte <= 0x7E))
        return static_cast<char>(byte);

    return '.';
}

void DumpBuffer(const uint8_t* pBuffer, DWORD byteCount)
{
    //// TODO: uncomment to skip displaying memory buffer on console
    //return;

    DWORD pos = 0;
    char stringBuffer[LineWidth + 1];
    ::ZeroMemory(stringBuffer, LineWidth + 1);

    std::cout << std::hex;
    for (DWORD i = 0; i < byteCount; i++)
    {
        std::cout << std::uppercase << std::setfill('0') << std::setw(2) << (int)pBuffer[i] << " ";
        stringBuffer[pos] = GetCharFromBinary(pBuffer[i]);
        pos++;

        if (pos % LineWidth == 0)
        {
            std::cout << "    ";
            std::cout << stringBuffer;
            std::cout << "\n";

            ::ZeroMemory(stringBuffer, LineWidth + 1);
            pos = 0;
        }
        else
            if (pos % (LineWidth / 2) == 0)
            {
                std::cout << " ";
            }
    }

    // show the remaining characters if any
    if (pos > 0)
    {
        for (size_t i = 0; i < LineWidth - pos; i++)
        {
            std::cout << "   ";
        }

        if (pos > LineWidth / 2)
            std::cout << "    ";
        else
            std::cout << "     ";
        std::cout << stringBuffer;
        std::cout << "\n";
    }

    // reset to default
    std::cout << std::setfill(' ') << std::setw(1) << std::dec;
}





ProcessInfoRequest::ProcessInfoRequest()
{
    _buffer = nullptr;
    Error = 0;
    Pid = 0;
    RuntimeCookie = {0, 0, 0, 0};
    CommandLine = nullptr;
    OperatingSystem = nullptr;
    Architecture = nullptr;
}

ProcessInfoRequest::~ProcessInfoRequest()
{
    CommandLine = nullptr;
    OperatingSystem = nullptr;
    Architecture = nullptr;

    if (_buffer != nullptr)
    {
        delete _buffer;
    }
}

bool ProcessInfoRequest::Send(HANDLE hPipe)
{
    // send the request
    IpcHeader message = ProcessInfoMessage;
    DWORD bytesWrittenCount = 0;
    if (!::WriteFile(hPipe, &message, sizeof(message), &bytesWrittenCount, nullptr))
    {
        Error = ::GetLastError();
        std::cout << "Error while sending ProcessInfo message to the CLR: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }

    // analyze the response
    // 1. get the header to know how large the buffer should be to get the payload
    message = {};
    DWORD bytesReadCount = 0;
    if (!::ReadFile(hPipe, &message, sizeof(message), &bytesReadCount, nullptr))
    {
        Error = ::GetLastError();
        std::cout << "Error while getting ProcessInfo response from the CLR: 0x" << std::hex << Error<< std::dec << "\n";
        return false;
    }

    if (message.CommandId != (uint8_t)DiagnosticServerResponseId::OK)
    {
        Error = message.CommandId;
        std::cout << "Error returned by the CLR in ProcessInfo response: 0x" << std::hex << Error<< std::dec << "\n";
        return false;
    }

    // 2. allocate the buffer and get the payload
    uint16_t payloadSize = message.Size - sizeof(message);
    _buffer = new uint8_t[payloadSize];
    if (!::ReadFile(hPipe, _buffer, payloadSize, &bytesReadCount, nullptr))
    {
        Error = ::GetLastError();
        std::cout << "Error while getting ProcessInfo payload: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }
    // Note: bytesReadCount == payloadSize

    return ParseResponse(bytesReadCount);
}

bool ProcessInfoRequest::Process(IIpcEndpoint* pEndpoint)
{
    // send the request
    IpcHeader message = ProcessInfoMessage;
    DWORD bytesWrittenCount = 0;
    if (!pEndpoint->Write(&message, sizeof(message), &bytesWrittenCount))
    {
        Error = ::GetLastError();
        std::cout << "Error while sending ProcessInfo message to the CLR: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }

    // analyze the response
    // 1. get the header to know how large the buffer should be to get the payload
    message = {};
    DWORD bytesReadCount = 0;
    if (!pEndpoint->Read(&message, sizeof(message), &bytesReadCount))
    {
        Error = ::GetLastError();
        std::cout << "Error while getting ProcessInfo response from the CLR: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }

    if (message.CommandId != (uint8_t)DiagnosticServerResponseId::OK)
    {
        Error = message.CommandId;
        std::cout << "Error returned by the CLR in ProcessInfo response: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }

    // 2. allocate the buffer and get the payload
    uint16_t payloadSize = message.Size - sizeof(message);
    _buffer = new uint8_t[payloadSize];
    if (!pEndpoint->Read(_buffer, payloadSize, &bytesReadCount))
    {
        Error = ::GetLastError();
        std::cout << "Error while getting ProcessInfo payload: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }
    // Note: bytesReadCount == payloadSize

    return ParseResponse(bytesReadCount);
}


void PointToString(uint8_t* buffer, uint32_t& index, wchar_t*& string)
{
    // strings are stored as:
    //    - characters count as uint32_t
    //    - array of UTF16 characters followed by "\0"
    // Note that the last L"\0" IS COUNTED
    uint32_t count;
    memcpy(&count, &buffer[index], sizeof(count));

    // skip characters count
    index += sizeof(count);

    // empty string case
    // Note: could make it point to the "count" which is 0 in the buffer
    //       instead of returning nullptr
    if (count == 0)
    {
        string = nullptr;
        return;
    }

    string = (wchar_t*)&buffer[index];

    // skip the whole string (including last UTF16 '\0')
    index += count * (uint32_t)sizeof(wchar_t);
}

bool ProcessInfoRequest::ParseResponse(DWORD payloadSize)
{
    // payload layout:
    // ---------------------------
    // uint64_t pid
    // GUID guid
    // string command line
    // string operating system
    // string architecture
    // ---------------------------
    //
    uint32_t index = 0;

    memcpy(&Pid, &_buffer[index], sizeof(Pid));
    index += sizeof(Pid);
    if (payloadSize < index) return false;

    memcpy(&RuntimeCookie, &_buffer[index], sizeof(RuntimeCookie));
    index += sizeof(RuntimeCookie);
    if (payloadSize < index) return false;

    PointToString(_buffer, index, CommandLine);
    if (payloadSize < index) return false;

    PointToString(_buffer, index, OperatingSystem);
    if (payloadSize < index) return false;

    PointToString(_buffer, index, Architecture);

    return true;
}


EventPipeStartRequest::EventPipeStartRequest()
{
    Error = 0;
    SessionId = 0;
}

StartSessionMessage* CreateStartSessionMessage(uint64_t keywords, EventVerbosityLevel verbosity)
{
    auto message = new StartSessionMessage();
    ::ZeroMemory(message, sizeof(message));
    memcpy(message->Magic, &DotnetIpcMagic_V1, sizeof(message->Magic));
    message->Size = sizeof(StartSessionMessage);  // weird that we get 120 instead of 119 : might be related to padding...
    message->CommandSet = (uint8_t)DiagnosticServerCommandSet::EventPipe;
    message->CommandId = (uint8_t)EventPipeCommandId::CollectTracing2;
    message->Reserved = 0;
    message->CircularBufferMB = CircularBufferMBSize;
    message->Format = NetTraceFormat;
    message->RequestRundown = 0;
    message->ProviderCount = 1;
    message->Keywords = (uint64_t)keywords;
    message->Verbosity = (uint32_t)verbosity;
    message->ProviderStringLen = DotnetProviderMagicLength;
    memcpy(message->Provider, &DotnetProviderMagic, sizeof(message->Provider));
    message->Arguments = 0;

    return message;
}

bool EventPipeStartRequest::Process(IIpcEndpoint* pEndpoint, uint64_t keywords, EventVerbosityLevel verbosity)
{
    // send an StartSessionMessage and parse the response
    StartSessionMessage* pMessage = CreateStartSessionMessage(keywords, verbosity);

    DumpBuffer((uint8_t*)pMessage, pMessage->Size);

    DWORD writtenBytes = 0;
    if (!pEndpoint->Write(pMessage, pMessage->Size, &writtenBytes))
    {
        Error = ::GetLastError();
        std::cout << "Error while sending EventPipe collect message to the CLR: 0x" << std::hex << Error << std::dec << "\n";
        delete pMessage;
        return false;
    }
    delete pMessage;

    // analyze the response
    IpcHeader response = {};
    DWORD bytesReadCount = 0;
    if (!pEndpoint->Read(&response, sizeof(response), &bytesReadCount))
    {
        Error = ::GetLastError();
        std::cout << "Error while getting EventPipe collect response from the CLR: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }

    if (response.CommandId != (uint8_t)DiagnosticServerResponseId::OK)
    {
        Error = response.CommandId;
        std::cout << "Error returned by the CLR in EventPipe collect response: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }

    // get the session ID from the payload
    uint16_t payloadSize = response.Size - sizeof(response);
    if (payloadSize < sizeof(uint64_t))
    {
        Error = 0;
        std::cout << "Unexpected EventPipe collect reponse payload size: " << payloadSize << "\n";
        return false;
    }

    if (!pEndpoint->ReadLong(SessionId))
    {
        Error = ::GetLastError();
        std::cout << "Error while getting Session ID from EventPipe collect reponse payload: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }

    return true;
}


EventPipeStopRequest::EventPipeStopRequest()
{
    Error = 0;
}

StopSessionMessage* CreateStopMessage(uint64_t sessionId)
{
    StopSessionMessage* message = new StopSessionMessage();
    ::ZeroMemory(message, sizeof(message));
    memcpy(message->Magic, &DotnetIpcMagic_V1, sizeof(message->Magic));
    message->Size = sizeof(StopSessionMessage);
    message->CommandSet = (uint8_t)DiagnosticServerCommandSet::EventPipe;
    message->CommandId = (uint8_t)EventPipeCommandId::StopTracing;
    message->Reserved = 0;
    message->SessionId = sessionId;

    return message;
}

bool EventPipeStopRequest::Process(IIpcEndpoint* pEndpoint, uint64_t sessionId)
{
    StopSessionMessage* pMessage = CreateStopMessage(sessionId);
    DWORD writtenBytes;
    if (!pEndpoint->Write(pMessage, pMessage->Size, &writtenBytes))
    {
        Error = ::GetLastError();
        std::cout << "Error while sending EventPipe Stop message to the CLR: 0x" << std::hex << Error << std::dec << "\n";
        delete pMessage;
        return false;
    }
    delete pMessage;

    // check the response
    IpcHeader response = {};
    DWORD bytesReadCount = 0;
    if (!pEndpoint->Read(&response, sizeof(response), &bytesReadCount))
    {
        Error = ::GetLastError();
        std::cout << "Error while reading EventPipe Stop message response from the CLR: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }

    if (response.CommandId == (uint8_t)DiagnosticServerResponseId::OK)
    {
        // TODO: the payload should be the same session ID
        return true;
    }

    // get the error if any
    uint16_t payloadSize = response.Size - sizeof(response);
    if (payloadSize < sizeof(uint32_t))
    {
        Error = 0;
        std::cout << "Unexpected EventPipe stop reponse payload size: " << payloadSize << "\n";
        return false;
    }
    uint32_t error = 0;
    if (!pEndpoint->ReadDWord(error))
    {
        Error = ::GetLastError();
        std::cout << "Error while getting Session ID from EventPipe stop reponse payload: 0x" << std::hex << Error << std::dec << "\n";
        return false;
    }

    std::cout << "Error while getting Session ID from EventPipe stop reponse payload: 0x" << std::hex << error << std::dec << "\n";
    return false;
}