#include "pal.h"

#if _WIN32
#include <Windows.h>
#else
#include "dlfcn.h"
#endif

#include "logging.h"

namespace datadog::shared::nativeloader
{

    void* LoadDynamicLibrary(std::string filePath)
    {
        Debug("LoadDynamicLibrary: ", filePath);

#if _WIN32
        HMODULE dynLibPtr = LoadLibrary(ToWSTRING(filePath).c_str());
        if (dynLibPtr == NULL || dynLibPtr == nullptr)
        {
            LPVOID msgBuffer;
            DWORD errorCode = GetLastError();

            FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                          NULL, errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPTSTR) &msgBuffer, 0, NULL);

            if (msgBuffer != NULL)
            {
                Warn("LoadDynamicLibrary: Error loading dynamic library '", filePath, "': ", (LPTSTR) msgBuffer);
                LocalFree(msgBuffer);
            }
        }
        return dynLibPtr;
#else
        void* dynLibPtr = dlopen(filePath.c_str(), RTLD_LOCAL | RTLD_LAZY);
        if (dynLibPtr == nullptr)
        {
            char* errorMessage = dlerror();
            Warn("LoadDynamicLibrary: Error loading dynamic library '", filePath, "': ", errorMessage);
        }
        return dynLibPtr;
#endif
    }

    void* GetExternalFunction(void* instance, const char* funcName)
    {
        Debug("GetExternalFunction: ", funcName);

        if (instance == nullptr)
        {
            Warn("GetExternalFunction: The module instance is null.");
            return nullptr;
        }

#if _WIN32
        FARPROC dynFunc = GetProcAddress((HMODULE) instance, funcName);
        if (dynFunc == NULL || dynFunc == nullptr)
        {
            LPVOID msgBuffer;
            DWORD errorCode = GetLastError();

            FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                          NULL, errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPTSTR) &msgBuffer, 0, NULL);

            if (msgBuffer != NULL)
            {
                Warn("GetExternalFunction: Error loading dynamic function '", funcName, "': ", (LPTSTR) msgBuffer);
                LocalFree(msgBuffer);
            }
        }
        return dynFunc;
#else
        void* dynFunc = dlsym(instance, funcName);
        if (dynFunc == nullptr)
        {
            char* errorMessage = dlerror();
            Warn("GetExternalFunction: Error loading dynamic function '", funcName, "': ", errorMessage);
        }
        return dynFunc;
#endif
    }

    bool FreeDynamicLibrary(void* handle)
    {
        Debug("FreeDynamicLibrary");

#if _WIN32
        return FreeLibrary((HMODULE) handle);
#else
        return dlclose(handle) == 0;
#endif
    }

    WSTRING GetEnvironmentValue(const WSTRING& name)
    {
        Debug("GetEnvironmentValue: ", name);

        /*
        Environment variables set with SetEnvironmentVariable() are not seen by
        getenv() (although GetEnvironmentVariable() sees changes done by
        putenv()), and since SetEnvironmentVariable() is preferable to putenv()
        because the former is thread-safe we use different apis for Windows implementation.
        */

#ifdef _WIN32
        const size_t max_buf_size = 4096;
        WSTRING buf(max_buf_size, 0);
        DWORD len = GetEnvironmentVariable(name.data(), buf.data(), (DWORD)(buf.size()));
        return Trim(buf.substr(0, len));
#else
        char* cstr = std::getenv(ToString(name).c_str());
        if (cstr == nullptr)
        {
            return WStr("");
        }
        std::string str(cstr);
        WSTRING wstr = ToWSTRING(str);
        return Trim(wstr);
#endif
    }

    bool SetEnvironmentValue(const WSTRING& name, const WSTRING& value)
    {
        Debug("SetEnvironmentValue: ", name, "=", value);

        /*
        Environment variables set with SetEnvironmentVariable() are not seen by
        getenv() (although GetEnvironmentVariable() sees changes done by
        putenv()), and since SetEnvironmentVariable() is preferable to putenv()
        because the former is thread-safe we use different apis for Windows implementation.
        */

#ifdef _WIN32
        return SetEnvironmentVariable(Trim(name).c_str(), value.c_str());
#else
        return setenv(ToString(name).c_str(), ToString(value).c_str(), 1) == 1;
#endif
    }

    std::vector<WSTRING> GetEnvironmentValues(const WSTRING& name, const wchar_t delim)
    {
        std::vector<WSTRING> values;
        for (WSTRING s : Split(GetEnvironmentValue(name), delim))
        {
            s = Trim(s);
            if (!s.empty())
            {
                values.push_back(s);
            }
        }
        return values;
    }

    std::vector<WSTRING> GetEnvironmentValues(const WSTRING& name)
    {
        return GetEnvironmentValues(name, L';');
    }

} // namespace datadog::shared::nativeloader
