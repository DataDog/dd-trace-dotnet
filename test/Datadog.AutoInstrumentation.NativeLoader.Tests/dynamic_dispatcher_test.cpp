#include "gtest/gtest.h"
#include "test_dynamic_dispatcher.h"
#include "test_dynamic_instance.h"

TEST(dynamic_dispatcher, CreateInstance)
{
    TestDynamicDispatcherImpl* test_dispatcher = new TestDynamicDispatcherImpl();
    test_dispatcher->SetTracerInstance(std::unique_ptr<IDynamicInstance>(CreateTestDynamicInstance(true)));

    IDynamicDispatcher* dispatcher = test_dispatcher;

    HRESULT result = dispatcher->LoadClassFactory(datadog::shared::nativeloader::IID_IUnknown);
    EXPECT_HRESULT_SUCCEEDED(result);

    result = dispatcher->LoadInstance(nullptr, datadog::shared::nativeloader::IID_IUnknown);
    EXPECT_HRESULT_SUCCEEDED(result);

#if _WINDOWS
    IDynamicInstance* instance = dispatcher->GetTracerInstance();
    ICorProfilerCallback10* profiler = instance->GetProfilerCallback();
    EXPECT_NE(nullptr, profiler);
#else
    // This assert is currently not supported on non windows platform.
#endif

    result = dispatcher->DllCanUnloadNow();
    EXPECT_HRESULT_SUCCEEDED(result);

    delete dispatcher;
}