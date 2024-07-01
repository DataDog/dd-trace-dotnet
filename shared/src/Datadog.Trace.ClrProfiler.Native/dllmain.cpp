// dllmain.cpp : Defines the entry point for the DLL application.
#include "cor_profiler_class_factory.h"

#include "log.h"
#include "dynamic_dispatcher.h"
#include "util.h"
#include "werapi.h"

#ifndef _WIN32
#undef EXTERN_C
#define EXTERN_C extern "C" __attribute__((visibility("default")))
#endif

using namespace datadog::shared::nativeloader;

IDynamicDispatcher* dispatcher;

std::wstring crashHandler;

std::wstring GetCurrentDllPath() {
    wchar_t path[MAX_PATH];
    HMODULE hm = NULL;

    if (GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
        GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        (LPCWSTR)&GetCurrentDllPath, &hm) == 0) {
        return L"";  // Failed to get module handle
    }

    if (GetModuleFileNameW(hm, path, sizeof(path)) == 0) {
        return L"";  // Failed to get module filename
    }

    return std::wstring(path);
}

void RegisterCrashHandler(const std::wstring& moduleName) {
    HKEY hKey;
    LPCWSTR subKey = L"SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting\\RuntimeExceptionHelperModules";
    DWORD value = 1;

    // Open the key
    DWORD disposition;
    LONG result = RegCreateKeyEx(HKEY_CURRENT_USER, subKey, 0, NULL, 0, KEY_SET_VALUE, NULL, &hKey, &disposition);
    if (result != ERROR_SUCCESS) {
        return;
    }

    // Set the value
    result = RegSetValueEx(hKey, moduleName.c_str(), 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
    if (result != ERROR_SUCCESS) {
        RegCloseKey(hKey);
        return;
    }

    RegCloseKey(hKey);
}


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

        Log::Debug("DllMain: DLL_PROCESS_ATTACH");
        Log::Debug("DllMain: Pointer size: ", 8 * sizeof(void*), " bits.");

#if _WIN32        
        Log::Debug("Registering unhandled exception filter.");

        crashHandler = GetCurrentDllPath();

        if (!crashHandler.empty())
        {
            RegisterCrashHandler(crashHandler);

            HMODULE hModule = GetModuleHandle(L"clr.dll");

            // TODO: Build the path dynamically
            auto unregisterDacHr = WerUnregisterRuntimeExceptionModule(L"C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\mscordacwks.dll", (PVOID)hModule);
            Log::Debug("Unregistering DAC handler : ", unregisterDacHr);

            auto registrationHr = WerRegisterRuntimeExceptionModule(crashHandler.c_str(), (PVOID)-1);
            Log::Debug("Registering Datadog crash handler: ", registrationHr);

            if (SUCCEEDED(unregisterDacHr))
            {
                // Put the .NET handler back in place
                registrationHr = WerRegisterRuntimeExceptionModule(L"C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\mscordacwks.dll", (PVOID)hModule);
                Log::Info("Re-register DAC handler: ", registrationHr);
            }
        }
        else
        {
            Log::Error("Could not register the crash handler: error when retrieving the path of the DLL");
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

        if (!crashHandler.empty())
        {
            auto hr = WerUnregisterRuntimeExceptionModule(crashHandler.c_str(), (PVOID)-1);
            Log::Debug("Unregistering Datadog crash handler: ", hr);
            crashHandler.clear();
        }

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
        if (!CreateProcess(NULL, &wCommandLine[0], NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, NULL, &si, &pi)) {
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