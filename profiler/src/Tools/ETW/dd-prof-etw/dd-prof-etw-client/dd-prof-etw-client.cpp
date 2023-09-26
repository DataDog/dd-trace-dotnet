// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// Inspired by https://github.com/zodiacon/Win10SysProgBookSamples/blob/master/Chapter18/CalculatorSvr/CalculatorSvr.cpp
// Another implementation without using ThreadPool API - https://learn.microsoft.com/en-us/windows/win32/ipc/multithreaded-pipe-server
#include <windows.h>

#include <iostream>
#include <memory>
#include <sstream>
#include <string>

#include "..\shared\Protocol.h"
#include "..\shared\IpcClient.h"
#include "..\shared\IpcServer.h"
#include "..\shared\EtwEventsHandler.h"

bool ParseCommandLine(int argc, char* argv[], int& pid, char*& pipe)
{
    bool success = false;
    for (size_t i = 1; i < argc; i++)
    {
        if (strcmp(argv[i], "-pid") == 0)
        {
            if (i == argc - 1)
            {
                return false;
            }

            // atoi is fine because we don't expect 0 to be a valid pid
            pid = atoi(argv[i+1]);
            success = (pid != 0);
        }
        else
        if (strcmp(argv[i], "-pipe") == 0)
        {
            if (i == argc - 1)
            {
                return false;
            }

            pipe = argv[i + 1];
            success = true;
        }
    }

    return success;
}

int ShowLastError(const char* msg, DWORD error = ::GetLastError())
{
    printf("%s (%u)\n", msg, error);
    return -2;
}

bool WriteErrorResponse(HANDLE hPipe)
{
    DWORD written;
    if (!::WriteFile(hPipe, &ErrorResponse, sizeof(ErrorResponse), &written, nullptr))
    {
        printf("Failed to send error response...\n");
        return false;
    }
    ::FlushFileBuffers(hPipe);

    return true;
}

bool WriteSuccessResponse(HANDLE hPipe)
{
    DWORD written;
    if (!::WriteFile(hPipe, &SuccessResponse, sizeof(SuccessResponse), &written, nullptr))
    {
        printf("Failed to send success response...\n");
        return false;
    }
    ::FlushFileBuffers(hPipe);

    return true;
}

bool ReadEvents(HANDLE hPipe, uint8_t* pBuffer, DWORD bufferSize, DWORD& readSize)
{
    bool success = true;
    auto pMessage = reinterpret_cast<ClrEventsMessage*>(pBuffer);
    DWORD totalReadSize = 0;
    DWORD lastError = ERROR_SUCCESS;

    while (totalReadSize < bufferSize)
    {
        success = ::ReadFile(hPipe, &(pBuffer[totalReadSize]), bufferSize - totalReadSize, &readSize, nullptr);

        if (!success || readSize == 0)
        {
            lastError = ::GetLastError();

            if (lastError == ERROR_MORE_DATA)
            {
                totalReadSize += readSize;
                if (totalReadSize == bufferSize)
                {
                    std::cout << "Read buffer was too small...\n";
                    return false;
                }

                continue;
            }
            else
            {
                if (lastError == ERROR_BROKEN_PIPE)
                {
                    std::cout << "Disconnected client...\n";
                }
                else
                {
                    std::cout << "Error reading from pipe (" << lastError << ")...\n ";
                }
                return false;
            }
        }
        else
        {
            if (!IsMessageValid(pMessage))
            {
                WriteErrorResponse(hPipe);
                return false;
            }

            return true;
        }
    }

    // too big for the buffer
    return false;
}

void ProcessEvents(HANDLE hPipe)
{
    DWORD bufferSize = (1 << 16) + sizeof(IpcHeader);
    auto buffer = std::make_unique<uint8_t[]>(bufferSize);
    auto message = reinterpret_cast<ClrEventsMessage*>(buffer.get());

    DWORD readSize;
    for (;;)
    {
        if (!ReadEvents(hPipe, buffer.get(), bufferSize, readSize))
        {
            break;
        }

        // check the message based on the expected command
        if (message->CommandId == Commands::ClrEvents)
        {
            std::cout << "Events received: " << message->Size - sizeof(IpcHeader) << " / " << readSize << " bytes\n";

            WriteSuccessResponse(hPipe);
        }
        else
        {
            std::cout << "Invalid command (" << message->CommandId << ")...\n";

            WriteErrorResponse(hPipe);
            break;
        }
    }

    ::DisconnectNamedPipe(hPipe);
    ::CloseHandle(hPipe);
}


