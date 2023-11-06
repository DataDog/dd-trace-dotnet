// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "IpcClient.h"  // TODO: return codes should be defined in another shared header file
#include "IpcServer.h"
#include <iostream>
#include <memory>
#include <vector>
#include <optional>
#include <aclapi.h>

namespace
{
    template <class P> struct heap_deleter
    {
        typedef P* pointer;

        void operator()(pointer ptr) const
        {
            HeapFree(GetProcessHeap(), 0, ptr);
        }
    };
    typedef std::unique_ptr<SID, heap_deleter<SID>> sid_ptr;

    sid_ptr make_sid(size_t sidLength)
    {
        return sid_ptr(static_cast<sid_ptr::pointer>(HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sidLength)));
    }

    std::optional<sid_ptr> World()
    {
        PSID psid = nullptr;
        SID_IDENTIFIER_AUTHORITY sidIdAuthority = SECURITY_WORLD_SID_AUTHORITY;
        if (!AllocateAndInitializeSid(&sidIdAuthority, 1,
            SECURITY_WORLD_RID,
            0, 0, 0, 0, 0, 0, 0,
            &psid))
        {
            return std::nullopt;
        }
        return sid_ptr(static_cast<sid_ptr::pointer>(psid));
    }

    std::optional<sid_ptr> BuiltinAdministrator()
    {
        PSID psid = nullptr;
        SID_IDENTIFIER_AUTHORITY sidIdAuthority = SECURITY_NT_AUTHORITY;
        if (!AllocateAndInitializeSid(&sidIdAuthority, 1,
            SECURITY_BUILTIN_DOMAIN_RID,
            DOMAIN_ALIAS_RID_ADMINS,
            0, 0, 0, 0, 0, 0,
            &psid))
        {
            return std::nullopt;
        }
        return sid_ptr(static_cast<sid_ptr::pointer>(psid));
    }
}

IpcServer::IpcServer()
{
}

IpcServer::~IpcServer()
{
    Stop();
}

IpcServer::IpcServer(bool showMessages,
                     const std::string& portName,
                     INamedPipeHandler* pHandler,
                     uint32_t inBufferSize,
                     uint32_t outBufferSize,
                     uint32_t maxInstances,
                     uint32_t timeoutMS)
{
    _portName = portName;
    _inBufferSize = inBufferSize;
    _outBufferSize = outBufferSize;
    _maxInstances = maxInstances;
    _timeoutMS = timeoutMS;
    _pHandler = pHandler;

    _serverCount = 0;
}

std::unique_ptr<IpcServer> IpcServer::StartAsync(
    bool showMessages,
    const std::string& portName,
    INamedPipeHandler* pHandler,
    uint32_t inBufferSize,
    uint32_t outBufferSize,
    uint32_t maxInstances,
    uint32_t timeoutMS
    )
{
    if (pHandler == nullptr)
    {
        return nullptr;
    }

    auto server = std::make_unique<IpcServer>(
        showMessages, portName, pHandler, inBufferSize, outBufferSize, maxInstances, timeoutMS
        );

    // let a threadpool thread process the command; allowing the server to process more incoming commands
    if (!::TrySubmitThreadpoolCallback(StartCallback, (PVOID)server.get(), nullptr))
    {
        server->ShowLastError("Impossible to add the Start callback into the threadpool...");
        return nullptr;
    }

    return server;
 }

void IpcServer::Stop()
{
    _stopRequested.store(true);
}

LONG GetStringRegKey(HKEY hKey, const std::wstring& strValueName, std::wstring& strValue, const std::wstring& strDefaultValue)
{
    strValue = strDefaultValue;
    WCHAR szBuffer[512];
    DWORD dwBufferSize = sizeof(szBuffer);
    ULONG nError;
    nError = RegQueryValueExW(hKey, strValueName.c_str(), 0, NULL, (LPBYTE)szBuffer, &dwBufferSize);
    if (ERROR_SUCCESS == nError)
    {
        strValue = szBuffer;
    }
    return nError;
}

