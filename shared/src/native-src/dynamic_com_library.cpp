#include "dynamic_com_library.h"

#include "../../../shared/src/native-src/logger.h"

namespace datadog::shared
{

DynamicCOMLibrary::DynamicCOMLibrary(const std::string& filePath, Logger* logger) :
    DynamicLibraryBase(filePath, logger), _dllGetClassObjectFn{nullptr}, _dllCanUnloadNowFn{nullptr}, _logger{logger}
{
}

HRESULT datadog::shared::DynamicCOMLibrary::DllGetClassObject(REFCLSID clsid, REFIID iid, LPVOID* ptr)
{
    if (_dllGetClassObjectFn != nullptr)
    {
        return _dllGetClassObjectFn(clsid, iid, ptr);
    }

    _logger->Warn("DynamicCOMLibrary::DllGetClassObject: cannot call to DllGetClassObject. An issue might have occured "
                  "and we were enable to get a pointer to this function");
    return E_FAIL;
}

HRESULT datadog::shared::DynamicCOMLibrary::DllCanUnloadNow()
{
    if (_dllCanUnloadNowFn != nullptr)
    {
        return _dllCanUnloadNowFn();
    }

    _logger->Warn("DynamicCOMLibrary::DllCanUnloadNow: cannot call to DllCanUnloadNow. An issue might have occured "
                  "and we were enable to get a pointer to this function");
    return E_FAIL;
}

void datadog::shared::DynamicCOMLibrary::OnInitialized()
{
    _dllGetClassObjectFn =
        reinterpret_cast<HRESULT(STDMETHODCALLTYPE*)(REFCLSID, REFIID, LPVOID*)>(GetFunction("DllGetClassObject"));
    if (_dllGetClassObjectFn == nullptr)
    {
        _logger->Warn(
            "DynamicCOMLibrary::OnInitialized: Unable to retrieve external function 'DllGetClassObject' from library:",
            GetFilePath());
    }

    _dllCanUnloadNowFn = reinterpret_cast<HRESULT(STDMETHODCALLTYPE*)()>(GetFunction("DllCanUnloadNow"));
    if (_dllCanUnloadNowFn == nullptr)
    {
        _logger->Warn(
            "DynamicCOMLibrary::OnInitialized: Unable to retrieve external function 'DllCanUnloadNow' from library:",
            GetFilePath());
    }
}

} // namespace datadog::shared
