// dllmain.cpp : Defines the entry point for the DLL application.

#if _WIN32 && AMD64
#define CRASHTRACKING 1
#endif

#include "cor_profiler_class_factory.h"

#include "log.h"
#include "dynamic_dispatcher.h"
#include "util.h"

#if CRASHTRACKING
#include "werapi.h"
#endif 

#include "util.h"
#include <map>

#ifndef _WIN32
#undef EXTERN_C
#define EXTERN_C extern "C" __attribute__((visibility("default")))
#endif

using namespace datadog::shared::nativeloader;

IDynamicDispatcher* dispatcher;

#if CRASHTRACKING
std::wstring crashHandler;

struct CrashMetadata
{
    WCHAR* Environ;
    int32_t EnvironLength;
};

CrashMetadata crashMetadata;

std::wstring GetCurrentDllPath()
{
    wchar_t path[MAX_PATH];
    HMODULE hm = NULL;

    if (GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
        GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        (LPCWSTR)&GetCurrentDllPath, &hm) == 0) {
        return L"";  // Failed to get module handle
    }

    if (GetModuleFileNameW(hm, path, MAX_PATH) == 0) {
        return L"";  // Failed to get module filename
    }

    return std::wstring(path);
}

void GetEnvironBlock(WCHAR*& environmentVariables, int32_t& length)
{
    auto envStrings = GetEnvironmentStrings();

    std::wstring envBlock;

    if (envStrings == nullptr)
    {
        length = 2;
        environmentVariables = new WCHAR[length];
        environmentVariables[0] = L'\0';
        environmentVariables[1] = L'\0';
        return;
    }

    for (LPWCH env = envStrings; *env != L'\0'; env += wcslen(env) + 1)
    {
        if (wcsncmp(env, L"DD_", 3) == 0)
        {
            envBlock.append(env);
            envBlock.push_back('\0');
        }
    }

    // Environment block ends with a double null terminator
    envBlock.push_back('\0');

    if (envBlock.length() == 1)
    {
        // If the environment block was empty, we're still missing one null terminator
        envBlock.push_back('\0');
    }

    length = envBlock.length();
    environmentVariables = new WCHAR[length];
    memcpy(environmentVariables, envBlock.c_str(), length * sizeof(WCHAR));
}

