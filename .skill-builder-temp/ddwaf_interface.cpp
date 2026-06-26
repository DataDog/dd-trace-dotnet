// Unless explicitly stated otherwise all files in this repository are
// dual-licensed under the Apache-2.0 License or BSD-3-Clause License.
//
// This product includes software developed at Datadog (https://www.datadoghq.com/).
// Copyright 2021 Datadog, Inc.

#include <algorithm>
#include <chrono>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <exception>
#include <limits>
#include <string_view>
#include <type_traits>
#include <utility>
#include <vector>

#include "builder/waf_builder.hpp"
#include "clock.hpp"
#include "configuration/common/raw_configuration.hpp"
#include "context.hpp"
#include "ddwaf.h"
#include "json_utils.hpp"
#include "log.hpp"
#include "memory_resource.hpp"
#include "object.hpp"
#include "object_type.hpp"
#include "pointer.hpp"
#include "re2.h"
#include "ruleset_info.hpp"
#include "user_resource.hpp"
#include "utils.hpp"
#include "version.hpp"
#include "waf.hpp"

using namespace ddwaf;

// Object type compatibility
static_assert(static_cast<uint8_t>(object_type::invalid) == DDWAF_OBJ_INVALID);
static_assert(static_cast<uint8_t>(object_type::null) == DDWAF_OBJ_NULL);
static_assert(static_cast<uint8_t>(object_type::boolean) == DDWAF_OBJ_BOOL);
static_assert(static_cast<uint8_t>(object_type::int64) == DDWAF_OBJ_SIGNED);
static_assert(static_cast<uint8_t>(object_type::uint64) == DDWAF_OBJ_UNSIGNED);
static_assert(static_cast<uint8_t>(object_type::float64) == DDWAF_OBJ_FLOAT);
static_assert(static_cast<uint8_t>(object_type::string) == DDWAF_OBJ_STRING);
static_assert(static_cast<uint8_t>(object_type::small_string) == DDWAF_OBJ_SMALL_STRING);
static_assert(static_cast<uint8_t>(object_type::literal_string) == DDWAF_OBJ_LITERAL_STRING);
static_assert(static_cast<uint8_t>(object_type::array) == DDWAF_OBJ_ARRAY);
static_assert(static_cast<uint8_t>(object_type::map) == DDWAF_OBJ_MAP);

// Object compatibility

// detail::object == ddwaf_object
static_assert(sizeof(detail::object) == sizeof(ddwaf_object));
static_assert(offsetof(detail::object, type) == offsetof(ddwaf_object, type));
static_assert(offsetof(detail::object, via) == offsetof(ddwaf_object, via));

// detail::object_kv == _ddwaf_object_kv
static_assert(sizeof(detail::object_kv) == sizeof(struct _ddwaf_object_kv));
static_assert(offsetof(detail::object_kv, key) == offsetof(_ddwaf_object_kv, key));
static_assert(offsetof(detail::object_kv, val) == offsetof(_ddwaf_object_kv, val));

// detail::object_scalar<bool> == _ddwaf_object_bool
static_assert(sizeof(detail::object_scalar<bool>) == sizeof(_ddwaf_object_bool));
static_assert(offsetof(detail::object_scalar<bool>, type) == offsetof(_ddwaf_object_bool, type));
static_assert(offsetof(detail::object_scalar<bool>, val) == offsetof(_ddwaf_object_bool, val));

// detail::object_scalar<int64_t> == _ddwaf_object_signed
static_assert(sizeof(detail::object_scalar<int64_t>) == sizeof(_ddwaf_object_signed));
static_assert(
    offsetof(detail::object_scalar<int64_t>, type) == offsetof(_ddwaf_object_signed, type));
static_assert(offsetof(detail::object_scalar<int64_t>, val) == offsetof(_ddwaf_object_signed, val));

// detail::object_scalar<uint64_t> == _ddwaf_object_unsigned
static_assert(sizeof(detail::object_scalar<uint64_t>) == sizeof(_ddwaf_object_unsigned));
static_assert(
    offsetof(detail::object_scalar<uint64_t>, type) == offsetof(_ddwaf_object_unsigned, type));
static_assert(
    offsetof(detail::object_scalar<uint64_t>, val) == offsetof(_ddwaf_object_unsigned, val));

