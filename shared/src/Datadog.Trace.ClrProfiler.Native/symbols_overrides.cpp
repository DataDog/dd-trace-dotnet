

#define _GNU_SOURCE /* See feature_test_macros(7) */
#include "log.h"

#include "elfutils.hpp"
#include <link.h>
#include <memory>
#include <mutex>
#include <stdatomic.h>


struct HookBase
{
};

thread_local unsigned long long g_dd_inside_wrapped = 0;

extern "C" __attribute__((visibility("default")))  unsigned long long dd_inside_wrapped_functions2()
{
    return g_dd_inside_wrapped;
}

__attribute__((visibility("hidden")))
atomic_ullong __dd_dlopen_dlcose_calls_counter = 0;

unsigned long long dd_nb_calls_to_dlopen_dlclose()
{
    return __dd_dlopen_dlcose_calls_counter;
}

struct DlIteratePphdrHook : HookBase
{
    static constexpr auto name = "dl_iterate_phdr";
    using FuncType = decltype(&::dl_iterate_phdr);
    static inline FuncType ref{};

    static int hook(int (*callback)(struct dl_phdr_info* info, size_t size, void* data), void* data) noexcept
    {
        g_dd_inside_wrapped++;
        auto res = ref(callback, data);
        g_dd_inside_wrapped--;
        return res;
    }
};

void update_overrides();

struct DlopenHook : HookBase
{
    static constexpr auto name = "dlopen";
    using FuncType = decltype(&::dlopen);
    static inline FuncType ref{};

    static void* hook(const char* filename, int flags) noexcept
    {
        g_dd_inside_wrapped++;
        void* ret = ref(filename, flags);
        __dd_dlopen_dlcose_calls_counter++;
        update_overrides();
        g_dd_inside_wrapped--;
        return ret;
    }
};

struct DlcloseHook : HookBase
{
    static constexpr auto name = "dlclose";
    using FuncType = decltype(&::dlclose);
    static inline FuncType ref{};

    static int hook(void* handle) noexcept
    {
        auto ret = ref(handle);
        __dd_dlopen_dlcose_calls_counter++;
        return ret;
    }
};
struct DatadogCheckFn : HookBase
{
    static constexpr auto name = "dd_inside_wrapped_functions2";
    using FuncType = decltype(&dd_inside_wrapped_functions2);
    static inline FuncType ref{};

    static int hook(void* handle) noexcept
    {
        return dd_inside_wrapped_functions2();
    }
};

std::mutex g_mutex;
std::unique_ptr<ddprof::SymbolOverrides> g_symbol_overrides;

template <typename T>
void register_hook()
{
    g_symbol_overrides->register_override(T::name, reinterpret_cast<uintptr_t>(&T::hook),
                                          reinterpret_cast<uintptr_t*>(&T::ref));
}

void register_hooks()
{
    register_hook<DlIteratePphdrHook>();
    register_hook<DatadogCheckFn>();
    register_hook<DlcloseHook>();
    register_hook<DlopenHook>();
}

void setup_overrides()
{
    std::lock_guard const lock(g_mutex);

    Log::Info("Setting up the symbols overriding mechanism");
    if (!g_symbol_overrides)
    {
        g_symbol_overrides = std::make_unique<ddprof::SymbolOverrides>();
        register_hooks();
    }

    g_symbol_overrides->apply_overrides();
}

void restore_overrides()
{
    std::lock_guard const lock(g_mutex);

    if (g_symbol_overrides)
    {
        g_symbol_overrides->restore_overrides();
        g_symbol_overrides.reset();
    }
}

void update_overrides()
{
    std::lock_guard const lock(g_mutex);

    if (g_symbol_overrides)
    {
        g_symbol_overrides->update_overrides();
    }
}