std::wstring RegisterCrashHandler()
{
    auto dllPath = GetCurrentDllPath();

    if (dllPath.empty())
    {
        Log::Warn("Crashtracking - Could not register the crash handler: error when retrieving the path of the DLL");
        return std::wstring();
    }

    GetEnvironBlock(crashMetadata.Environ, crashMetadata.EnvironLength);

    HKEY hKey;
    LPCWSTR subKey = L"SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting\\RuntimeExceptionHelperModules";
    DWORD value = 1;

    // Open the key
    DWORD disposition;
    auto result = RegCreateKeyEx(HKEY_CURRENT_USER, subKey, 0, NULL, 0, KEY_SET_VALUE, NULL, &hKey, &disposition);

    if (result != ERROR_SUCCESS)
    {
        return std::wstring();
    }

    // Set the value
    RegSetValueEx(hKey, dllPath.c_str(), 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
    RegCloseKey(hKey);

    // TODO: handle coreclr
    bool isDotnetCore = true;
    HMODULE hModule = GetModuleHandle(L"coreclr.dll");

    if (hModule == NULL)
    {
        hModule = GetModuleHandle(L"clr.dll");
        isDotnetCore = false;

        if (hModule == NULL)
        {
            Log::Warn("Crashtracking - Failed to get module handle for coreclr.dll or clr.dll");
            return std::wstring();
        }
    }

    wchar_t buffer[MAX_PATH];

    if (GetModuleFileNameW(hModule, buffer, MAX_PATH) == 0)
    {
        Log::Warn("Crashtracking - Failed to get module filename");
        return std::wstring();
    }

    auto clrFileName = std::wstring(buffer);
    
    std::filesystem::path clrFileNamePath(clrFileName);
    auto clrDirectory = clrFileNamePath.parent_path();

    // Build the path to the DAC (mscordacwks.dll on .NET, mscordaccore.dll on .NET Core)
    std::wstring dacFileName = isDotnetCore ? L"mscordaccore.dll" : L"mscordacwks.dll";
    std::filesystem::path dacFilePath = clrDirectory / dacFileName;

    // Unregister the .NET handler
    Log::Debug("Crashtracking - Unregistering the .NET handler ", dacFilePath.c_str());
    auto unregisterDacHr = WerUnregisterRuntimeExceptionModule(dacFilePath.c_str(), (PVOID)hModule);

    if (FAILED(unregisterDacHr))
    {
        Log::Warn("Crashtracking - Failed to unregister the DAC handler: ", unregisterDacHr);
    }

    // Register our handler
    Log::Debug("Crashtracking - Registering the crash handler ", dllPath.c_str());
    auto registrationHr = WerRegisterRuntimeExceptionModule(dllPath.c_str(), &crashMetadata);

    // If we successfully unregistered the .NET handler, put it back in place
    if (SUCCEEDED(unregisterDacHr))
    {
        auto registrationHr2 = WerRegisterRuntimeExceptionModule(dacFilePath.c_str(), (PVOID)hModule);

        if (FAILED(registrationHr2))
        {
            Log::Warn("Crashtracking - Failed to re-register the DAC handler: ", registrationHr2);
        }
    }

    if (FAILED(registrationHr))
    {
        Log::Warn("Crashtracking - Could not register the crash handler: ", registrationHr);
        return std::wstring();
    }

    return dllPath;
}

#endif

EXTERN_C BOOL STDMETHODCALLTYPE DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    // Perform actions based on the reason for calling.
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    {
        // Initialize once for each new process.
        // Return FALSE to fail DLL load.

        constexpr const bool IsLogDebugEnabledDefault = false;
        bool isLogDebugEnabled;

        shared::WSTRING isLogDebugEnabledStr = shared::GetEnvironmentValue(EnvironmentVariables::DebugLogEnabled);

        // no environment variable set
        if (isLogDebugEnabledStr.empty())
        {
            Log::Info("No \"", EnvironmentVariables::DebugLogEnabled, "\" environment variable has been found.",
                " Enable debug log = ", IsLogDebugEnabledDefault, " (default).");

            isLogDebugEnabled = IsLogDebugEnabledDefault;
        }
        else
        {
            if (!shared::TryParseBooleanEnvironmentValue(isLogDebugEnabledStr, isLogDebugEnabled))
            {
                // invalid value for environment variable
                Log::Info("Non boolean value \"", isLogDebugEnabledStr, "\" for \"",
                    EnvironmentVariables::DebugLogEnabled, "\" environment variable.",
                    " Enable debug log = ", IsLogDebugEnabledDefault, " (default).");

                isLogDebugEnabled = IsLogDebugEnabledDefault;
            }
            else
            {
                // take environment variable into account
                Log::Info("Enable debug log = ", isLogDebugEnabled, " from (", EnvironmentVariables::DebugLogEnabled, " environment variable)");
            }
        }

        if (isLogDebugEnabled)
        {
            Log::EnableDebug(true);
        }

#if CRASHTRACKING
        bool telemetry_enabled = true;
        shared::TryParseBooleanEnvironmentValue(shared::GetEnvironmentValue(L"DD_INSTRUMENTATION_TELEMETRY_ENABLED"), telemetry_enabled);

        bool crashtracking_enabled = true;
        shared::TryParseBooleanEnvironmentValue(shared::GetEnvironmentValue(L"DD_CRASHTRACKING_ENABLED"), crashtracking_enabled);

        if (telemetry_enabled && crashtracking_enabled)
        {
            Log::Info("Crashtracking - Registering unhandled exception filter.");
            crashHandler = RegisterCrashHandler();
        }
        else
        {
            Log::Info("Crashtracking is disabled by configuration.");
        }
#endif

        dispatcher = new DynamicDispatcherImpl();
        dispatcher->LoadConfiguration(GetConfigurationFilePath());

        // *****************************************************************************************************************
        break;
    }

    case DLL_PROCESS_DETACH:
        // Perform any necessary cleanup.
        Log::Debug("DllMain: DLL_PROCESS_DETACH");

#if CRASHTRACKING
        if (!crashHandler.empty())
        {
            auto hr = WerUnregisterRuntimeExceptionModule(crashHandler.c_str(), &crashMetadata);
            Log::Debug("Crashtracking - Unregistering crash handler: ", hr);
            crashHandler.clear();
        }
#endif

        break;
    }
    return TRUE; // Successful DLL_PROCESS_ATTACH.
}

EXTERN_C HRESULT STDMETHODCALLTYPE DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv)
{
    Log::Debug("DllGetClassObject");

    // {846F5F1C-F9AE-4B07-969E-05C26BC060D8}
    const GUID CLSID_CorProfiler = { 0x846f5f1c, 0xf9ae, 0x4b07, {0x96, 0x9e, 0x5, 0xc2, 0x6b, 0xc0, 0x60, 0xd8} };

    if (ppv == nullptr || rclsid != CLSID_CorProfiler)
    {
        return E_FAIL;
    }

    auto factory = new CorProfilerClassFactory(dispatcher);
    if (factory == nullptr)
    {
        return E_FAIL;
    }

    return factory->QueryInterface(riid, ppv);
}

EXTERN_C HRESULT STDMETHODCALLTYPE DllCanUnloadNow()
{
    Log::Debug("DllCanUnloadNow");

    return dispatcher->DllCanUnloadNow();
}

