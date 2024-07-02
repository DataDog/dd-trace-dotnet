#pragma once
#include "cor.h"
#include "corprof.h"
#include <corhlpr.h>
#include <iomanip>
#include <sstream>
#include <unordered_set>
#include <vector>
#include "integration.h"

#include "../../../shared/src/native-src/string.h"

namespace trace
{

struct IntegrationDefinition
{
private:
    UINT32 categories = 1;
    UINT32 enabled_categories = 0;

public:
    const MethodReference target_method;
    const TypeReference integration_type;
    const bool is_derived = false;
    const bool is_interface = false;
    const bool is_exact_signature_match = true;

    IntegrationDefinition()
    {
    }

    IntegrationDefinition(const IntegrationDefinition& other) :
        target_method(other.target_method),
        integration_type(other.integration_type),
        is_derived(other.is_derived),
        is_interface(other.is_interface),
        is_exact_signature_match(other.is_exact_signature_match),
        categories(other.categories),
        enabled_categories(other.enabled_categories)
    {
    }

    IntegrationDefinition(const MethodReference& target_method, const TypeReference& integration_type, bool isDerived,
                          bool is_interface, bool is_exact_signature_match, UINT32 categories = 1,
                          UINT32 enabledCategories = -1) :
        target_method(target_method),
        integration_type(integration_type),
        is_derived(isDerived),
        is_interface(is_interface),
        is_exact_signature_match(is_exact_signature_match),
        categories(categories),
        enabled_categories(categories & enabledCategories)
    {
    }

    inline bool operator==(const IntegrationDefinition& other) const
    {
        return target_method == other.target_method && integration_type == other.integration_type &&
               is_derived == other.is_derived && is_interface == other.is_interface &&
               is_exact_signature_match == other.is_exact_signature_match && categories == other.categories;
    }

    inline bool GetEnabled() const
    {
        return enabled_categories != 0;
    }
    inline bool SetEnabled(bool enabled, UINT32 categories_ = -1)
    {
        auto enabledCategories = categories & categories_;
        if (enabled)
        {
            enabled_categories |= enabledCategories;
        }
        else
        {
            enabled_categories &= ~enabledCategories;
        }
        return GetEnabled();
    }

    void Update(const IntegrationDefinition& other)
    {
        enabled_categories = other.enabled_categories;
    }
};

std::vector<IntegrationDefinition> GetIntegrationsFromTraceMethodsConfiguration(const TypeReference& integration_type, const shared::WSTRING& configuration_string);

} // namespace trace