// detail::object_scalar<double> == _ddwaf_object_float
static_assert(sizeof(detail::object_scalar<double>) == sizeof(_ddwaf_object_float));
static_assert(offsetof(detail::object_scalar<double>, type) == offsetof(_ddwaf_object_float, type));
static_assert(offsetof(detail::object_scalar<double>, val) == offsetof(_ddwaf_object_float, val));

// detail::object_string == _ddwaf_object_string
static_assert(sizeof(detail::object_string) == sizeof(_ddwaf_object_string));
static_assert(offsetof(detail::object_string, type) == offsetof(_ddwaf_object_string, type));
static_assert(offsetof(detail::object_string, size) == offsetof(_ddwaf_object_string, size));
static_assert(offsetof(detail::object_string, ptr) == offsetof(_ddwaf_object_string, ptr));

// detail::object_small_string == _ddwaf_object_small_string
static_assert(sizeof(detail::object_small_string) == sizeof(_ddwaf_object_small_string));
static_assert(
    offsetof(detail::object_small_string, type) == offsetof(_ddwaf_object_small_string, type));
static_assert(
    offsetof(detail::object_small_string, size) == offsetof(_ddwaf_object_small_string, size));
static_assert(
    offsetof(detail::object_small_string, data) == offsetof(_ddwaf_object_small_string, data));

// detail::object_array == _ddwaf_object_array
static_assert(sizeof(detail::object_array) == sizeof(_ddwaf_object_array));
static_assert(offsetof(detail::object_array, type) == offsetof(_ddwaf_object_array, type));
static_assert(offsetof(detail::object_array, size) == offsetof(_ddwaf_object_array, size));
static_assert(offsetof(detail::object_array, capacity) == offsetof(_ddwaf_object_array, capacity));
static_assert(offsetof(detail::object_array, ptr) == offsetof(_ddwaf_object_array, ptr));

// detail::object_map == _ddwaf_object_map
static_assert(sizeof(detail::object_map) == sizeof(_ddwaf_object_map));
static_assert(offsetof(detail::object_map, type) == offsetof(_ddwaf_object_map, type));
static_assert(offsetof(detail::object_map, size) == offsetof(_ddwaf_object_map, size));
static_assert(offsetof(detail::object_map, capacity) == offsetof(_ddwaf_object_map, capacity));
static_assert(offsetof(detail::object_map, ptr) == offsetof(_ddwaf_object_map, ptr));

// Allocator callback compatibility
static_assert(std::is_same_v<ddwaf_alloc_fn_type, memory::user_resource::alloc_fn_type>);
static_assert(std::is_same_v<ddwaf_free_fn_type, memory::user_resource::free_fn_type>);
static_assert(std::is_same_v<ddwaf_udata_free_fn_type, memory::user_resource::udata_free_fn_type>);

// Log compatibility
static_assert(DDWAF_LOG_TRACE == static_cast<uint8_t>(log_level::trace));
static_assert(DDWAF_LOG_DEBUG == static_cast<uint8_t>(log_level::debug));
static_assert(DDWAF_LOG_INFO == static_cast<uint8_t>(log_level::info));
static_assert(DDWAF_LOG_WARN == static_cast<uint8_t>(log_level::warn));
static_assert(DDWAF_LOG_ERROR == static_cast<uint8_t>(log_level::error));
static_assert(DDWAF_LOG_OFF == static_cast<uint8_t>(log_level::off));
static_assert(sizeof(DDWAF_LOG_LEVEL) == sizeof(log_level));
static_assert(alignof(DDWAF_LOG_LEVEL) == alignof(log_level));

namespace {

// NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast)
detail::object *to_ptr(ddwaf_object *ptr) { return reinterpret_cast<detail::object *>(ptr); }
const detail::object *to_ptr(const ddwaf_object *ptr)
{
    // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast)
    return reinterpret_cast<const detail::object *>(ptr);
}

detail::object &to_ref(ddwaf_object *ptr) { return *to_ptr(ptr); }
const detail::object &to_ref(const ddwaf_object *ptr) { return *to_ptr(ptr); }

// UNSAFE: caller is responsible for ensuring that the allocator macthes the
// object's memory; to the extent that the borrow is from a larger owned object,
// the allocator must match that of the owned object. The reason for this is the
// contents of the borrowed object may be replaced through the assignment
// operator, and this new value may be destroyed using the allocator of the
// owning object.
borrowed_object to_borrowed(ddwaf_object *ptr, nonnull_ptr<memory::memory_resource> alloc)
{
    // safety: caller is responsible for ensuring allocator matches object's memory
    // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast)
    return borrowed_object{reinterpret_cast<detail::object *>(ptr), alloc};
}

memory::memory_resource *to_alloc_ptr(ddwaf_allocator alloc)
{
    // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast)
    return reinterpret_cast<memory::memory_resource *>(alloc);
}

} // namespace

