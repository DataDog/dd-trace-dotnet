#pragma once

#include <vector>
#include "IntegrationBase.h"
#include "AspNetMvc5Integration.h"
#include "CustomIntegration.h"

const struct
{
    const AspNetMvc5Integration AspNetMvc5Integration;
    const CustomIntegration CustomIntegration;

    const std::vector<const IntegrationBase*> All = {
        &AspNetMvc5Integration,
        &CustomIntegration,
    };
} GlobalIntegrations;