#if CRASHTRACKING
LPWSTR ConcatenateEnvironmentBlocks(LPCWSTR envBlock1, LPCWSTR envBlock2) {
    // First, calculate the total length needed
    size_t lenFirstBlock = 0;
    size_t lenSecondBlock = 0;
    LPCWSTR p;

    // Calculate length of the first environment block
    p = envBlock1;
    while (*p) {
        size_t len = wcslen(p) + 1; // Length of the entry including null terminator
        lenFirstBlock += len;
        p += len;
    }

    // Calculate length of the second environment block
    p = envBlock2;
    while (*p) {
        size_t len = wcslen(p) + 1;
        lenSecondBlock += len;
        p += len;
    }

    // Allocate buffer for the merged environment block, add one for the final null character (double null termination)
    LPWSTR mergedEnvBlock = (LPWSTR)malloc((lenFirstBlock + lenSecondBlock + 1) * sizeof(WCHAR));
    if (!mergedEnvBlock) {
        // Handle allocation failure
        return NULL;
    }

    // Now copy the entries from both environment blocks
    memcpy(mergedEnvBlock, envBlock1, lenFirstBlock * sizeof(WCHAR));
    memcpy(mergedEnvBlock + lenFirstBlock, envBlock2, lenSecondBlock * sizeof(WCHAR));

    // Add final null character for double null termination
    mergedEnvBlock[lenFirstBlock + lenSecondBlock] = L'\0';

    return mergedEnvBlock;
}

extern "C"
{
    HRESULT __declspec(dllexport) OutOfProcessExceptionEventCallback(
        PVOID pContext,
        const PWER_RUNTIME_EXCEPTION_INFORMATION pExceptionInformation,
        BOOL* pbOwnershipClaimed,
        PWSTR pwszEventName,
        PDWORD pchSize,
        PDWORD pdwSignatureCount
    )
    {
        if (pbOwnershipClaimed != nullptr)
        {
            *pbOwnershipClaimed = FALSE;
        }

        // Get the pid from the exception
        auto pid = GetProcessId(pExceptionInformation->hProcess);
        auto tid = GetThreadId(pExceptionInformation->hThread);

        crashHandler = GetCurrentDllPath();

        CrashMetadata crashMetadata{};

        BOOL hasMetadata = ReadProcessMemory(pExceptionInformation->hProcess, pContext, &crashMetadata, sizeof(CrashMetadata), nullptr);
        BOOL hasEnviron = FALSE;

        WCHAR* envBlock = nullptr;

        if (hasMetadata && crashMetadata.EnvironLength > 0 && crashMetadata.Environ != nullptr)
        {
            envBlock = new WCHAR[crashMetadata.EnvironLength];
            hasEnviron = ReadProcessMemory(pExceptionInformation->hProcess, crashMetadata.Environ, envBlock, crashMetadata.EnvironLength * sizeof(WCHAR), nullptr);
        }

        auto currentEnv = GetEnvironmentStrings();

        if (hasEnviron)
        {
            envBlock = ConcatenateEnvironmentBlocks((LPCWSTR)envBlock, currentEnv);
        }
        else 
        {
            envBlock = currentEnv;
        }

        // Extract the directory using filesystem, then append dd-dotnet.exe
        std::filesystem::path p(crashHandler);
        auto directory = p.parent_path();
        auto ddDotnetPath = directory / "dd-dotnet.exe";

        std::stringstream ss;
        ss << ddDotnetPath << " createdump " << pid << " --crashthread " << tid;
        std::string commandLine = ss.str();

        // Convert command line to a wide string
        std::wstring wCommandLine(commandLine.begin(), commandLine.end());

        // Initialize the STARTUPINFO and PROCESS_INFORMATION structures
        STARTUPINFO si;
        PROCESS_INFORMATION pi;
        ZeroMemory(&si, sizeof(si));
        si.cb = sizeof(si);
        ZeroMemory(&pi, sizeof(pi));

        // Create the process
        if (!CreateProcessW(NULL, &wCommandLine[0], NULL, NULL, FALSE, CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT, envBlock, NULL, &si, &pi))
        {
            return S_OK;
        }

        // Wait for the process to exit
        WaitForSingleObject(pi.hProcess, INFINITE);

        return S_OK;
    }

    HRESULT __declspec(dllexport) OutOfProcessExceptionEventSignatureCallback(
        PVOID pContext,
        const PWER_RUNTIME_EXCEPTION_INFORMATION pExceptionInformation,
        DWORD dwIndex,
        PWSTR pwszName,
        PDWORD pchName,
        PWSTR pwszValue,
        PDWORD pchValue
    )
    {
        return E_NOTIMPL;
    }

    HRESULT __declspec(dllexport) OutOfProcessExceptionEventDebuggerLaunchCallback(
        PVOID pContext,
        const PWER_RUNTIME_EXCEPTION_INFORMATION pExceptionInformation,
        PBOOL pbIsCustomDebugger,
        PWSTR pwszDebuggerLaunch,
        PDWORD pchDebuggerLaunch,
        PBOOL pbIsDebuggerAutolaunch
    )
    {
        return E_NOTIMPL;
    }
}
#endif