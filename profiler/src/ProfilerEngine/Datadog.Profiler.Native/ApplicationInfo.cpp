#include "ApplicationInfo.h"

#include <utility>

ApplicationInfo::ApplicationInfo(std::string serviceName, std::string environment, std::string version) :
    ServiceName(std::move(serviceName)), Environment(std::move(environment)), Version(std::move(version))
{    
}