void CALLBACK IpcServer::StartCallback(PTP_CALLBACK_INSTANCE instance, PVOID context)
{
    IpcServer* pThis = reinterpret_cast<IpcServer*>(context);

    // TODO: there is no timeout on ConnectNamedPipe
    // so we would need to use the overlapped version to support _stopRequested :^(
    while (!pThis->_stopRequested.load())
    {
        HKEY hKey;
        LONG lRes = RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SOFTWARE\\Datadog\\Datadog Agent", 0, KEY_READ, &hKey);
        if (lRes != ERROR_SUCCESS)
        {
            pThis->ShowLastError("Failed to retrieve ddagentuser...");
            pThis->_pHandler->OnStartError();
            return;
        }

        std::wstring ddagentUser, ddagentDomain;
        if (GetStringRegKey(hKey, L"installedUser", ddagentUser, L"") != ERROR_SUCCESS)
        {
            RegCloseKey(hKey);
            pThis->ShowLastError("Failed to retrieve installedUser...");
            pThis->_pHandler->OnStartError();
            return;
        }

        if (GetStringRegKey(hKey, L"installedDomain", ddagentDomain, L"") != ERROR_SUCCESS)
        {
            RegCloseKey(hKey);
            pThis->ShowLastError("Failed to retrieve installedDomain...");
            pThis->_pHandler->OnStartError();
            return;
        }
        RegCloseKey(hKey);

        DWORD cbSid = 0;
        DWORD cchRefDomain = 0;
        SID_NAME_USE use;

        LookupAccountName(ddagentDomain.c_str(), ddagentUser.c_str(), nullptr, &cbSid, nullptr, &cchRefDomain, &use);
        sid_ptr sidDdagentUser = make_sid(cbSid);
        std::vector<wchar_t> refDomain;
        // +1 in case cchRefDomain == 0
        refDomain.resize(cchRefDomain + 1);
        if (!LookupAccountName(ddagentDomain.c_str(), ddagentUser.c_str(), sidDdagentUser.get(), &cbSid, &refDomain[0], &cchRefDomain, &use) || !IsValidSid(sidDdagentUser.get()))
        {
            pThis->ShowLastError("Failed to lookup ddagentuser SID...");
            pThis->_pHandler->OnStartError();
            return;
        }

        auto sidEveryone = World();
        if (!sidEveryone.has_value())
        {
            pThis->ShowLastError("Failed to create Everyone SID...");
            pThis->_pHandler->OnStartError();
            return;
        }
        auto sidAdmin = BuiltinAdministrator();
        if (!sidAdmin.has_value())
        {
            pThis->ShowLastError("Failed to create Builting\\Administrator SID...");
            pThis->_pHandler->OnStartError();
            return;
        }

        std::vector explicitAccess =
        {
            EXPLICIT_ACCESS
            {
                .grfAccessPermissions = FILE_GENERIC_READ | FILE_GENERIC_WRITE | SYNCHRONIZE,
                .grfAccessMode = SET_ACCESS,
                .grfInheritance = NO_INHERITANCE,
                .Trustee = TRUSTEE {
                    .pMultipleTrustee = nullptr,
                    .MultipleTrusteeOperation = NO_MULTIPLE_TRUSTEE,
                    .TrusteeForm = TRUSTEE_IS_SID,
                    .TrusteeType = TRUSTEE_IS_WELL_KNOWN_GROUP,
                    .ptstrName = reinterpret_cast<LPTSTR>(sidEveryone->get()),
                }
            },
            EXPLICIT_ACCESS
            {
                .grfAccessPermissions = FILE_GENERIC_READ | FILE_GENERIC_WRITE | SYNCHRONIZE,
                .grfAccessMode = SET_ACCESS,
                .grfInheritance = NO_INHERITANCE,
                .Trustee = TRUSTEE {
                    .pMultipleTrustee = nullptr,
                    .MultipleTrusteeOperation = NO_MULTIPLE_TRUSTEE,
                    .TrusteeForm = TRUSTEE_IS_SID,
                    .TrusteeType = TRUSTEE_IS_WELL_KNOWN_GROUP,
                    .ptstrName = reinterpret_cast<LPTSTR>(sidAdmin->get()),
                }
            },
            EXPLICIT_ACCESS
            {
                .grfAccessPermissions = FILE_GENERIC_READ | FILE_GENERIC_WRITE | SYNCHRONIZE,
                .grfAccessMode = SET_ACCESS,
                .grfInheritance = NO_INHERITANCE,
                .Trustee = TRUSTEE {
                    .pMultipleTrustee = nullptr,
                    .MultipleTrusteeOperation = NO_MULTIPLE_TRUSTEE,
                    .TrusteeForm = TRUSTEE_IS_SID,
                    .TrusteeType = TRUSTEE_IS_USER,
                    .ptstrName = reinterpret_cast<LPTSTR>(sidDdagentUser.get()),
                }
            },
        };

        PACL pACL = nullptr;
        if (SetEntriesInAcl(explicitAccess.size(), &explicitAccess[0], nullptr, &pACL) != ERROR_SUCCESS)
        {
            pThis->ShowLastError("Failed to SetEntriesInAcl...");
            pThis->_pHandler->OnStartError();
            return;
        }

        SECURITY_DESCRIPTOR securityDescriptor;
        if (!InitializeSecurityDescriptor(&securityDescriptor, SECURITY_DESCRIPTOR_REVISION))
        {
            pThis->ShowLastError("Failed to InitializeSecurityDescriptor...");
            pThis->_pHandler->OnStartError();
            return;
        }

        if (!SetSecurityDescriptorDacl(&securityDescriptor,
            TRUE,     // bDaclPresent flag   
            pACL,
            FALSE))   // not a default DACL 
        {
            pThis->ShowLastError("Failed to SetSecurityDescriptorDacl...");
            pThis->_pHandler->OnStartError();
            return;
        }

        SECURITY_ATTRIBUTES sa = {
            .nLength = sizeof(SECURITY_ATTRIBUTES),
            .lpSecurityDescriptor = &securityDescriptor,
            .bInheritHandle = FALSE,
        };

        // This works too but is akin to having no-security at all.
        //SECURITY_ATTRIBUTES g_sa = { 0 };
        //g_sa.nLength = sizeof(g_sa);
        //auto g_hsa = GlobalAlloc(GHND, SECURITY_DESCRIPTOR_MIN_LENGTH);
        //g_sa.lpSecurityDescriptor = GlobalLock(g_hsa);
        //g_sa.bInheritHandle = TRUE;
        //InitializeSecurityDescriptor(g_sa.lpSecurityDescriptor, 1);
        //SetSecurityDescriptorDacl(g_sa.lpSecurityDescriptor, TRUE, NULL, FALSE);

        HANDLE hNamedPipe =
            ::CreateNamedPipeA(
                pThis->_portName.c_str(),
                PIPE_ACCESS_DUPLEX,
                PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                pThis->_maxInstances,
                pThis->_outBufferSize,
                pThis->_inBufferSize,
                0,
                &sa
                );

        pThis->_serverCount++;

        if (hNamedPipe == INVALID_HANDLE_VALUE)
        {
            pThis->ShowLastError("Failed to create named pipe...");
            if (pThis->_showMessages)
            {
                std::cout << "--> for server #" << pThis->_serverCount << "...\n";
            }

            pThis->_pHandler->OnStartError();
            return;
        }

        if (pThis->_showMessages)
        {
            std::cout << "Listening to server #" << pThis->_serverCount << "...\n";
        }

        if (!::ConnectNamedPipe(hNamedPipe, nullptr) && ::GetLastError() != ERROR_PIPE_CONNECTED)
        {
            pThis->ShowLastError("ConnectNamedPipe failed...");
            ::CloseHandle(hNamedPipe);

            pThis->_pHandler->OnConnectError();
            return;
        }

        auto pServerInfo = new ServerInfo();
        pServerInfo->pThis = pThis;
        pServerInfo->hPipe = hNamedPipe;

        // let a threadpool thread process the read/write communication; allowing the server to process more incoming connections
        if (!::TrySubmitThreadpoolCallback(ConnectCallback, pServerInfo, nullptr))
        {
            delete pServerInfo;

            pThis->ShowLastError("Impossible to add the Connect callback into the threadpool...");
            pThis->_pHandler->OnStartError();
            return;
        }
    }
}

void CALLBACK IpcServer::ConnectCallback(PTP_CALLBACK_INSTANCE instance, PVOID context)
{
    ServerInfo* pInfo = reinterpret_cast<ServerInfo*>(context);
    IpcServer* pThis = pInfo->pThis;
    HANDLE hPipe = pInfo->hPipe;
    delete pInfo;

    // this is a blocking call until the communication ends on this named pipe
    pThis->_pHandler->OnConnect(hPipe);

    // cleanup
    ::DisconnectNamedPipe(hPipe);
    ::CloseHandle(hPipe);
}

void IpcServer::ShowLastError(const char* message, uint32_t lastError)
{
    if (_showMessages)
    {
        std::cout << message << " (" << lastError << ")\n";
    }
}
