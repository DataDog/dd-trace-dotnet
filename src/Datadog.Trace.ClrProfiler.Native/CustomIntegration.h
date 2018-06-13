#pragma once

#include "IntegrationBase.h"

class CustomIntegration : public IntegrationBase
{
public:
    CustomIntegration();

    bool IsEnabled() const override;

    IntegrationType GetIntegrationType() const override
    {
        return IntegrationType_Custom;
    }
};
