#pragma once

#include "../../../shared/src/native-src/dd_filesystem.hpp"
#include "../../../shared/src/native-src/dynamic_library_base.h"

#include <corhlpr.h>
#include <corprof.h>

#include <functional>

namespace datadog::shared
{
// forward declarations
class Logger;

class DynamicCOMLibrary : public DynamicLibraryBase
{
public:
    DynamicCOMLibrary(const std::string& filePath, Logger* logger);

    HRESULT DllGetClassObject(REFCLSID, REFIID, LPVOID*);
    HRESULT DllCanUnloadNow();

private:
    void OnInitialized() override;

    std::function<HRESULT(REFCLSID, REFIID, LPVOID*)> _dllGetClassObjectFn;
    std::function<HRESULT()> _dllCanUnloadNowFn;
    Logger* const _logger;
};
} // namespace datadog::shared