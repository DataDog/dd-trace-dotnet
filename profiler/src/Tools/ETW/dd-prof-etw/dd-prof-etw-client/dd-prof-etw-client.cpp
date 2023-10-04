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


void ShowHelp()
{
    printf("\nDatadog CLR Events Client v1.0\n");
    printf("Simulate a .NET profiled application asking for ETW CLR events via named pipes.\n");
    printf("\n");
    printf("Usage: -pid <pid of a .NET process emitting events> -pipe <server named pipe endpoint>\n");
    printf("   Ex: -pid 1234 -pipe DD_ETW_DISPATCHER\n");
    printf("\n");
}

bool ParseCommandLine(int argc, char* argv[], int& pid, char*& pipe, bool& needHelp)
{
    bool success = false;

    // show help if no parameter is provided
    if (argc == 1)
    {
        needHelp = true;
        return true;
    }

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
        else
        if (strcmp(argv[i], "-help") == 0)
        {
            needHelp = true;
            return true;
        }
    }

    return success;
}

void ShowLastError(const char* msg, DWORD error = ::GetLastError())
{
    printf("%s (%u)\n", msg, error);
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
    bool needHelp = false;
    if (!ParseCommandLine(argc, argv, pid, pipe, needHelp))
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

    if (needHelp)
    {
        ShowHelp();
        return 0;
    }

    std::cout << "\n";

    // start the server part to receive proxied ETW events
    std::stringstream buffer;
    buffer << "\\\\.\\pipe\\DD_ETW_CLIENT_";
    buffer << pid;
    std::string pipeName = buffer.str();
    std::cout << "Exposing " << pipeName << "\n";

    bool showMessages = true;
    auto handler = std::make_unique<EtwEventsHandler>(showMessages);
    auto server = IpcServer::StartAsync(
        showMessages,
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

    auto client = IpcClient::Connect(showMessages, pipeName, 500);
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




