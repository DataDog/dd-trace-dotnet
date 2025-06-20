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

#if LINUX
/// \brief Checks if the current system has a buggy implementation of dlclose (glibc 2.34-2.36).
/// \return True if the system is affected by the buggy dlclose, false otherwise.
bool DynamicLibraryBase::HasBuggyDlclose() const
{
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

    // Cache the value statically to avoid repeated checks
    static bool result = []() {
        // Need to check whether we can close libraries or not
        // But we compile for both musl and glibc, so need to manually
        // try to load the glibc function gnu_get_libc_version
        void* handle = dlopen("libc.so.6", RTLD_LAZY);
        if (!handle) {
            // Likely not glibc (e.g. musl)
            return false;
        }

        using gnu_get_libc_version_fn = const char* (*)();
        auto func = (gnu_get_libc_version_fn)dlsym(handle, "gnu_get_libc_version");
        if (!func) {
            // We do have glibc, but the function is not available...
            // This shouldn't happen given it's available since 2.1 and we require 2.17
            // so overall, a bit weird... Don't close the handle, just in case, and
            // treat it as faulty
            // dlclose(handle);
            return true;
        }

        // Check if it's one of the buggy versions
        const auto version = std::string(func());
        // Try to "Save" the valuea as en environment variable so that we can report it later if necessary
        ::shared::SetEnvironmentValue(WStr("DD_INTERNAL_PROFILING_NATIVE_ENGINE_PATH"), version);

#if ARM64
        const auto is_buggy = (version == "2.34" || version == "2.35" || version == "2.36");
#else
        const auto is_buggy = (version == "2.34" || version == "2.35");
#endif

        if (!is_buggy) {
            // Not buggy, so we can close the handle
            dlclose(handle);
            return false;
        }

        // buggy, so we can't close the handle
        return true;
    }();

    return result;
}
#endif

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
#if LINUX
    if (HasBuggyDlclose())
    {
        _logger->Warn("Unload: Skipping unload of dynamic library '", _filePath,
                      "' due to buggy dlclose implementation on this system. ",
                      "GLIBC version is likely 2.34-2.36, which has a TLS-reuse bug that can cause ",
                      "crashes when unloading shared libraries.");
        return false;
    }

#endif // if LINUX
    auto result = dlclose(_instance) == 0;
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