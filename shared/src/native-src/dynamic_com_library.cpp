#pragma once

#include "dynamic_com_library.h"

namespace datadog::shared
{
    DynamicCOMLibrary::DynamicCOMLibrary(const std::string& filePath) :
        DynamicLibraryBase(filePath)
    {
    }

    HRESULT datadog::shared::DynamicCOMLibrary::DllGetClassObject(REFCLSID clsid, REFIID iid, LPVOID* ptr)
    {
        if (_dllGetClassObjectFn != nullptr)
        {
            return _dllGetClassObjectFn(clsid, iid, ptr);
        }

        // The function cannot be loaded.
        return E_FAIL;
    }

    HRESULT datadog::shared::DynamicCOMLibrary::DllCanUnloadNow()
    {
        if (_dllCanUnloadNowFn != nullptr)
        {
            return _dllCanUnloadNowFn();
        }

        // The function cannot be loaded.
        return E_FAIL;
    }

    void datadog::shared::DynamicCOMLibrary::AfterLoad()
    {
        _dllGetClassObjectFn = reinterpret_cast<HRESULT(*)(REFCLSID, REFIID, LPVOID*)>(GetFunction("DllGetClassObject"));
        if (_dllGetClassObjectFn == nullptr)
        {
            // Log something
        }

        _dllCanUnloadNowFn = reinterpret_cast<HRESULT(*)()>(GetFunction("DllCanUnloadNow"));
        if (_dllCanUnloadNowFn == nullptr)
        {
            // Log something
        }
    }

    void datadog::shared::DynamicCOMLibrary::BeforeUnload()
    {
    }
}