// explicit instantiation declaration to suppress warning
extern "C" {
ddwaf::waf *ddwaf_init(const ddwaf_object *ruleset, ddwaf_object *diagnostics)
{
    try {
        if (ruleset != nullptr) {
            waf_builder builder;

            ddwaf::raw_configuration input{to_ref(ruleset)};
            ddwaf::ruleset_info ri;
            const ddwaf::defer on_exit([&]() {
                if (diagnostics != nullptr) {
                    // avoid to_borrowed(diagnostics, ...) = ... as that would destroy
                    // the current value in diagnostics, which could be uninitialized
                    *to_ptr(diagnostics) = ri.to_object().move();
                }
            });
            builder.add_or_update("default", input, ri);
            return new ddwaf::waf{builder.build()};
        }
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }

    return nullptr;
}

void ddwaf_destroy(ddwaf::waf *handle)
{
    try {
        delete handle;
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }
}

const char *const *ddwaf_known_addresses(ddwaf::waf *handle, uint32_t *size)
{
    if (handle == nullptr) {
        *size = 0;
        return nullptr;
    }

    const auto &addresses = handle->get_root_addresses();
    if (addresses.empty() || addresses.size() > std::numeric_limits<uint32_t>::max()) {
        *size = 0;
        return nullptr;
    }

    *size = (uint32_t)addresses.size();
    return addresses.data();
}

const char *const *ddwaf_known_actions(ddwaf::waf *handle, uint32_t *size)
{
    if (handle == nullptr) {
        *size = 0;
        return nullptr;
    }

    const auto &action_types = handle->get_available_action_types();
    if (action_types.empty() || action_types.size() > std::numeric_limits<uint32_t>::max()) {
        *size = 0;
        return nullptr;
    }

    *size = (uint32_t)action_types.size();
    return action_types.data();
}

ddwaf_context ddwaf_context_init(ddwaf::waf *handle, ddwaf_allocator output_alloc)
{
    try {
        if (handle != nullptr && output_alloc != nullptr) {
            return new context(handle->create_context(to_alloc_ptr(output_alloc)));
        }
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }
    return nullptr;
}

DDWAF_RET_CODE ddwaf_context_eval(ddwaf_context context, ddwaf_object *data,
    // NOLINTNEXTLINE(bugprone-easily-swappable-parameters)
    ddwaf_allocator alloc, ddwaf_object *result, uint64_t timeout)
{
    if (context == nullptr || data == nullptr) {
        DDWAF_WARN("Illegal WAF call: context or data was null");
        return DDWAF_ERR_INVALID_ARGUMENT;
    }

    try {
        if (alloc != nullptr) {
            // safety: caller is responsible to ensure that the passed allocator
            // can deallocate memory allocated for `data`. An array carries
            // multiple input batches, anything else is a single (map) batch.
            owned_object input{to_ref(data), to_alloc_ptr(alloc)};
            if (!context->insert_batch(std::move(input))) {
                return DDWAF_ERR_INVALID_OBJECT;
            }
        } else {
            const object_view input{to_ref(data)};
            if (!input.is_map() || !context->insert_batch(input.as<map_view>())) {
                return DDWAF_ERR_INVALID_OBJECT;
            }
        }

        // The timers will actually count nanoseconds, std::chrono doesn't
        // deal well with durations being beyond range.
        constexpr uint64_t max_timeout_us = std::chrono::nanoseconds::max().count() / 1000;
        timeout = std::min(timeout, max_timeout_us);

        timer deadline{std::chrono::microseconds(timeout)};
        auto [code, res] = context->eval(deadline);
        if (result != nullptr) {
            // avoid to_borrowed(result, res.alloc()) = std::move(res);
            // as that would destroy the current value in result, which could be
            // garbage (result could be uninitialized memory)
            *to_ptr(result) = res.move();
        }
        return code ? DDWAF_MATCH : DDWAF_OK;
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }

    return DDWAF_ERR_INTERNAL;
}

DDWAF_RET_CODE ddwaf_context_multieval(ddwaf_context context, ddwaf_object *data,
    // NOLINTNEXTLINE(bugprone-easily-swappable-parameters)
    ddwaf_allocator alloc, ddwaf_object *result, uint64_t timeout)
{
    if (context == nullptr || data == nullptr) {
        DDWAF_WARN("Illegal WAF call: context or data was null");
        return DDWAF_ERR_INVALID_ARGUMENT;
    }

    try {
        if (alloc != nullptr) {
            if (!context->insert_batches(owned_object{to_ref(data), to_alloc_ptr(alloc)})) {
                return DDWAF_ERR_INVALID_OBJECT;
            }
        } else {
            const object_view input{to_ref(data)};
            if (!input.is_array() || !context->insert_batches(input.as<array_view>())) {
                return DDWAF_ERR_INVALID_OBJECT;
            }
        }

        constexpr uint64_t max_timeout_us = std::chrono::nanoseconds::max().count() / 1000;
        timeout = std::min(timeout, max_timeout_us);

        timer deadline{std::chrono::microseconds(timeout)};
        auto [code, res] = context->eval(deadline);
        if (result != nullptr) {
            *to_ptr(result) = res.move();
        }
        return code ? DDWAF_MATCH : DDWAF_OK;
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }

    return DDWAF_ERR_INTERNAL;
}

void ddwaf_context_destroy(ddwaf_context context)
{
    try {
        delete context;
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }
}

ddwaf_subcontext ddwaf_subcontext_init(ddwaf_context context)
{
    try {
        if (context != nullptr) {
            return new subcontext(context->create_subcontext());
        }
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }
    return nullptr;
}

DDWAF_RET_CODE ddwaf_subcontext_eval(ddwaf_subcontext subcontext, ddwaf_object *data,
    // NOLINTNEXTLINE(bugprone-easily-swappable-parameters)
    ddwaf_allocator alloc, ddwaf_object *result, uint64_t timeout)
{
    if (subcontext == nullptr || data == nullptr) {
        DDWAF_WARN("Illegal WAF call: subcontext or data was null");
        return DDWAF_ERR_INVALID_ARGUMENT;
    }

    try {
        if (alloc != nullptr) {
            // safety: caller is responsible to ensure that the passed allocator
            // can deallocate memory allocated for `data`. An array carries
            // multiple input batches, anything else is a single (map) batch.
            owned_object input{to_ref(data), to_alloc_ptr(alloc)};
            if (!subcontext->insert_batch(std::move(input))) {
                return DDWAF_ERR_INVALID_OBJECT;
            }
        } else {
            const object_view input{to_ref(data)};
            if (!input.is_map() || !subcontext->insert_batch(input.as<map_view>())) {
                return DDWAF_ERR_INVALID_OBJECT;
            }
        }
        // The timers will actually count nanoseconds, std::chrono doesn't
        // deal well with durations being beyond range.
        constexpr uint64_t max_timeout_us = std::chrono::nanoseconds::max().count() / 1000;
        timeout = std::min(timeout, max_timeout_us);

        timer deadline{std::chrono::microseconds(timeout)};
        auto [code, res] = subcontext->eval(deadline);
        if (result != nullptr) {
            *to_ptr(result) = res.move();
        }
        return code ? DDWAF_MATCH : DDWAF_OK;
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }

    return DDWAF_ERR_INTERNAL;
}

DDWAF_RET_CODE ddwaf_subcontext_multieval(ddwaf_subcontext subcontext, ddwaf_object *data,
    // NOLINTNEXTLINE(bugprone-easily-swappable-parameters)
    ddwaf_allocator alloc, ddwaf_object *result, uint64_t timeout)
{
    if (subcontext == nullptr || data == nullptr) {
        DDWAF_WARN("Illegal WAF call: subcontext or data was null");
        return DDWAF_ERR_INVALID_ARGUMENT;
    }

    try {
        if (alloc != nullptr) {
            if (!subcontext->insert_batches(owned_object{to_ref(data), to_alloc_ptr(alloc)})) {
                return DDWAF_ERR_INVALID_OBJECT;
            }
        } else {
            const object_view input{to_ref(data)};
            if (!input.is_array() || !subcontext->insert_batches(input.as<array_view>())) {
                return DDWAF_ERR_INVALID_OBJECT;
            }
        }

        constexpr uint64_t max_timeout_us = std::chrono::nanoseconds::max().count() / 1000;
        timeout = std::min(timeout, max_timeout_us);

        timer deadline{std::chrono::microseconds(timeout)};
        auto [code, res] = subcontext->eval(deadline);
        if (result != nullptr) {
            *to_ptr(result) = res.move();
        }
        return code ? DDWAF_MATCH : DDWAF_OK;
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }

    return DDWAF_ERR_INTERNAL;
}

void ddwaf_subcontext_destroy(ddwaf_subcontext subcontext)
{
    try {
        delete subcontext;
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }
}

const char *ddwaf_get_version() { return ddwaf::current_version.cstring(); }

bool ddwaf_set_log_cb(ddwaf_log_cb cb, DDWAF_LOG_LEVEL min_level)
{
    auto level = static_cast<log_level>(min_level);
    // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast)
    ddwaf::logger::init(reinterpret_cast<logger::log_cb_type>(cb), level);
    DDWAF_INFO("Sending log messages to binding, min level {}", log_level_to_str(level));
    return true;
}

ddwaf_builder ddwaf_builder_init()
{
    try {
        return new waf_builder();
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }

    return nullptr;
}

bool ddwaf_builder_add_or_update_config(ddwaf::waf_builder *builder, const char *path,
    uint32_t path_len, const ddwaf_object *config, ddwaf_object *diagnostics)
{
    if (builder == nullptr || path == nullptr || path_len == 0 || config == nullptr) {
        return false;
    }

    try {
        auto input = static_cast<ddwaf::raw_configuration>(to_ref(config));

        ddwaf::ruleset_info ri;
        const ddwaf::defer on_exit([&]() {
            if (diagnostics != nullptr) {
                // avoid to_borrowed(diagnostics, ...) = ... as that would destroy
                // the current value in diagnostics, which could be uninitialized
                *to_ptr(diagnostics) = ri.to_object().move();
            }
        });
        return builder->add_or_update({path, path_len}, input, ri);
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }

    return false;
}

bool ddwaf_builder_remove_config(ddwaf::waf_builder *builder, const char *path, uint32_t path_len)
{
    if (builder == nullptr || path == nullptr || path_len == 0) {
        return false;
    }

    try {
        return builder->remove({path, path_len});
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }

    return false;
}

ddwaf_handle ddwaf_builder_build_instance(ddwaf::waf_builder *builder)
{
    if (builder == nullptr) {
        return nullptr;
    }

    try {
        return new ddwaf::waf{builder->build()};
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }

    return nullptr;
}

uint32_t ddwaf_builder_get_config_paths(
    ddwaf_builder builder, ddwaf_object *paths, const char *filter, uint32_t filter_len)
{
    if (builder == nullptr) {
        return 0;
    }

    try {
        std::vector<std::string_view> config_paths;
        if (filter != nullptr) {
            re2::RE2::Options options;
            options.set_log_errors(false);
            options.set_case_sensitive(true);

            re2::RE2 regex_filter{{filter, static_cast<std::size_t>(filter_len)}, options};
            config_paths = builder->get_filtered_config_paths(regex_filter);
        } else {
            config_paths = builder->get_config_paths();
        }

        if (paths != nullptr) {
            auto *default_allocator = memory::get_default_resource();
            auto object = owned_object::make_array(config_paths.size(), default_allocator);
            for (const auto &value : config_paths) { object.emplace_back(value); }
            // avoid to_borrowed(paths, ...) = ... as that would destroy
            // the current value in paths, which could be uninitialized
            *to_ptr(paths) = object.move();
        }
        return config_paths.size();
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }

    return 0;
}

void ddwaf_builder_destroy(ddwaf_builder builder)
{
    try {
        delete builder;
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }
}

ddwaf_allocator ddwaf_get_default_allocator() { return memory::get_default_resource(); }

ddwaf_allocator ddwaf_synchronized_pool_allocator_init()
{
    try {
        return new memory::synchronized_pool_resource();
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }
    return nullptr;
}

ddwaf_allocator ddwaf_unsynchronized_pool_allocator_init()
{
    try {
        return new memory::unsynchronized_pool_resource();
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }
    return nullptr;
}

ddwaf_allocator ddwaf_monotonic_allocator_init()
{
    try {
        return new memory::monotonic_buffer_resource();
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }
    return nullptr;
}

ddwaf_allocator ddwaf_user_allocator_init(ddwaf_alloc_fn_type alloc_fn, ddwaf_free_fn_type free_fn,
    void *udata, ddwaf_udata_free_fn_type udata_free_fn)
{
    try {
        return new memory::user_resource(alloc_fn, free_fn, udata, udata_free_fn);
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }
    return nullptr;
}

void *ddwaf_allocator_alloc(ddwaf_allocator alloc, size_t bytes, size_t alignment)
{
    if (alloc == nullptr) {
        return nullptr;
    }

    try {
        return to_alloc_ptr(alloc)->allocate(bytes, alignment);
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }
    return nullptr;
}

void ddwaf_allocator_free(ddwaf_allocator alloc, void *p, size_t bytes, size_t alignment)
{
    if (alloc != nullptr) {
        to_alloc_ptr(alloc)->deallocate(p, bytes, alignment);
    }
}

void ddwaf_allocator_destroy(ddwaf_allocator alloc)
{
    if (alloc == memory::get_default_resource()) {
        return;
    }

    try {
        delete to_alloc_ptr(alloc);
    } catch (const std::exception &e) {
        DDWAF_ERROR("{}", e.what());
    } catch (...) {
        DDWAF_ERROR("unknown exception");
    }
}

ddwaf_object *ddwaf_object_set_invalid(ddwaf_object *object)
{
    if (object == nullptr) {
        return nullptr;
    }

    to_ref(object) = detail::object{
        .type = object_type::invalid,
    };

    return object;
}

ddwaf_object *ddwaf_object_set_null(ddwaf_object *object)
{
    if (object == nullptr) {
        return nullptr;
    }

    to_ref(object) = detail::object{
        .type = object_type::null,
    };

    return object;
}

ddwaf_object *ddwaf_object_null(ddwaf_object *object) { return ddwaf_object_set_null(object); }

ddwaf_object *ddwaf_object_set_string(
    ddwaf_object *object, const char *string, uint32_t length, ddwaf_allocator alloc)
{
    if (object == nullptr || (string == nullptr && length != 0) || alloc == nullptr) {
        return nullptr;
    }
    owned_object new_str = owned_object::make_string(string, length, to_alloc_ptr(alloc));
    to_ref(object) = new_str.move();
    return object;
}

ddwaf_object *ddwaf_object_set_string_nocopy(
    ddwaf_object *object, const char *string, uint32_t length)
{
    if (object == nullptr || string == nullptr) {
        return nullptr;
    }
    // safety: the allocator is irrelevant: unsafe_make_string_copy doesn't
    // allocate from it and we forget it afterwards (we just care about the
    // detail::object part of the owned_object)
    to_ref(object) =
        owned_object::make_string_nocopy(string, length, memory::get_default_null_resource())
            .move();
    return object;
}

ddwaf_object *ddwaf_object_set_string_literal(
    ddwaf_object *object, const char *string, uint32_t length)
{
    if (object == nullptr || string == nullptr) {
        return nullptr;
    }
    to_ref(object) = owned_object::make_string_literal(string, length).ref();
    return object;
}

ddwaf_object *ddwaf_object_set_unsigned(ddwaf_object *object, uint64_t value)
{
    if (object == nullptr) {
        return nullptr;
    }

    to_ref(object) = detail::object{
        .via = {.u64 = {.type = object_type::uint64, .val = value}},
    };
    return object;
}

ddwaf_object *ddwaf_object_unsigned(ddwaf_object *object, uint64_t value)
{
    return ddwaf_object_set_unsigned(object, value);
}

ddwaf_object *ddwaf_object_set_signed(ddwaf_object *object, int64_t value)
{
    if (object == nullptr) {
        return nullptr;
    }

    to_ref(object) = detail::object{
        .via = {.i64 = {.type = object_type::int64, .val = value}},
    };
    return object;
}

ddwaf_object *ddwaf_object_signed(ddwaf_object *object, int64_t value)
{
    return ddwaf_object_set_signed(object, value);
}

ddwaf_object *ddwaf_object_set_bool(ddwaf_object *object, bool value)
{
    if (object == nullptr) {
        return nullptr;
    }
    to_ref(object) = detail::object{
        .via = {.b8 = {.type = object_type::boolean, .val = value}},
    };
    return object;
}

ddwaf_object *ddwaf_object_bool(ddwaf_object *object, bool value)
{
    return ddwaf_object_set_bool(object, value);
}

ddwaf_object *ddwaf_object_set_float(ddwaf_object *object, double value)
{
    if (object == nullptr) {
        return nullptr;
    }
    to_ref(object) = detail::object{
        .via = {.f64 = {.type = object_type::float64, .val = value}},
    };
    return object;
}

ddwaf_object *ddwaf_object_float(ddwaf_object *object, double value)
{
    return ddwaf_object_set_float(object, value);
}

ddwaf_object *ddwaf_object_set_array(ddwaf_object *object, uint16_t capacity, ddwaf_allocator alloc)
{
    if (object == nullptr || alloc == nullptr) {
        return nullptr;
    }
    // object may be uninitialized
    auto *alloc_ptr = to_alloc_ptr(alloc);
    auto new_array = owned_object::make_array(capacity, alloc_ptr);
    to_ref(object) = new_array.move();
    return object;
}

ddwaf_object *ddwaf_object_set_map(ddwaf_object *object, uint16_t capacity, ddwaf_allocator alloc)
{
    if (object == nullptr || alloc == nullptr) {
        return nullptr;
    }

    // object may be uninitialized
    auto *alloc_ptr = to_alloc_ptr(alloc);
    auto new_map = owned_object::make_map(capacity, alloc_ptr);
    to_ref(object) = new_map.move();
    return object;
}

bool ddwaf_object_from_json(
    ddwaf_object *output, const char *json_str, uint32_t length, ddwaf_allocator alloc)
{
    if (output == nullptr || json_str == nullptr || length == 0) {
        return false;
    }

    auto *alloc_ptr = to_alloc_ptr(alloc);
    try {
        // avoid to_borrowed(output, ...) = ... as that would destroy
        // the current value in output, which could be uninitialized
        *to_ptr(output) = json_to_object({json_str, length}, alloc_ptr).move();
        return to_borrowed(output, alloc_ptr).is_valid();
    } catch (...) {} // NOLINT(bugprone-empty-catch)

    return false;
}

ddwaf_object *ddwaf_object_insert(ddwaf_object *array, ddwaf_allocator alloc)
{
    if (array == nullptr || array->type != DDWAF_OBJ_ARRAY || alloc == nullptr) {
        return nullptr;
    }

    try {
        auto *alloc_ptr = to_alloc_ptr(alloc);
        borrowed_object container = to_borrowed(array, alloc_ptr);
        owned_object new_element = owned_object{};
        borrowed_object inserted = container.emplace_back(std::move(new_element));

        // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast)
        return reinterpret_cast<ddwaf_object *>(inserted.ptr());
    } catch (...) {} // NOLINT(bugprone-empty-catch)
    return nullptr;
}
ddwaf_object *ddwaf_object_insert_key(
    ddwaf_object *map, const char *key, uint32_t length, ddwaf_allocator alloc)
{
    if (map == nullptr || map->type != DDWAF_OBJ_MAP || alloc == nullptr) {
        return nullptr;
    }

    try {
        auto *alloc_ptr = to_alloc_ptr(alloc);
        borrowed_object container = to_borrowed(map, alloc_ptr);
        owned_object new_value = owned_object{};
        borrowed_object inserted =
            container.emplace(std::string_view{key, length}, std::move(new_value));

        // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast)
        return reinterpret_cast<ddwaf_object *>(inserted.ptr());
    } catch (...) {} // NOLINT(bugprone-empty-catch)
    return nullptr;
}

ddwaf_object *ddwaf_object_insert_key_nocopy(
    ddwaf_object *map, const char *key, uint32_t length, ddwaf_allocator alloc)
{
    if (map == nullptr || map->type != DDWAF_OBJ_MAP || alloc == nullptr) {
        return nullptr;
    }

    try {
        auto *alloc_ptr = to_alloc_ptr(alloc);
        auto key_obj = owned_object::make_string_nocopy(key, length, alloc_ptr);
        auto value_obj = owned_object{};

        // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast)
        return reinterpret_cast<ddwaf_object *>(
            to_borrowed(map, alloc_ptr)
                // safety: it's part of the contract of this function that the
                // key can be deallocated with alloc
                .emplace(std::move(key_obj), std::move(value_obj))
                .ptr());
    } catch (...) {} // NOLINT(bugprone-empty-catch)
    return nullptr;
}

ddwaf_object *ddwaf_object_insert_literal_key(
    ddwaf_object *map, const char *key, uint32_t length, ddwaf_allocator alloc)
{
    if (map == nullptr || map->type != DDWAF_OBJ_MAP || alloc == nullptr) {
        return nullptr;
    }

    try {
        auto *alloc_ptr = to_alloc_ptr(alloc);
        auto key_obj = owned_object::make_string_literal(key, length);
        auto value_obj = owned_object{};

        // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast)
        return reinterpret_cast<ddwaf_object *>(
            to_borrowed(map, alloc_ptr).emplace(std::move(key_obj), std::move(value_obj)).ptr());
    } catch (...) {} // NOLINT(bugprone-empty-catch)
    return nullptr;
}

void ddwaf_object_destroy(ddwaf_object *object, ddwaf_allocator alloc)
{
    if (object == nullptr || alloc == nullptr) {
        return;
    }

    detail::object_destroy(to_ref(object), to_alloc_ptr(alloc));

    ddwaf_object_set_invalid(object);
}

DDWAF_OBJ_TYPE ddwaf_object_get_type(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    if (!view.has_value()) {
        return DDWAF_OBJ_INVALID;
    }

    return static_cast<DDWAF_OBJ_TYPE>(view.type());
}

size_t ddwaf_object_get_size(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    if (!view.has_value() || !view.is_container()) {
        return 0;
    }

    return view.size();
}

size_t ddwaf_object_get_length(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    if (!view.has_value() || !view.is_string()) {
        return 0;
    }

    return view.size();
}

const char *ddwaf_object_get_string(const ddwaf_object *object, size_t *length)
{
    const object_view view{to_ptr(object)};
    if (!view.has_value() || !view.is_string()) {
        return nullptr;
    }

    if (length != nullptr) {
        *length = view.size();
    }

    return view.data();
}

uint64_t ddwaf_object_get_unsigned(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    if (!view.has_value() || !view.is<uint64_t>()) {
        return 0;
    }
    return view.as<uint64_t>();
}

int64_t ddwaf_object_get_signed(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    if (!view.has_value() || !view.is<int64_t>()) {
        return 0;
    }
    return view.as<int64_t>();
}

double ddwaf_object_get_float(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    if (!view.has_value() || !view.is<double>()) {
        return 0;
    }
    return view.as<double>();
}

bool ddwaf_object_get_bool(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    if (!view.has_value() || !view.is<bool>()) {
        return false;
    }
    return view.as<bool>();
}

const ddwaf_object *ddwaf_object_at_key(const ddwaf_object *object, size_t index)
{
    const object_view view{to_ptr(object)};
    if (!view.has_value() || !view.is_map() || index >= view.size()) {
        return nullptr;
    }

    // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast)
    return reinterpret_cast<const ddwaf_object *>(view.at_key(index).ptr());
}

const ddwaf_object *ddwaf_object_at_value(const ddwaf_object *object, size_t index)
{
    const object_view view{to_ptr(object)};
    if (!view.has_value() || !view.is_container() || index >= view.size()) {
        return nullptr;
    }

    // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast)
    return reinterpret_cast<const ddwaf_object *>(view.at_value(index).ptr());
}

const ddwaf_object *ddwaf_object_find(const ddwaf_object *object, const char *key, size_t length)
{
    const object_view view{to_ptr(object)};
    if (!view.has_value() || !view.is_map() || view.empty() || key == nullptr || length == 0) {
        return nullptr;
    }

    // NOLINTNEXTLINE(cppcoreguidelines-pro-type-reinterpret-cast)
    return reinterpret_cast<const ddwaf_object *>(view.find(std::string_view{key, length}).ptr());
}

ddwaf_object *ddwaf_object_clone(
    const ddwaf_object *source, ddwaf_object *destination, ddwaf_allocator alloc)
{
    const object_view view{to_ptr(source)};
    if (!view.has_value()) {
        return nullptr;
    }

    auto *alloc_ptr = to_alloc_ptr(alloc);
    // destination may be uninitialized; don't use borrowed_object
    auto output = view.clone(alloc_ptr);
    to_ref(destination) = output.move();
    return destination;
}

bool ddwaf_object_is_invalid(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    return view.has_value() && view.is_invalid();
}

bool ddwaf_object_is_null(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    return view.has_value() && view.type() == object_type::null;
}

bool ddwaf_object_is_bool(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    return view.has_value() && view.type() == object_type::boolean;
}

bool ddwaf_object_is_signed(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    return view.has_value() && view.type() == object_type::int64;
}

bool ddwaf_object_is_unsigned(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    return view.has_value() && view.type() == object_type::uint64;
}

bool ddwaf_object_is_float(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    return view.has_value() && view.type() == object_type::float64;
}

bool ddwaf_object_is_string(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    return view.has_value() && view.is_string();
}

bool ddwaf_object_is_array(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    return view.has_value() && view.is_array();
}

bool ddwaf_object_is_map(const ddwaf_object *object)
{
    const object_view view{to_ptr(object)};
    return view.has_value() && view.is_map();
}
}
