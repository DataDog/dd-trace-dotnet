// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// Inspired by https://github.com/zodiacon/Win10SysProgBookSamples/blob/master/Chapter18/CalculatorSvr/CalculatorSvr.cpp
// Another implementation without using ThreadPool API - https://learn.microsoft.com/en-us/windows/win32/ipc/multithreaded-pipe-server
#include <windows.h>

#include <iostream>
#include <sstream>
#include <string>

#include "..\..\..\..\ProfilerEngine\Datadog.Profiler.Native.Windows\ETW\Protocol.h"
#include "..\..\..\..\ProfilerEngine\Datadog.Profiler.Native.Windows\ETW\IpcClient.h"

#include "..\ConsoleLogger.h"

int ShowLastError(const char* msg, DWORD error = ::GetLastError())
{
    printf("%s (%u)\n", msg, error);
    return 1;
}

bool IsSuccessResponse(IpcClient* client)
{
    IpcHeader response;
    auto code = client->Read(&response, sizeof(response));
    if (code != NamedPipesCode::Success)
    {
        if (code == NamedPipesCode::MissingData)
        {
            std::cout << "invalid response size\n";
        }
        else
        {
            std::cout << "Failed to get response...\n";
        }

        return false;
    }

    if (!IsMessageValid(&response))
    {
        std::cout << "Invalid Magic signature...\n";
        return false;
    }

    return (response.ResponseCode == (uint8_t)ResponseId::OK);
}

void SendClrEvents(PTP_CALLBACK_INSTANCE instance, PVOID context)
{
    uint64_t pid = reinterpret_cast<uint64_t>(context);

    // check the profiled application is listening
    std::stringstream sBuffer;
    sBuffer << "\\\\.\\pipe\\DD_ETW_CLIENT_";
    sBuffer << pid;
    std::string pipeName = sBuffer.str();

    std::unique_ptr<ConsoleLogger> logger = std::make_unique<ConsoleLogger>();
    auto client = IpcClient::Connect(logger.get(), pipeName, 500);
    if (client == nullptr)
    {
        std::cout << "Impossible to connect to the profiled application on " << pipeName << "\n";
        return;
    }

    std::cout << "Connected to " << pipeName << "\n ";

    // send messages with different sizes and payload
    auto buffer = std::make_unique<byte[]>((1 << 16) + sizeof(IpcHeader));
    ClrEventsMessage* pMessage = reinterpret_cast<ClrEventsMessage*>(buffer.get());

    for (uint8_t i = 1; i <= 8; i++)
    {
        SetupSendEventsCommand(pMessage, 1024 * i);
        uint16_t messageSize = pMessage->Size;
        buffer[sizeof(IpcHeader)] = i;

        auto code = client->Send(pMessage, messageSize);
        if (code != NamedPipesCode::Success)
        {
            std::cout << "Failed to send CLR events...\n";
            return;
        }

        // fire and forget
        //if (!IsSuccessResponse(client.get()))
        //{
        //    std::cout << "Error from the CLR events receiver...\n ";
        //    return;
        //}
    }

    // check the client is still listening
    IpcHeader IsAliveMessage;
    SetupIsAliveCommand(IsAliveMessage);
    auto success = client->Send(&IsAliveMessage, sizeof(IsAliveMessage));
    if (success != NamedPipesCode::Success)
    {
        std::cout << "Failed to send IsAlive command...\n";
        return;
    }
    else
    {
        std::cout << "The client is alive\n";
    }

    std::cout << "Disconnecting from " << pipeName << "\n ";
    client->Disconnect();
}

void SendClrEventsAsync(uint64_t pid)
{
    if (!::TrySubmitThreadpoolCallback(SendClrEvents, reinterpret_cast<PVOID>(pid), nullptr))
    {
        ShowLastError("Impossible to add the send CLR events callback into the threadpool...");
    }
}

void CALLBACK CommandCallback(PTP_CALLBACK_INSTANCE instance, PVOID context)
{
    uint8_t buffer[sizeof(RegistrationProcessMessage)];
    auto hPipe = static_cast<HANDLE>(context);
    uint64_t unregisteredPid = 0;

    DWORD read;
    for (;;)
    {
        if (!::ReadFile(hPipe, buffer, sizeof(buffer), &read, nullptr) || read == 0)
        {
            auto lastError = ::GetLastError();
            if (lastError == ERROR_BROKEN_PIPE)
            {
                std::cout << "Disconnected client...\n";
            }
            else
            {
                std::cout << "Error reading from pipe (" << lastError << ")...\n ";
            }

            break;
        }

        // process the command
        auto message = reinterpret_cast<RegistrationProcessMessage*>(buffer);
        if (!IsMessageValid(message))
        {
            std::cout << "Invalid received message...\n";
            break;
        }

        // check the expected commands
        if (message->CommandId == Commands::Register)
        {
            std::cout << "pid " << message->Pid << " has been registered\n";

            SendClrEventsAsync(message->Pid);
        }
        else
        if (message->CommandId == Commands::Unregister)
        {
            unregisteredPid = message->Pid;
            std::cout << "pid " << unregisteredPid << " has been unregistered\n";
            break;
        }
        else
        {
            std::cout << "Invalid command (" << message->CommandId << ")...\n";

            // TODO: don't exit but send an error response
            break;
        }

        // send the response
        DWORD written;
        if (!::WriteFile(hPipe, &SuccessResponse, sizeof(SuccessResponse), &written, nullptr))
        {
            printf("Failed to send result!\n");
            continue;
        }
    }

    std::cout << "Disconnecting from " << unregisteredPid << "\n";
    ::DisconnectNamedPipe(hPipe);
    ::CloseHandle(hPipe);
}


int main(int argc, char* argv[])
{
    std::cout << "\n";

    std::string pipeName = "\\\\.\\pipe\\DD_ETW_DISPATCHER";
    std::cout << "Exposing " << pipeName << "\n";

    while (true)
    {
        HANDLE hNamedPipe =
            ::CreateNamedPipeA(
                pipeName.c_str(),
                PIPE_ACCESS_DUPLEX,
                PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                PIPE_UNLIMITED_INSTANCES, // in real world, limit to a max number of profiled applications (255 anyway)
                sizeof(SuccessResponse),
                sizeof(RegistrationProcessMessage),
                0,
                nullptr
                );

        if (hNamedPipe == INVALID_HANDLE_VALUE)
        {
            return ShowLastError("Failed to create named pipe");
        }

        std::cout << "Listening...\n";

        if (!::ConnectNamedPipe(hNamedPipe, nullptr) && ::GetLastError() != ERROR_PIPE_CONNECTED)
        {
            return ShowLastError("ConnectNamedPipe failed...");
        }

        // let a threadpool thread process the command; allowing the server to process more incoming commands
        if (!::TrySubmitThreadpoolCallback(CommandCallback, hNamedPipe, nullptr))
        {
            return ShowLastError("Impossible to add a command callback into the threadpool...");
        }
    }

    return 0;
}