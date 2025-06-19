#include "dynamic_library_base.h"

#if _WIN32
#include <Windows.h>
#else
#include <dlfcn.h>
#endif

#include "../../../shared/src/native-src/dd_filesystem.hpp"
#include "../../../shared/src/native-src/logger.h"
#include "../../../shared/src/native-src/string.h"

namespace datadog::shared
{

DynamicLibraryBase::DynamicLibraryBase(const std::string& filePath, Logger* logger) :
    _filePath{filePath}, _instance{nullptr}, _logger{logger}
{
}

void* DynamicLibraryBase::GetFunction(const std::string& funcName)
{
    _logger->Debug("GetFunction: ", funcName);

    if (_instance == nullptr)
    {
        _logger->Warn("GetFunction: The module instance is null.");
        return nullptr;
    }

#if _WIN32
    FARPROC dynFunc = GetProcAddress((HMODULE) _instance, funcName.c_str());
    if (dynFunc == NULL)
    {
        LPVOID msgBuffer;
        DWORD errorCode = GetLastError();

        FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, NULL,
                      errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPTSTR) &msgBuffer, 0, NULL);

        if (msgBuffer != NULL)
        {
            _logger->Warn("GetFunction: Error loading dynamic function '", funcName, "': ", (LPTSTR) msgBuffer);
            LocalFree(msgBuffer);
        }
    }
    return dynFunc;
#else
    void* dynFunc = dlsym(_instance, funcName.c_str());
    if (dynFunc == nullptr)
    {
        char* errorMessage = dlerror();
        _logger->Warn("GetFunction: Error loading dynamic function '", funcName, "': ", errorMessage);
    }
    return dynFunc;
#endif
}

bool DynamicLibraryBase::Load()
{
    _logger->Debug("Load: ", _filePath);

#if _WIN32
    _instance = LoadLibraryEx(::shared::ToWSTRING(_filePath).c_str(),
                              NULL, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR);
    if (_instance == NULL)
    {
        LPVOID msgBuffer;
        DWORD errorCode = GetLastError();

        FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, NULL,
                      errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPTSTR) &msgBuffer, 0, NULL);

        if (msgBuffer != NULL)
        {
            _logger->Warn("Load: Error loading dynamic library '", _filePath, "': ", (LPTSTR) msgBuffer);
            LocalFree(msgBuffer);
        }
    }

#else
    _instance = dlopen(_filePath.c_str(), RTLD_LOCAL | RTLD_LAZY);
    if (_instance == nullptr)
    {
        char* errorMessage = dlerror();
        _logger->Warn("Load: Error loading dynamic library '", _filePath, "': ", errorMessage);
    }
#endif

    OnInitialized();
    return _instance != nullptr;
}

bool DynamicLibraryBase::Unload()
{
    _logger->Debug("Unloading ", _filePath);
    if (_instance == nullptr)
    {
        _logger->Warn("Unload: Unable to unload dynamic library '", _filePath,
                      ". Reason: An issue occured while loading it. See previous message.");
        return false;
    }

#if _WIN32
    auto result = FreeLibrary((HMODULE) _instance);
#else
    // In certain versions of glibc, there is a TLS-reuse bug that can cause crashes when unloading shared libraries.
    // The bug was introduced in 2.34, fixed in 2.36 on x86-64, and fixed in 2.37 on aarch64.
    // See https://sourceware.org/git/gitweb.cgi?p=glibc.git;h=3921c5b40f293c57cb326f58713c924b0662ef59
    // Explanation in Fedora where we spotted it: https://bugzilla.redhat.com/show_bug.cgi?id=2251557
    //
    // 2.34 shipped with a regression: after a dlclose() of a library that carried dynamic-TLS, the loader could reuse
    // the same “module-ID” for a different library without first clearing the associated DTV (Dynamic Thread Vector)
    // entry. The next time any code accessed that TLS slot it could read or write an unmapped address → SIGSEGV.
    //
    // This manifested as a crash in the WAF when we called `ddwaf_context_info` on arm64. It explicitly happens
    // on arm64 when we unload the continuous profiler (because it's not supported).
    //
    // It manifests in this scenario, because ddwaf_context_init starts like this:
    //   +128  bl   __tls_get_addr     ; ask glibc for the TLS slot for libddwaf
    //   +136  mrs  x11, TPIDR_EL0     ; TLS base for this thread
    //   +140  ldrb w9, [x11, x0]      ; <–– boom if x0 points to a stale DTV entry
    //
    // When we unload the continuous profiler and call dlcose, it causes the loader to hand out a recycled module-ID
    // to libddwaf. When ddwaf_context_init tries to access TLS in the `ldrb` instruction, `x11 + x0` is outside
    // every mapped version, and so crashes.
    //
    // Note that although calling dlclose with the continuous profiler may trigger the issue (the actual crash is flaky
    // depending on load/unload timing and address layout), unloading _any_ library that is is built with
    // `__thread`/`thread_local` data could trigger the crash. To minimize the risk of hitting this issue,

    // TODO: Just force block dlclose for testing - we will gate this on glibc later

#if ARM64
    auto requiredGlibcMinorVersion = 37;
#else
    auto requiredGlibcMinorVersion = 36;
#endif

    auto result = true;
    // auto result = dlclose(_instance) == 0;
#endif

    if (!result)
    {
        _logger->Warn("Unload: DynamicInstanceImpl::~DynamicInstanceImpl: Error unloading: ", _filePath, " dynamic library.");
    }
    _instance = nullptr;

    return result;
}

const std::string& DynamicLibraryBase::GetFilePath()
{
    return _filePath;
}

} // namespace datadog::shared