#pragma once
#include "IApplicationStore.h"

// forward declarations
class IConfiguration;

class ApplicationStore : public IApplicationStore
{
public:
    ApplicationStore(IConfiguration* configuration);

    const std::string& GetName(std::string_view runtimeId) override;

private:
    IConfiguration* const _pConfiguration;
};
