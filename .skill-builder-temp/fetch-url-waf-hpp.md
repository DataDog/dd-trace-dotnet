// Unless explicitly stated otherwise all files in this repository are
// dual-licensed under the Apache-2.0 License or BSD-3-Clause License.
//
// This product includes software developed at Datadog (https://www.datadoghq.com/).
// Copyright 2021 Datadog, Inc.
#pragma once

#include <memory>
#include <utility>
#include <vector>

#include "context.hpp"
#include "memory_resource.hpp"
#include "pointer.hpp"
#include "ruleset.hpp"

namespace ddwaf {

class waf {
public:
    explicit waf(std::shared_ptr<ruleset> ruleset) : ruleset_(std::move(ruleset)) {}
    waf(const waf &) = default;
    waf(waf &&) = default;
    waf &operator=(const waf &) = default;
    waf &operator=(waf &&) = default;
    ~waf() = default;

    context create_context(nonnull_ptr<memory::memory_resource> alloc)
    {
        return context(ruleset_, alloc);
    }

    [[nodiscard]] const std::vector<const char *> &get_root_addresses() const
    {
        return ruleset_->get_root_addresses();
    }

    [[nodiscard]] const std::vector<const char *> &get_available_action_types() const
    {
        return ruleset_->get_available_action_types();
    }

protected:
    std::shared_ptr<ruleset> ruleset_;
};

} // namespace ddwaf
