// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// Inspired by https://github.com/zodiacon/Win10SysProgBookSamples/blob/master/Chapter18/CalculatorSvr/CalculatorSvr.cpp
// Another implementation without using ThreadPool API - https://learn.microsoft.com/en-us/windows/win32/ipc/multithreaded-pipe-server

#include <iostream>
#include <memory>
#include <sstream>
#include "stdio.h"
#include <string>
#include <windows.h>

#include "..\..\..\..\ProfilerEngine\Datadog.Profiler.Native.Windows\ETW\Protocol.h"
#include "..\..\..\..\ProfilerEngine\Datadog.Profiler.Native.Windows\ETW\IpcClient.h"
#include "..\..\..\..\ProfilerEngine\Datadog.Profiler.Native.Windows\ETW\IpcServer.h"
#include "..\..\..\..\ProfilerEngine\Datadog.Profiler.Native.Windows\ETW\EtwEventsHandler.h"
#include "EtwEventDumper.h"
#include "..\ConsoleLogger.h"


// This application should be used in conjunction with a profiled .NET application that is emitting ETW events
// for a certain scenario and serializes them into a .bevents file that can be replayed by dd-prof-etw-replay but
// also during integration tests via the AgentEtwProxy integrated in the MockDatadogAgent.
// NOTE: no 32/64 bit difference because the events are serialized as received
void ShowHelp()
{
    printf("\nDatadog CLR Events Client v1.1\n");
    printf("Simulate a .NET profiled application asking for ETW CLR events via named pipes.\n");
    printf("\n");
    printf("Usage: -pid <pid of a .NET process emitting events> -r <filename containing the recorded events>\n");
    printf("   Ex: -pid 1234 -r gc.bevents\n");
    printf("\n");
}

bool ParseCommandLine(int argc, char* argv[], int& pid, std::string& eventsFilename, bool& needHelp)
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
        if (strcmp(argv[i], "-r") == 0)
        {
            if (i == argc - 1)
            {
                return false;
            }

            eventsFilename = argv[i+1];
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

bool SendRegistrationCommand(IpcClient* pClient, int pid, bool add)
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
        return false;
    }

    IpcHeader response;
    code = pClient->Read(&response, sizeof(response));
    if (code == NamedPipesCode::Success)
    {
        bool success = (response.ResponseCode == (uint8_t)ResponseId::OK);
        if (add)
        {
            if (success)
            {
                std::cout << "Registered!\n";
            }
            else
            {
                std::cout << "Registration failed...\n";
                return false;
            }
        }
        else
        {
            if (success)
            {
                std::cout << "Unregistered!\n";
            }
            else
            {
                std::cout << "Unregistration failed...\n";
                return false;
            }
        }
    }
    else
    {
        if (code == NamedPipesCode::NotConnected)
        {
            // expected after unregistration (i.e. !add) because the pipe will be closed by the Agent
            std::cout << "Pipe is no more connected\n";
        }
        else
        if (code == NamedPipesCode::Broken)
        {
            // expected when the Agent crashes
            std::cout << "Pipe is broken\n";
        }
        else
        {
            ShowLastError("Failed to read result", code);
        }

        if (add)
        {
            std::cout << "Registration failed...\n";
        }
        else
        {
            std::cout << "Unregistration failed...\n";
        }
        return false;
    }

    return true;
}


int main(int argc, char* argv[])
{
    int pid = -1;
    const char* pipe = "DD_ETW_DISPATCHER";
    bool needHelp = false;
    std::string eventsFilename;
    if (!ParseCommandLine(argc, argv, pid, eventsFilename, needHelp))
    {
        if (pid == -1)
        {
            std::cout << "Missing -p <pid>...\n";
        }

        return -1;
    }

    if (needHelp)
    {
        ShowHelp();
        return 0;
    }

    FILE* pEventsFile = nullptr;
    if (!eventsFilename.empty())
    {
        pEventsFile = fopen(eventsFilename.c_str(), "wb");
        if (pEventsFile == nullptr)
        {
            std::cout << "Impossible to create the events file " << eventsFilename << "...\n";
            return -1;
        }
        std::cout << "Events will be saved into the file " << eventsFilename << "\n";
    }

    std::cout << "\n";

    // start the server part to receive proxied ETW events
    std::stringstream buffer;
    buffer << "\\\\.\\pipe\\DD_ETW_CLIENT_";
    buffer << pid;
    std::string pipeName = buffer.str();
    std::cout << "Exposing " << pipeName << "\n";

    EtwEventDumper eventDumper;
    std::unique_ptr<ConsoleLogger> logger = std::make_unique<ConsoleLogger>();
    auto handler = std::make_unique<EtwEventsHandler>(logger.get(), &eventDumper, pEventsFile);
    auto server = IpcServer::StartAsync(
        logger.get(),
        pipeName,
        handler.get(),
        (1 << 16) + sizeof(IpcHeader),
        sizeof(SuccessResponse),
        2,
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

    auto client = IpcClient::Connect(logger.get(), pipeName, 500);
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
    handler->Cleanup();
    server->Stop();

    return 0;
}




