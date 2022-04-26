#pragma once
#include <string>

class ApplicationInfo
{
public:
    ApplicationInfo(std::string serviceName, std::string environment, std::string version);

    std::string ServiceName;
    std::string Environment;
    std::string Version;
};
