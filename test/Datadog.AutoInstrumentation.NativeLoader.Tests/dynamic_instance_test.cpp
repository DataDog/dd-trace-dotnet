#include "gtest/gtest.h"
#include "test_dynamic_instance.h"

TEST(dynamic_instance, CreateInstance)
{
    IDynamicInstance* instance = CreateTestDynamicInstance(true);

    EXPECT_EQ(TestDynamicInstanceFilePath, instance->GetFilePath());

    HRESULT result = instance->LoadClassFactory(datadog::shared::nativeloader::IID_IUnknown);
    EXPECT_HRESULT_SUCCEEDED(result);

    result = instance->LoadInstance(nullptr, datadog::shared::nativeloader::IID_IUnknown);
    EXPECT_HRESULT_SUCCEEDED(result);

#if _WINDOWS
    ICorProfilerCallback10* profiler = instance->GetProfilerCallback();
    EXPECT_NE(nullptr, profiler);
#else
    // This assert is currently not supported on non windows platform.
#endif

    result = instance->DllCanUnloadNow();
    EXPECT_HRESULT_SUCCEEDED(result);

    delete instance;
}