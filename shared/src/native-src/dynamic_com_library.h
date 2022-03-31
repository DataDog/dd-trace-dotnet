#pragma once

#include "../../../shared/src/native-src/dd_filesystem.hpp"
#include "../../../shared/src/native-src/dynamic_library_base.h"

#include <corhlpr.h>
#include <corprof.h>

#include <functional>

namespace datadog::shared
{
class DynamicCOMLibrary : public DynamicLibraryBase
{
public:
    DynamicCOMLibrary(const std::string& filePath);

    HRESULT DllGetClassObject(REFCLSID, REFIID, LPVOID*);
    HRESULT DllCanUnloadNow();

private:
    void AfterLoad() override;
    void BeforeUnload() override;

    std::function<HRESULT(REFCLSID, REFIID, LPVOID*)> _dllGetClassObjectFn;
    std::function<HRESULT()> _dllCanUnloadNowFn;
};
} // namespace datadog::shared