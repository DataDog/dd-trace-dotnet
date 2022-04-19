#pragma once

#include <string>

namespace datadog::shared
{
// forwar declarations
class Logger;

class DynamicLibraryBase
{
public:
    bool Load();
    bool Unload();

    const std::string& GetFilePath();

protected:
    DynamicLibraryBase(const std::string& filePath, Logger* logger);
    virtual ~DynamicLibraryBase() = default;

    void* GetFunction(const std::string& funcName);

private:
    virtual void OnInitialized() = 0;

    std::string _filePath;
    void* _instance;
    Logger* const _logger;
};

} // namespace datadog::shared
