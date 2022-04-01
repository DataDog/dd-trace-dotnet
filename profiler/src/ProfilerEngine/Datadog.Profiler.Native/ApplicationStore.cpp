#include "ApplicationStore.h"

#include "IConfiguration.h"

ApplicationStore::ApplicationStore(IConfiguration* configuration) :
    _pConfiguration{configuration}
{
}

const std::string& ApplicationStore::GetName(std::string_view runtimeId)
{
    return _pConfiguration->GetServiceName();
}
