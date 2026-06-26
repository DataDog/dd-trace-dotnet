// Unless explicitly stated otherwise all files in this repository are
// dual-licensed under the Apache-2.0 License or BSD-3-Clause License.
//
// This product includes software developed at Datadog (https://www.datadoghq.com/).
// Copyright 2021 Datadog, Inc.

#pragma once

#include <array>
#include <memory>
#include <string>
#include <string_view>
#include <unordered_map>
#include <unordered_set>
#include <utility>
#include <vector>

#include "action_mapper.hpp"
#include "builder/module_builder.hpp"
#include "exclusion/input_filter.hpp"
#include "exclusion/rule_filter.hpp"
#include "matcher/base.hpp"
#include "module.hpp"
#include "module_category.hpp"
#include "obfuscator.hpp"
#include "processor/base.hpp"
#include "rule.hpp"
#include "scanner.hpp"
#include "target_address.hpp"

namespace ddwaf {

struct ruleset {
    // NOLINTNEXTLINE(bugprone-easily-swappable-parameters)
    void insert_rules(std::shared_ptr<const std::vector<core_rule>> base,
        std::shared_ptr<const std::vector<core_rule>> user)
    {
        base_rules = std::move(base);
        user_rules = std::move(user);

        for (const auto &rule : *base_rules) { rule.get_addresses(rule_addresses); }
        for (const auto &rule : *user_rules) { rule.get_addresses(rule_addresses); }

        rule_module_set_builder builder;
        rule_modules = builder.build(*base_rules, *user_rules);
    }

    void insert_filters(std::shared_ptr<const std::vector<rule_filter>> filters)
    {
        rule_filters = std::move(filters);
        for (const auto &filter : *rule_filters) { filter.get_addresses(filter_addresses); }
    }

    void insert_filters(std::shared_ptr<const std::vector<input_filter>> filters)
    {
        input_filters = std::move(filters);
        for (const auto &filter : *input_filters) { filter.get_addresses(filter_addresses); }
    }

    void insert_preprocessors(
        std::shared_ptr<const std::vector<std::unique_ptr<base_processor>>> processors)
    {
        preprocessors = std::move(processors);
        for (const auto &proc : *preprocessors) { proc->get_addresses(preprocessor_addresses); }
    }

    void insert_postprocessors(
        std::shared_ptr<const std::vector<std::unique_ptr<base_processor>>> processors)
    {
        postprocessors = std::move(processors);
        for (const auto &proc : *postprocessors) { proc->get_addresses(postprocessor_addresses); }
    }

    [[nodiscard]] const std::vector<const char *> &get_root_addresses()
    {
        if (root_addresses.empty()) {
            std::unordered_set<target_index> known_targets;
            for (const auto &[index, str] : rule_addresses) {
                const auto &[it, res] = known_targets.emplace(index);
                if (res) {
                    root_addresses.emplace_back(str.c_str());
                }
            }
            for (const auto &[index, str] : filter_addresses) {
                const auto &[it, res] = known_targets.emplace(index);
                if (res) {
                    root_addresses.emplace_back(str.c_str());
                }
            }
            for (const auto &[index, str] : preprocessor_addresses) {
                const auto &[it, res] = known_targets.emplace(index);
                if (res) {
                    root_addresses.emplace_back(str.c_str());
                }
            }
            for (const auto &[index, str] : postprocessor_addresses) {
                const auto &[it, res] = known_targets.emplace(index);
                if (res) {
                    root_addresses.emplace_back(str.c_str());
                }
            }
        }
        return root_addresses;
    }

    [[nodiscard]] const std::vector<const char *> &get_available_action_types()
    {
        if (available_action_types.empty()) {
            std::unordered_set<std::string_view> all_types;
            // We preallocate at least the total available actions in the mapper
            all_types.reserve(actions->size());

            auto maybe_add_action = [&](auto &&action) {
                auto it = actions->find(action);
                if (it == actions->end()) {
                    return;
                }
                auto [new_it, res] = all_types.emplace(it->second.type_str);
                if (res) {
                    available_action_types.emplace_back(it->second.type_str.c_str());
                }
            };

            for (const auto &rule : *base_rules) {
                for (const auto &action : rule.get_actions()) { maybe_add_action(action); }
            }

            for (const auto &rule : *user_rules) {
                for (const auto &action : rule.get_actions()) { maybe_add_action(action); }
            }

            for (const auto &filter : *rule_filters) { maybe_add_action(filter.get_action()); }
        }
        return available_action_types;
    }

    std::shared_ptr<const match_obfuscator> obfuscator;

    std::shared_ptr<const std::vector<std::unique_ptr<base_processor>>> preprocessors;
    std::shared_ptr<const std::vector<std::unique_ptr<base_processor>>> postprocessors;

    std::shared_ptr<const std::vector<rule_filter>> rule_filters;
    std::shared_ptr<const std::vector<input_filter>> input_filters;

    std::shared_ptr<const std::vector<core_rule>> base_rules;
    std::shared_ptr<const std::vector<core_rule>> user_rules;

    std::shared_ptr<const matcher_mapper> rule_matchers;
    std::shared_ptr<const matcher_mapper> exclusion_matchers;

    std::shared_ptr<const std::vector<scanner>> scanners;
    std::shared_ptr<const action_mapper> actions;

    // Rule modules
    std::array<rule_module, rule_module_count> rule_modules;

    std::unordered_map<target_index, std::string> rule_addresses;
    std::unordered_map<target_index, std::string> filter_addresses;
    std::unordered_map<target_index, std::string> preprocessor_addresses;
    std::unordered_map<target_index, std::string> postprocessor_addresses;

    // The following two members are computed only when required; they are
    // provided to the caller of ddwaf_known_* and are only cached for the
    // purpose of avoiding the need for a destruction method in the API.
    //
    // Root addresses, lazily computed
    std::vector<const char *> root_addresses;
    // A list of the possible action types that can be returned as a result of
    // the evaluation of the current set of rules and exclusion filters.
    // These are lazily computed andthe underlying memory of each string is
    // owned by the action mapper.
    std::vector<const char *> available_action_types;
};

} // namespace ddwaf