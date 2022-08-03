#pragma once
#include "../../src/Datadog.Trace.ClrProfiler.Native/dynamic_instance.h"

#if AMD64
const std::string CurrentArch = "x64";
#elif X86
const std::string CurrentArch = "x86";
#elif ARM64
const std::string CurrentArch = "arm64";
#elif ARM
const std::string CurrentArch = "arm";
#else
#error "CurrentArch not defined."
#endif

using namespace datadog::shared::nativeloader;

class TestDynamicInstanceImpl : public DynamicInstanceImpl
{
private:
    HRESULT m_loadClassFactory = S_OK;
    HRESULT m_loadInstance = S_OK;
    HRESULT m_dllCanUnloadNow = S_OK;

public:
    TestDynamicInstanceImpl(std::string filePath, std::string clsid) : DynamicInstanceImpl(filePath, clsid)
    {
    }

    HRESULT LoadClassFactory(REFIID riid) override
    {
        if (GetFilePath() != "Test")
            return DynamicInstanceImpl::LoadClassFactory(riid);
        return m_loadClassFactory;
    }

    HRESULT LoadInstance(IUnknown* pUnkOuter, REFIID riid) override
    {
        if (GetFilePath() != "Test")
            return DynamicInstanceImpl::LoadInstance(pUnkOuter, riid);
        return m_loadInstance;
    }

    HRESULT STDMETHODCALLTYPE DllCanUnloadNow() override
    {
        if (GetFilePath() != "Test")
            return DynamicInstanceImpl::DllCanUnloadNow();
        return m_dllCanUnloadNow;
    }

    void SetLoadClassFactoryReturn(HRESULT result)
    {
        m_loadClassFactory = result;
    }

    void SetLoadInstanceReturn(HRESULT result)
    {
        m_loadInstance = result;
    }

    void SetDllCanUnloadNowReturn(HRESULT result)
    {
        m_dllCanUnloadNow = result;
    }

    void SetProfilerCallback(ICorProfilerCallback10* corProfilerCallback)
    {
        m_corProfilerCallback = corProfilerCallback;
    }
};


#if _WINDOWS
const std::string TestDynamicInstanceFilePath = "..\\..\\..\\tracer\\src\\Datadog.Tracer.Native\\bin\\Debug\\" +
                                                CurrentArch + "\\Datadog.Tracer.Native.dll";
#else
const std::string TestDynamicInstanceFilePath = "Test";
#endif
const std::string TestDynamicInstanceIid = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

inline TestDynamicInstanceImpl* CreateTestDynamicInstance(bool useTracerFilePath)
{
    if (useTracerFilePath)
        return new TestDynamicInstanceImpl(TestDynamicInstanceFilePath, TestDynamicInstanceIid);

    return new TestDynamicInstanceImpl("Test", TestDynamicInstanceIid);
}