#pragma once
#include <string>
#include <memory>

struct ApplicationInfo
{
public:
    std::string ServiceName;
    std::string Environment;
    std::string Version;
    std::string RepositoryUrl;
    std::string CommitSha;
};
