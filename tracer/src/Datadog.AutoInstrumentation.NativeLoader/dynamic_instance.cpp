#include "dynamic_instance.h"

#include "log.h"

#include <string>

#include "../../../shared/src/native-src/dd_guid.h"
#include "../../../shared/src/native-src/util.h"

namespace datadog::shared::nativeloader
{
    //
    // public
    //

    DynamicInstanceImpl::DynamicInstanceImpl(std::string filePath, std::string clsid) : m_mainLibrary{filePath, Log::Instance}
    {
        m_clsid = guid_parse::make_guid(clsid);
        m_classFactory = nullptr;
        m_corProfilerCallback = nullptr;
        m_loaded = m_mainLibrary.Load();
    }

    DynamicInstanceImpl::~DynamicInstanceImpl()
    {
        m_corProfilerCallback = nullptr;
        m_classFactory = nullptr;
        m_loaded = false;
        m_mainLibrary.Unload();
    }

    /*
     * DllGetClassObject Is called to get the proxy profiler class factory.
     * So in our proxy class factory we redirect the call to the target profilers
     * DllGetClassObject exported function, so we get the class factories from the
     * target profilers as well.
     *
     * Then the CreateInstance method from the proxy profiler class factory gets called.
     * In this case we create our proxy profiler callbacks instance and also redirect
     * the same call to the CreateInstance method on each target profilers class factories,
     * so in this steps we get also the CorProfilerCallback instances from the target
     * profilers as well.
     *
     * Then the CorProfilerCallback::Initialize method gets called, here we do the same
     * a previous steps, calls the same method in each target profiler and set the global
     * event mask.
     *
     */

    HRESULT DynamicInstanceImpl::LoadClassFactory(REFIID riid)
    {
        LPVOID ppv;
        HRESULT res = m_mainLibrary.DllGetClassObject(m_clsid, riid, &ppv);
        if (SUCCEEDED(res))
        {
            m_classFactory = static_cast<IClassFactory*>(ppv);
        }
        else
        {
            Log::Warn("DynamicInstanceImpl::LoadClassFactory: Error getting IClassFactory from: ", m_mainLibrary.GetFilePath());
        }

        Log::Debug("DynamicInstanceImpl::LoadClassFactory: ", res);
        return res;
    }

    HRESULT DynamicInstanceImpl::LoadInstance(IUnknown* pUnkOuter, REFIID riid)
    {
        Log::Debug("DynamicInstanceImpl::LoadInstance");

        // Check if the class factory instance is loaded.
        if (m_classFactory == nullptr)
        {
            return E_FAIL;
        }

        // Creates the profiler callback instance from the class factory
        Log::Debug("DynamicInstanceImpl::LoadInstance: m_classFactory: ", ::shared::WHexStr(m_classFactory, sizeof(IClassFactory*)));
        HRESULT res =
            m_classFactory->CreateInstance(nullptr, __uuidof(ICorProfilerCallback10), (void**) &m_corProfilerCallback);
        if (FAILED(res))
        {
            m_corProfilerCallback = nullptr;
            Log::Warn("DynamicInstanceImpl::LoadInstance: Error getting ICorProfilerCallback10 from: ", m_mainLibrary.GetFilePath());
        }

        Log::Debug("DynamicInstanceImpl::LoadInstance: ", res);
        return res;
    }

    HRESULT STDMETHODCALLTYPE DynamicInstanceImpl::DllCanUnloadNow()
    {
        // Check if the library is loaded
        if (!m_loaded)
        {
            return E_FAIL;
        }

        return m_mainLibrary.DllCanUnloadNow();
    }

    ICorProfilerCallback10* DynamicInstanceImpl::GetProfilerCallback()
    {
        return m_corProfilerCallback;
    }

    std::string DynamicInstanceImpl::GetFilePath()
    {
        return m_mainLibrary.GetFilePath();
    }

} // namespace datadog::shared::nativeloader