int RunServer(int pid)
{
    std::stringstream buffer;
    buffer << "\\\\.\\pipe\\DD_ETW_CLIENT_";
    buffer << pid;
    std::string pipeName = buffer.str();
    std::cout << "Exposing " << pipeName << "\n";

    // we are expecting only one "client" connecting: the Datadog Agent
    HANDLE hNamedPipe =
        ::CreateNamedPipeA(
            pipeName.c_str(),
            PIPE_ACCESS_DUPLEX,  // TODO: do we really need to send a response to the Agent?
            PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
            1,
            sizeof(SuccessResponse),
            (1 << 16) + sizeof(IpcHeader),
            0,
            nullptr);

    if (hNamedPipe == INVALID_HANDLE_VALUE)
    {
        return ShowLastError("Failed to create named pipe");
    }

    std::cout << "Listening...\n";

    if (!::ConnectNamedPipe(hNamedPipe, nullptr) && ::GetLastError() != ERROR_PIPE_CONNECTED)
    {
        return ShowLastError("ConnectNamedPipe failed...");
    }

    // process events as they are received
    ProcessEvents(hNamedPipe);

    return 0;
}

void CALLBACK RunServerCallback(PTP_CALLBACK_INSTANCE instance, PVOID context)
{
    int pid = reinterpret_cast<int>(context);
    RunServer(pid);
}

void StartServerAsync(int pid)
{
    // let a threadpool thread run the named pipe server
    if (!::TrySubmitThreadpoolCallback(RunServerCallback, reinterpret_cast<PVOID>(pid), nullptr))
    {
        ShowLastError("Impossible to run the server into the threadpool...");
    }
}

void SendRegistrationCommand(HANDLE hPipe, int pid, bool add)
{
    RegistrationProcessMessage message;
    if (add)
    {
        SetupRegisterCommand(message, pid);
    }
    else
    {
        SetupUnregisterCommand(message, pid);
    }

	DWORD written;
    if (!::WriteFile(hPipe, &message, sizeof(message), &written, nullptr))
    {
        ShowLastError("Failed to write to pipe");
        return;
    }
    ::FlushFileBuffers(hPipe);

    IpcHeader response;
    DWORD read;
    if (!::ReadFile(hPipe, &response, sizeof(response), &read, nullptr))
    {
        DWORD lastError = ::GetLastError();
        if (lastError == ERROR_PIPE_NOT_CONNECTED)
        {
            // expected after unregistration (i.e. !add) because the pipe will be closed by the Agent
            if (add)
            {
                std::cout << "Pipe no more connected: registration failed...\n";
            }
        }
        else
        {
            ShowLastError("Failed to read result");
            if (add)
            {
                std::cout << "Registration failed...\n";
            }
            else
            {
                std::cout << "Unregistration failed...\n";
            }
        }
    }
    else
    {
        if (add)
        {
            std::cout << "Registered!\n";
        }
        else
        {
            std::cout << "Unregistered!\n";
        }
    }

}

void SendRegistrationCommand(IpcClient* pClient, int pid, bool add)
{
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
        ShowLastError("Failed to write to pipe", code);
        return;
    }

    IpcHeader response;
    code = pClient->Read(&response, sizeof(response));
    if (code == NamedPipesCode::Success)
    {
        if (add)
        {
            std::cout << "Registered!\n";
        }
        else
        {
            std::cout << "Unregistered!\n";
        }
    }
    else
    {
        if (code == NamedPipesCode::NotConnected)
        {
            // expected after unregistration (i.e. !add) because the pipe will be closed by the Agent
            if (add)
            {
                std::cout << "Pipe no more connected: registration failed...\n";
            }
        }
        else
        {
            ShowLastError("Failed to read result", code);

            if (add)
            {
                std::cout << "Registration failed...\n";
            }
            else
            {
                std::cout << "Unregistration failed...\n";
            }
        }
    }
}


int main(int argc, char* argv[])
{
    int pid = -1;
    char* pipe = nullptr;
    if (!ParseCommandLine(argc, argv, pid, pipe))
    {
        if (pid == -1)
        {
            std::cout << "Missing -p <pid>...\n";
        }
        if (pipe == nullptr)
        {
            std::cout << "Missing -pipe <server name>...\n";
        }

        return -1;
    }

    std::cout << "\n";

    // start the server part to receive proxied ETW events
    //StartServerAsync(pid);

    std::stringstream buffer;
    buffer << "\\\\.\\pipe\\DD_ETW_CLIENT_";
    buffer << pid;
    std::string pipeName = buffer.str();
    std::cout << "Exposing " << pipeName << "\n";

    auto handler = std::make_unique<EtwEventsHandler>();
    auto server = IpcServer::StartAsync(
        pipeName,
        handler.get(),
        (1 << 16) + sizeof(IpcHeader),
        sizeof(SuccessResponse),
        16,
        500
        );
    if (server == nullptr)
    {
        std::cout << "Error creating the server to receive CLR events...\n";
        return -1;
    }

    // create the client part to send the registration command
    pipeName = "\\\\.\\pipe\\";
    pipeName += pipe;
    std::cout << "Contacting " << pipeName << "...\n";

    auto client = IpcClient::Connect(pipeName, 500);
    if (client == nullptr)
    {
        std::cout << "Impossible to connect to the ETW server...\n";
        return -2;
    }

    SendRegistrationCommand(client.get(), pid, true);

    std::cout << "Press ENTER to unregister...\n";
    std::string input;
    std::getline(std::cin, input);

    SendRegistrationCommand(client.get(), pid, false);

    client->Disconnect();
    handler->Stop();
    server->Stop();

    return 0;
}




