// Copyright(c) 2015-present, Gabi Melman & spdlog contributors.
// Distributed under the MIT License (http://opensource.org/licenses/MIT)

#pragma once

#if defined(SPDLOG_NO_TLS)
#error "This header requires thread local storage support, but SPDLOG_NO_TLS is defined."
#endif

#if defined(DD_SPDLOG_NO_MDC)
#error "mdc.h was included but DD_SPDLOG_NO_MDC is defined. An include site for spdlog/mdc.h is not gated by DD_SPDLOG_NO_MDC; patch pattern_formatter-inl.h (or the new include site) and update PatchSpdlogFile in tracer/build/_build/UpdateVendors/VendoredDependency.cs."
#endif

#include <map>
#include <string>

#include <spdlog/common.h>

// MDC is a simple map of key->string values stored in thread local storage whose content will be
// printed by the loggers. Note: Not supported in async mode (thread local storage - so the async
// thread pool have different copy).
//
// Usage example:
// spdlog::mdc::put("mdc_key_1", "mdc_value_1");
// spdlog::info("Hello, {}", "World!");  // => [2024-04-26 02:08:05.040] [info]
// [mdc_key_1:mdc_value_1] Hello, World!

namespace spdlog {
class SPDLOG_API mdc {
public:
    using mdc_map_t = std::map<std::string, std::string>;

    static void put(const std::string &key, const std::string &value) {
        get_context()[key] = value;
    }

    static std::string get(const std::string &key) {
        auto &context = get_context();
        auto it = context.find(key);
        if (it != context.end()) {
            return it->second;
        }
        return "";
    }

    static void remove(const std::string &key) { get_context().erase(key); }

    static void clear() { get_context().clear(); }

    static mdc_map_t &get_context() {
        static thread_local mdc_map_t context;
        return context;
    }
};

}  // namespace spdlog
