#include "crashhandler.h"
#include <string>
#include "util.h"
#include "WerApi.h"

namespace datadog::shared::nativeloader
{
    // Get the path of the current module
    std::wstring GetCurrentDllPath()
    {
        wchar_t path[MAX_PATH];
        HMODULE hm = NULL;

        if (GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
            GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            (LPCWSTR)&GetCurrentDllPath, &hm) == 0) {
            return std::wstring();  // Failed to get module handle
        }

        if (GetModuleFileNameW(hm, path, MAX_PATH) == 0) {
            return std::wstring();  // Failed to get module filename
        }

        return std::wstring(path);
    }

    // Capture all the environment variables starting with "DD_"
    void GetDatadogEnvironmentBlock(WCHAR*& environmentVariables, int32_t& length)
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

        length = static_cast<int32_t>(envBlock.length());
        environmentVariables = new WCHAR[length];
        memcpy(environmentVariables, envBlock.c_str(), length * sizeof(WCHAR));
    }

    // Concatenate two environment blocks
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

    bool CrashHandler::Register()
    {
        if (!_crashHandler.empty())
        {
            Log::Warn("Crashtracking - Trying to initialize the crash handler twice");
            return false;
        }

        auto dllPath = GetCurrentDllPath();

        if (dllPath.empty())
        {
            Log::Warn("Crashtracking - Could not register the crash handler: error when retrieving the path of the DLL");
            return false;
        }

        GetDatadogEnvironmentBlock(_context.Environ, _context.EnvironLength);

        // Register the crash handler in the registry
        // Windows expects a DWORD value with the full path of the DLL as the name, 
        // in HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\Windows Error Reporting\RuntimeExceptionHelperModules
        HKEY hKey;
        LPCWSTR subKey = L"SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting\\RuntimeExceptionHelperModules";
        DWORD value = 1;

        // Open the key
        DWORD disposition;
        auto result = RegCreateKeyEx(HKEY_CURRENT_USER, subKey, 0, NULL, 0, KEY_SET_VALUE, NULL, &hKey, &disposition);

        if (result != ERROR_SUCCESS)
        {
            return false;
        }

        // Set the value
        RegSetValueEx(hKey, dllPath.c_str(), 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
        RegCloseKey(hKey);

        // The profiler API is not initialized yet, so we look for clr.dll/coreclr.dll
        // to know if we're running with .NET Framework or .NET Core
        bool isDotnetCore = true;
        HMODULE hModule = GetModuleHandle(L"coreclr.dll");

        if (hModule == NULL)
        {
            hModule = GetModuleHandle(L"clr.dll");
            isDotnetCore = false;

            if (hModule == NULL)
            {
                Log::Warn("Crashtracking - Failed to get module handle for coreclr.dll or clr.dll");
                return false;
            }
        }

        wchar_t buffer[MAX_PATH];

        if (GetModuleFileNameW(hModule, buffer, MAX_PATH) == 0)
        {
            Log::Warn("Crashtracking - Failed to get module filename");
            return false;
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
        auto registrationHr = WerRegisterRuntimeExceptionModule(dllPath.c_str(), &_context);

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
            return false;
        }

        _crashHandler = dllPath;
        return true;
    }

    bool CrashHandler::Unregister()
    {
        if (!_crashHandler.empty())
        {
            auto hr = WerUnregisterRuntimeExceptionModule(_crashHandler.c_str(), &_context);
            Log::Debug("Crashtracking - Unregistering crash handler: ", hr);
            _crashHandler.clear();

            return SUCCEEDED(hr);
        }

        return false;
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
                // We don't claim the ownership of the crash.
                // This way, the original crash handler registered by .NET will be invoked,
                // and we don't affect the normal behavior.
                *pbOwnershipClaimed = FALSE;
            }

            // Get the pid and tid from the exception
            auto pid = GetProcessId(pExceptionInformation->hProcess);
            auto tid = GetThreadId(pExceptionInformation->hThread);

            // Read the environment variables saved in the crashing process
            WerContext context{};

            BOOL hasContext = ReadProcessMemory(pExceptionInformation->hProcess, pContext, &context, sizeof(context), nullptr);
            BOOL hasEnviron = FALSE;

            WCHAR* envBlock = nullptr;

            if (hasContext && context.EnvironLength > 0 && context.Environ != nullptr)
            {
                envBlock = new WCHAR[context.EnvironLength];
                hasEnviron = ReadProcessMemory(pExceptionInformation->hProcess, context.Environ, envBlock, context.EnvironLength * sizeof(WCHAR), nullptr);
            }

            // Merge them with the current environment variables
            auto currentEnv = GetEnvironmentStrings();

            if (hasEnviron)
            {
                envBlock = ConcatenateEnvironmentBlocks((LPCWSTR)envBlock, currentEnv);
            }
            else
            {
                envBlock = currentEnv;
            }

            // Create the command-line for dd-dotnet
            std::filesystem::path p(GetCurrentDllPath());
            auto directory = p.parent_path();
            auto ddDotnetPath = directory / "dd-dotnet.exe";

            std::stringstream ss;
            ss << ddDotnetPath << " createdump " << pid << " --crashthread " << tid;
            std::string commandLine = ss.str();

            // Convert command line to a wide string
            std::wstring wCommandLine(commandLine.begin(), commandLine.end());

            // Spawn dd-dotnet
            STARTUPINFO si;
            PROCESS_INFORMATION pi;
            ZeroMemory(&si, sizeof(si));
            si.cb = sizeof(si);
            ZeroMemory(&pi, sizeof(pi));

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
}