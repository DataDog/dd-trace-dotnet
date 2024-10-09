#include "crashhandler.h"
#include <string>
#include "util.h"
#include "WerApi.h"
#include <map>

namespace datadog::shared::nativeloader
{
    // Get the path of the current module
    std::wstring GetCurrentDllPath()
    {
        wchar_t path[MAX_PATH];
        HMODULE hm = NULL;

        if (GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
            GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            (LPCWSTR)&GetCurrentDllPath, &hm) == 0)
        {
            Log::Warn("Crashtracking - Failed to get the current module handle: ", GetLastError());
            return std::wstring();
        }

        if (GetModuleFileNameW(hm, path, MAX_PATH) == 0)
        {
            Log::Warn("Crashtracking - Failed to get the current module filename: ", GetLastError());
            return std::wstring();
        }

        return std::wstring(path);
    }

    bool RegistryValueExists(HKEY rootKey, LPCWSTR subKey, LPCWSTR valueName)
    {
        HKEY hKey;
        auto result = RegOpenKeyEx(rootKey, subKey, 0, KEY_QUERY_VALUE, &hKey);
        
        if (result == ERROR_SUCCESS)
        {
            DWORD dataSize = 0;
            DWORD valueType = 0;
            result = RegQueryValueEx(hKey, valueName, NULL, &valueType, NULL, &dataSize);
            RegCloseKey(hKey);

            return result == ERROR_SUCCESS;
        }

        return false;
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
        FreeEnvironmentStrings(envStrings);
    }

    // Merge two environment blocks. The first one takes precedence.
    std::vector<WCHAR> MergeEnvironmentBlocks(LPCWSTR envBlock1, LPCWSTR envBlock2)
    {
        // Define a case-insensitive comparator for keys
        struct CaseInsensitiveCompare
        {
            bool operator()(const std::wstring& lhs, const std::wstring& rhs) const
            {
                return _wcsicmp(lhs.c_str(), rhs.c_str()) < 0;
            }
        };

        // Map to hold environment variables with case-insensitive keys
        std::map<std::wstring, std::wstring, CaseInsensitiveCompare> envMap;

        // Helper lambda to parse an environment block into the map
        auto parseEnvBlock = [&envMap](LPCWSTR envBlock)
            {
                if (envBlock)
                {
                    LPCWSTR curr = envBlock;
                    while (*curr)
                    {
                        std::wstring entry = curr;
                        size_t pos = entry.find(L'=');
                        if (pos != std::wstring::npos)
                        {
                            std::wstring key = entry.substr(0, pos);
                            std::wstring value = entry.substr(pos + 1);
                            envMap[key] = std::move(value); // Insert or overwrite the key
                        }
                        // Move to the next null-terminated string
                        curr += entry.length() + 1;
                    }
                }
            };

        // Parse envBlock2 first so that envBlock1 values take precedence
        parseEnvBlock(envBlock2);
        parseEnvBlock(envBlock1);

        // Reconstruct the combined environment block
        std::vector<WCHAR> result;
        result.reserve(envMap.size());

        for (const auto& kv : envMap)
        {
            std::wstring entry = kv.first + L"=" + kv.second;
            result.insert(result.end(), entry.begin(), entry.end());
            result.push_back(L'\0'); // Null terminator for the entry
        }
        result.push_back(L'\0'); // Double null terminator at the end

        return result;
    }

    std::unique_ptr<CrashHandler> CrashHandler::Create()
    {
        std::unique_ptr<CrashHandler> crashHandler(new CrashHandler());

        if (crashHandler->Register())
        {
            return crashHandler;
        }

        return nullptr;
    }

    CrashHandler::~CrashHandler()
    {
        if (!_crashHandler.empty())
        {
            auto hr = WerUnregisterRuntimeExceptionModule(_crashHandler.c_str(), &_context);
            Log::Debug("Crashtracking - Unregistering crash handler: ", hr);
            _crashHandler.clear();
        }

        if (_context.Environ != nullptr)
        {
            delete[] _context.Environ;
        }

        _context.Environ = nullptr;
        _context.EnvironLength = 0;
    }

    CrashHandler::CrashHandler()
        : _context(),
        _crashHandler()
    {
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
        // in SOFTWARE\Microsoft\Windows\Windows Error Reporting\RuntimeExceptionHelperModules.
        // The key can be located either in HKLM or HKCU. The MSI will create the value in HKLM,
        // but if it's missing we add it to HKCU.
        HKEY hKey;
        LPCWSTR subKey = L"SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting\\RuntimeExceptionHelperModules";
        DWORD value = 1;

        if (!RegistryValueExists(HKEY_LOCAL_MACHINE, subKey, dllPath.c_str()))
        {
            // Open the key
            DWORD disposition;
            auto result = RegCreateKeyEx(HKEY_CURRENT_USER, subKey, 0, NULL, 0, KEY_SET_VALUE, NULL, &hKey, &disposition);

            if (result != ERROR_SUCCESS)
            {
                // Failing to set the registry is not a fatal error: in the worst case scenario the crash handler just won't be invoked by WER
                Log::Warn("Crashtracking - Failed to create registry key: ", GetLastError());
            }
            else
            {
                // Set the value
                result = RegSetValueEx(hKey, dllPath.c_str(), 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));

                if (result != ERROR_SUCCESS)
                {
                    Log::Warn("Crashtracking - Failed to set registry value: ", GetLastError());
                }

                RegCloseKey(hKey);
            }
        }

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
            Log::Warn("Crashtracking - Failed to get module filename: ", GetLastError());
            return false;
        }

        auto clrFileName = std::wstring(buffer);

        fs::path clrFileNamePath(clrFileName);
        auto clrDirectory = clrFileNamePath.parent_path();

        // Build the path to the DAC (mscordacwks.dll on .NET, mscordaccore.dll on .NET Core)
        std::wstring dacFileName = isDotnetCore ? L"mscordaccore.dll" : L"mscordacwks.dll";
        fs::path dacFilePath = clrDirectory / dacFileName;

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
            Log::Debug("Crashtracking - OutOfProcessExceptionEventCallback");

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

            std::vector<WCHAR> envBlock;

            if (hasContext && context.EnvironLength > 0 && context.Environ != nullptr)
            {
                envBlock.resize(context.EnvironLength * sizeof(WCHAR));
                hasEnviron = ReadProcessMemory(pExceptionInformation->hProcess, context.Environ, envBlock.data(), context.EnvironLength * sizeof(WCHAR), nullptr);
                
                if (!hasEnviron)
                {
                    Log::Warn("Crashtracking - Failed to read the environment block from crashing process");
                }
            }

            // Merge them with the current environment variables
            auto currentEnv = GetEnvironmentStrings();

            if (hasEnviron)
            {
                envBlock = MergeEnvironmentBlocks((LPCWSTR)envBlock.data(), currentEnv);
            }
            else
            {
                envBlock.assign(currentEnv, currentEnv + envBlock.size());
            }

            FreeEnvironmentStrings(currentEnv);

            // Create the command-line for dd-dotnet
            fs::path p(GetCurrentDllPath());
            auto directory = p.parent_path();
            auto ddDotnetPath = directory / "dd-dotnet.exe";

            if (!fs::exists(ddDotnetPath))
            {
                Log::Error("Crashtracking - dd-dotnet.exe not found at ", ddDotnetPath.c_str());
                return S_OK;
            }

            std::wstringstream ss;
            ss << ddDotnetPath << " createdump " << pid << " --crashthread " << tid;
            auto commandLine = ss.str();

            // Spawn dd-dotnet
            STARTUPINFO si;
            PROCESS_INFORMATION pi;
            ZeroMemory(&si, sizeof(si));
            si.cb = sizeof(si);
            ZeroMemory(&pi, sizeof(pi));

            if (!CreateProcessW(NULL, &commandLine[0], NULL, NULL, FALSE, CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT, envBlock.data(), NULL, &si, &pi))
            {
                Log::Error("Crashtracking - Failed to spawn dd-dotnet.exe: ", GetLastError());
                return S_OK;
            }

            // Wait for the process to exit
            WaitForSingleObject(pi.hProcess, INFINITE);

            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);

            Log::Debug("Crashtracking - Crash report sent.");

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