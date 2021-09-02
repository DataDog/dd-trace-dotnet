#include "gtest/gtest.h"
#include "test_dynamic_instance.h"
#include "test_dynamic_dispatcher.h"
#include "test_cor_profiler.h"
#include "../../src/Datadog.AutoInstrumentation.NativeLoader/cor_profiler.h"

TEST(cor_profiler, CallBackTests)
{
    // Test dispatcher instance
    TestDynamicDispatcherImpl* test_dispatcher = new TestDynamicDispatcherImpl();

    // Test dynamic instance
    TestDynamicInstanceImpl* test_instance = CreateTestDynamicInstance(false);
    // Internal Test cor profiler instance
    TestCorProfiler* test_instance_profiler = new TestCorProfiler();
    // Add test profiler to the dynamic instance
    test_instance->SetProfilerCallback(test_instance_profiler);
    // Add dynamic instance to the dynamic dispatcher
    test_dispatcher->SetContinuousProfilerInstance(std::unique_ptr<IDynamicInstance>(test_instance));

    // Test dynamic instance 2
    TestDynamicInstanceImpl* test_instance2 = CreateTestDynamicInstance(false);
    // Internal Test cor profiler instance 2
    TestCorProfiler* test_instance_profiler2 = new TestCorProfiler();
    // Add test profiler to the dynamic instance 2
    test_instance2->SetProfilerCallback(test_instance_profiler2);
    // Add dynamic instance to the dynamic dispatcher
    test_dispatcher->SetTracerInstance(std::unique_ptr<IDynamicInstance>(test_instance2));

    // Test dynamic instance 3
    TestDynamicInstanceImpl* test_instance3 = CreateTestDynamicInstance(false);
    // Internal Test cor profiler instance 3
    TestCorProfiler* test_instance_profiler3 = new TestCorProfiler();
    // Add test profiler to the dynamic instance 3
    test_instance3->SetProfilerCallback(test_instance_profiler3);
    // Add dynamic instance to the dynamic dispatcher
    test_dispatcher->SetCustomInstance(std::unique_ptr<IDynamicInstance>(test_instance3));

    //
    // User the test dispatcher in the CorProfiler
    //
    CorProfiler* profiler = new CorProfiler(test_dispatcher);

    EXPECT_HRESULT_FAILED(profiler->Initialize(test_instance_profiler));
    EXPECT_EQ(0, test_instance_profiler->m_Initialize);
    EXPECT_EQ(0, test_instance_profiler2->m_Initialize);
    EXPECT_EQ(0, test_instance_profiler3->m_Initialize);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->AppDomainCreationStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_AppDomainCreationStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_AppDomainCreationStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_AppDomainCreationStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->AppDomainCreationFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_AppDomainCreationFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_AppDomainCreationFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_AppDomainCreationFinished);

    EXPECT_HRESULT_SUCCEEDED(profiler->AppDomainShutdownStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_AppDomainShutdownStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_AppDomainShutdownStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_AppDomainShutdownStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->AppDomainShutdownFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_AppDomainShutdownFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_AppDomainShutdownFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_AppDomainShutdownFinished);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->AssemblyLoadStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_AssemblyLoadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_AssemblyLoadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_AssemblyLoadStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->AssemblyLoadFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_AssemblyLoadFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_AssemblyLoadFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_AssemblyLoadFinished);

    EXPECT_HRESULT_SUCCEEDED(profiler->AssemblyUnloadStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_AssemblyUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_AssemblyUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_AssemblyUnloadStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->AssemblyUnloadFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_AssemblyUnloadFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_AssemblyUnloadFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_AssemblyUnloadFinished);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->ClassLoadStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_ClassLoadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_ClassLoadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_ClassLoadStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->ClassLoadFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_ClassLoadFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_ClassLoadFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_ClassLoadFinished);

    EXPECT_HRESULT_SUCCEEDED(profiler->ClassUnloadStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_ClassUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_ClassUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_ClassUnloadStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->ClassUnloadFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_ClassUnloadFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_ClassUnloadFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_ClassUnloadFinished);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->COMClassicVTableCreated(0, datadog::shared::nativeloader::IID_IUnknown, NULL, 0));
    EXPECT_EQ(1, test_instance_profiler->m_COMClassicVTableCreated);
    EXPECT_EQ(1, test_instance_profiler2->m_COMClassicVTableCreated);
    EXPECT_EQ(1, test_instance_profiler3->m_COMClassicVTableCreated);

    EXPECT_HRESULT_SUCCEEDED(profiler->COMClassicVTableDestroyed(0, datadog::shared::nativeloader::IID_IUnknown, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_COMClassicVTableDestroyed);
    EXPECT_EQ(1, test_instance_profiler2->m_COMClassicVTableDestroyed);
    EXPECT_EQ(1, test_instance_profiler3->m_COMClassicVTableDestroyed);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->ConditionalWeakTableElementReferences(0, nullptr, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_ConditionalWeakTableElementReferences);
    EXPECT_EQ(1, test_instance_profiler2->m_ConditionalWeakTableElementReferences);
    EXPECT_EQ(1, test_instance_profiler3->m_ConditionalWeakTableElementReferences);

    EXPECT_HRESULT_SUCCEEDED(profiler->DynamicMethodJITCompilationStarted(0, true, NULL, 0));
    EXPECT_EQ(1, test_instance_profiler->m_DynamicMethodJITCompilationStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_DynamicMethodJITCompilationStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_DynamicMethodJITCompilationStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->DynamicMethodJITCompilationFinished(0, S_OK, false));
    EXPECT_EQ(1, test_instance_profiler->m_DynamicMethodJITCompilationFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_DynamicMethodJITCompilationFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_DynamicMethodJITCompilationFinished);

    EXPECT_HRESULT_SUCCEEDED(profiler->DynamicMethodUnloaded(0));
    EXPECT_EQ(1, test_instance_profiler->m_DynamicMethodUnloaded);
    EXPECT_EQ(1, test_instance_profiler2->m_DynamicMethodUnloaded);
    EXPECT_EQ(1, test_instance_profiler3->m_DynamicMethodUnloaded);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->EventPipeProviderCreated(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_EventPipeProviderCreated);
    EXPECT_EQ(1, test_instance_profiler2->m_EventPipeProviderCreated);
    EXPECT_EQ(1, test_instance_profiler3->m_EventPipeProviderCreated);

    EXPECT_HRESULT_SUCCEEDED(profiler->EventPipeEventDelivered(NULL, NULL, NULL, 0, NULL, 0, NULL, NULL, NULL, NULL, 0, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_EventPipeEventDelivered);
    EXPECT_EQ(1, test_instance_profiler2->m_EventPipeEventDelivered);
    EXPECT_EQ(1, test_instance_profiler3->m_EventPipeEventDelivered);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionCatcherEnter(NULL, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionCatcherEnter);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionCatcherEnter);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionCatcherEnter);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionCatcherLeave());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionCatcherLeave);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionCatcherLeave);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionCatcherLeave);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionCLRCatcherExecute());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionCLRCatcherExecute);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionCLRCatcherExecute);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionCLRCatcherExecute);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionCLRCatcherFound());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionCLRCatcherFound);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionCLRCatcherFound);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionCLRCatcherFound);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionOSHandlerEnter(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionOSHandlerEnter);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionOSHandlerEnter);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionOSHandlerEnter);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionOSHandlerLeave(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionOSHandlerLeave);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionOSHandlerLeave);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionOSHandlerLeave);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionSearchCatcherFound(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionSearchCatcherFound);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionSearchCatcherFound);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionSearchCatcherFound);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionSearchFilterEnter(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionSearchFilterEnter);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionSearchFilterEnter);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionSearchFilterEnter);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionSearchFilterLeave());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionSearchFilterLeave);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionSearchFilterLeave);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionSearchFilterLeave);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionSearchFunctionEnter(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionSearchFunctionEnter);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionSearchFunctionEnter);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionSearchFunctionEnter);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionSearchFunctionLeave());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionSearchFunctionLeave);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionSearchFunctionLeave);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionSearchFunctionLeave);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionThrown(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionThrown);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionThrown);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionThrown);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionUnwindFinallyEnter(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionUnwindFinallyEnter);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionUnwindFinallyEnter);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionUnwindFinallyEnter);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionUnwindFinallyLeave());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionUnwindFinallyLeave);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionUnwindFinallyLeave);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionUnwindFinallyLeave);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionUnwindFunctionEnter(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionUnwindFunctionEnter);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionUnwindFunctionEnter);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionUnwindFunctionEnter);

    EXPECT_HRESULT_SUCCEEDED(profiler->ExceptionUnwindFunctionLeave());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionUnwindFunctionLeave);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionUnwindFunctionLeave);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionUnwindFunctionLeave);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->FinalizeableObjectQueued(NULL, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_FinalizeableObjectQueued);
    EXPECT_EQ(1, test_instance_profiler2->m_FinalizeableObjectQueued);
    EXPECT_EQ(1, test_instance_profiler3->m_FinalizeableObjectQueued);

    EXPECT_HRESULT_SUCCEEDED(profiler->FunctionUnloadStarted(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_FunctionUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_FunctionUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_FunctionUnloadStarted);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->GarbageCollectionStarted(0, nullptr, COR_PRF_GC_INDUCED));
    EXPECT_EQ(1, test_instance_profiler->m_GarbageCollectionStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_GarbageCollectionStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_GarbageCollectionStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->GarbageCollectionFinished());
    EXPECT_EQ(1, test_instance_profiler->m_GarbageCollectionFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_GarbageCollectionFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_GarbageCollectionFinished);

    EXPECT_HRESULT_SUCCEEDED(profiler->GetAssemblyReferences(nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_GetAssemblyReferences);
    EXPECT_EQ(1, test_instance_profiler2->m_GetAssemblyReferences);
    EXPECT_EQ(1, test_instance_profiler3->m_GetAssemblyReferences);

    EXPECT_HRESULT_SUCCEEDED(profiler->GetReJITParameters(0, mdMethodDefNil, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_GetReJITParameters);
    EXPECT_EQ(1, test_instance_profiler2->m_GetReJITParameters);
    EXPECT_EQ(1, test_instance_profiler3->m_GetReJITParameters);

    EXPECT_HRESULT_SUCCEEDED(profiler->HandleCreated(NULL, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_HandleCreated);
    EXPECT_EQ(1, test_instance_profiler2->m_HandleCreated);
    EXPECT_EQ(1, test_instance_profiler3->m_HandleCreated);

    EXPECT_HRESULT_SUCCEEDED(profiler->HandleDestroyed(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_HandleDestroyed);
    EXPECT_EQ(1, test_instance_profiler2->m_HandleDestroyed);
    EXPECT_EQ(1, test_instance_profiler3->m_HandleDestroyed);

    EXPECT_HRESULT_SUCCEEDED(profiler->InitializeForAttach(nullptr, nullptr, 0));
    EXPECT_EQ(1, test_instance_profiler->m_InitializeForAttach);
    EXPECT_EQ(1, test_instance_profiler2->m_InitializeForAttach);
    EXPECT_EQ(1, test_instance_profiler3->m_InitializeForAttach);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->JITCachedFunctionSearchStarted(NULL, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_JITCachedFunctionSearchStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_JITCachedFunctionSearchStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_JITCachedFunctionSearchStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->JITCachedFunctionSearchFinished(NULL, COR_PRF_CACHED_FUNCTION_FOUND));
    EXPECT_EQ(1, test_instance_profiler->m_JITCachedFunctionSearchFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_JITCachedFunctionSearchFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_JITCachedFunctionSearchFinished);

    EXPECT_HRESULT_SUCCEEDED(profiler->JITCompilationStarted(NULL, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_JITCompilationStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_JITCompilationStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_JITCompilationStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->JITCompilationFinished(NULL, S_OK, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_JITCompilationFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_JITCompilationFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_JITCompilationFinished);

    EXPECT_HRESULT_SUCCEEDED(profiler->JITFunctionPitched(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_JITFunctionPitched);
    EXPECT_EQ(1, test_instance_profiler2->m_JITFunctionPitched);
    EXPECT_EQ(1, test_instance_profiler3->m_JITFunctionPitched);

    EXPECT_HRESULT_SUCCEEDED(profiler->JITInlining(NULL, NULL, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_JITInlining);
    EXPECT_EQ(1, test_instance_profiler2->m_JITInlining);
    EXPECT_EQ(1, test_instance_profiler3->m_JITInlining);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->ManagedToUnmanagedTransition(NULL, COR_PRF_TRANSITION_CALL));
    EXPECT_EQ(1, test_instance_profiler->m_ManagedToUnmanagedTransition);
    EXPECT_EQ(1, test_instance_profiler2->m_ManagedToUnmanagedTransition);
    EXPECT_EQ(1, test_instance_profiler3->m_ManagedToUnmanagedTransition);

    EXPECT_HRESULT_SUCCEEDED(profiler->ModuleAttachedToAssembly(NULL, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ModuleAttachedToAssembly);
    EXPECT_EQ(1, test_instance_profiler2->m_ModuleAttachedToAssembly);
    EXPECT_EQ(1, test_instance_profiler3->m_ModuleAttachedToAssembly);

    EXPECT_HRESULT_SUCCEEDED(profiler->ModuleInMemorySymbolsUpdated(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ModuleInMemorySymbolsUpdated);
    EXPECT_EQ(1, test_instance_profiler2->m_ModuleInMemorySymbolsUpdated);
    EXPECT_EQ(1, test_instance_profiler3->m_ModuleInMemorySymbolsUpdated);

    EXPECT_HRESULT_SUCCEEDED(profiler->ModuleLoadStarted(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ModuleLoadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_ModuleLoadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_ModuleLoadStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->ModuleLoadFinished(NULL, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_ModuleLoadFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_ModuleLoadFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_ModuleLoadFinished);

    EXPECT_HRESULT_SUCCEEDED(profiler->ModuleUnloadStarted(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ModuleUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_ModuleUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_ModuleUnloadStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->ModuleUnloadFinished(NULL, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_ModuleUnloadFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_ModuleUnloadFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_ModuleUnloadFinished);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->MovedReferences(0, NULL, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_MovedReferences);
    EXPECT_EQ(1, test_instance_profiler2->m_MovedReferences);
    EXPECT_EQ(1, test_instance_profiler3->m_MovedReferences);

    EXPECT_HRESULT_SUCCEEDED(profiler->MovedReferences2(0, nullptr, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_MovedReferences2);
    EXPECT_EQ(1, test_instance_profiler2->m_MovedReferences2);
    EXPECT_EQ(1, test_instance_profiler3->m_MovedReferences2);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->ObjectAllocated(NULL, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ObjectAllocated);
    EXPECT_EQ(1, test_instance_profiler2->m_ObjectAllocated);
    EXPECT_EQ(1, test_instance_profiler3->m_ObjectAllocated);

    EXPECT_HRESULT_SUCCEEDED(profiler->ObjectReferences(NULL, NULL, 0, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_ObjectReferences);
    EXPECT_EQ(1, test_instance_profiler2->m_ObjectReferences);
    EXPECT_EQ(1, test_instance_profiler3->m_ObjectReferences);

    EXPECT_HRESULT_SUCCEEDED(profiler->ObjectsAllocatedByClass(0, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_ObjectsAllocatedByClass);
    EXPECT_EQ(1, test_instance_profiler2->m_ObjectsAllocatedByClass);
    EXPECT_EQ(1, test_instance_profiler3->m_ObjectsAllocatedByClass);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->ProfilerAttachComplete());
    EXPECT_EQ(1, test_instance_profiler->m_ProfilerAttachComplete);
    EXPECT_EQ(1, test_instance_profiler2->m_ProfilerAttachComplete);
    EXPECT_EQ(1, test_instance_profiler3->m_ProfilerAttachComplete);

    EXPECT_HRESULT_SUCCEEDED(profiler->ProfilerDetachSucceeded());
    EXPECT_EQ(1, test_instance_profiler->m_ProfilerDetachSucceeded);
    EXPECT_EQ(1, test_instance_profiler2->m_ProfilerDetachSucceeded);
    EXPECT_EQ(1, test_instance_profiler3->m_ProfilerDetachSucceeded);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->ReJITCompilationStarted(NULL, NULL, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ReJITCompilationStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_ReJITCompilationStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_ReJITCompilationStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->ReJITCompilationFinished(NULL, NULL, S_OK, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ReJITCompilationFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_ReJITCompilationFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_ReJITCompilationFinished);

    EXPECT_HRESULT_SUCCEEDED(profiler->ReJITError(NULL, mdMethodDefNil, NULL, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_ReJITError);
    EXPECT_EQ(1, test_instance_profiler2->m_ReJITError);
    EXPECT_EQ(1, test_instance_profiler3->m_ReJITError);

    //

    EXPECT_HRESULT_SUCCEEDED(profiler->RemotingClientInvocationStarted());
    EXPECT_EQ(1, test_instance_profiler->m_RemotingClientInvocationStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingClientInvocationStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingClientInvocationStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->RemotingClientInvocationFinished());
    EXPECT_EQ(1, test_instance_profiler->m_RemotingClientInvocationFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingClientInvocationFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingClientInvocationFinished);

    EXPECT_HRESULT_SUCCEEDED(profiler->RemotingClientReceivingReply(nullptr, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_RemotingClientReceivingReply);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingClientReceivingReply);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingClientReceivingReply);

    EXPECT_HRESULT_SUCCEEDED(profiler->RemotingClientSendingMessage(nullptr, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_RemotingClientSendingMessage);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingClientSendingMessage);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingClientSendingMessage);

    EXPECT_HRESULT_SUCCEEDED(profiler->RemotingServerInvocationReturned());
    EXPECT_EQ(1, test_instance_profiler->m_RemotingServerInvocationReturned);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingServerInvocationReturned);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingServerInvocationReturned);

    EXPECT_HRESULT_SUCCEEDED(profiler->RemotingServerInvocationStarted());
    EXPECT_EQ(1, test_instance_profiler->m_RemotingServerInvocationStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingServerInvocationStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingServerInvocationStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->RemotingServerReceivingMessage(nullptr, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_RemotingServerReceivingMessage);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingServerReceivingMessage);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingServerReceivingMessage);

    EXPECT_HRESULT_SUCCEEDED(profiler->RemotingServerSendingReply(nullptr, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_RemotingServerSendingReply);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingServerSendingReply);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingServerSendingReply);

    EXPECT_HRESULT_SUCCEEDED(profiler->RootReferences(0, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_RootReferences);
    EXPECT_EQ(1, test_instance_profiler2->m_RootReferences);
    EXPECT_EQ(1, test_instance_profiler3->m_RootReferences);

    EXPECT_HRESULT_SUCCEEDED(profiler->RootReferences2(0, nullptr, nullptr, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_RootReferences2);
    EXPECT_EQ(1, test_instance_profiler2->m_RootReferences2);
    EXPECT_EQ(1, test_instance_profiler3->m_RootReferences2);

    EXPECT_HRESULT_SUCCEEDED(profiler->RuntimeResumeFinished());
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeResumeFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeResumeFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeResumeFinished);

    EXPECT_HRESULT_SUCCEEDED(profiler->RuntimeResumeStarted());
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeResumeStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeResumeStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeResumeStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->RuntimeSuspendAborted());
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeSuspendAborted);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeSuspendAborted);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeSuspendAborted);

    EXPECT_HRESULT_SUCCEEDED(profiler->RuntimeSuspendFinished());
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeSuspendFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeSuspendFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeSuspendFinished);

    EXPECT_HRESULT_SUCCEEDED(profiler->RuntimeSuspendStarted(COR_PRF_SUSPEND_FOR_PROFILER));
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeSuspendStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeSuspendStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeSuspendStarted);

    EXPECT_HRESULT_SUCCEEDED(profiler->RuntimeThreadResumed(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeThreadResumed);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeThreadResumed);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeThreadResumed);

    EXPECT_HRESULT_SUCCEEDED(profiler->RuntimeThreadSuspended(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeThreadSuspended);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeThreadSuspended);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeThreadSuspended);

    EXPECT_HRESULT_SUCCEEDED(profiler->Shutdown());
    EXPECT_EQ(1, test_instance_profiler->m_Shutdown);
    EXPECT_EQ(1, test_instance_profiler2->m_Shutdown);
    EXPECT_EQ(1, test_instance_profiler3->m_Shutdown);

    EXPECT_HRESULT_SUCCEEDED(profiler->SurvivingReferences(0, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_SurvivingReferences);
    EXPECT_EQ(1, test_instance_profiler2->m_SurvivingReferences);
    EXPECT_EQ(1, test_instance_profiler3->m_SurvivingReferences);

    EXPECT_HRESULT_SUCCEEDED(profiler->SurvivingReferences2(0, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_SurvivingReferences2);
    EXPECT_EQ(1, test_instance_profiler2->m_SurvivingReferences2);
    EXPECT_EQ(1, test_instance_profiler3->m_SurvivingReferences2);

    EXPECT_HRESULT_SUCCEEDED(profiler->ThreadAssignedToOSThread(NULL, NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ThreadAssignedToOSThread);
    EXPECT_EQ(1, test_instance_profiler2->m_ThreadAssignedToOSThread);
    EXPECT_EQ(1, test_instance_profiler3->m_ThreadAssignedToOSThread);

    EXPECT_HRESULT_SUCCEEDED(profiler->ThreadCreated(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ThreadCreated);
    EXPECT_EQ(1, test_instance_profiler2->m_ThreadCreated);
    EXPECT_EQ(1, test_instance_profiler3->m_ThreadCreated);

    EXPECT_HRESULT_SUCCEEDED(profiler->ThreadDestroyed(NULL));
    EXPECT_EQ(1, test_instance_profiler->m_ThreadDestroyed);
    EXPECT_EQ(1, test_instance_profiler2->m_ThreadDestroyed);
    EXPECT_EQ(1, test_instance_profiler3->m_ThreadDestroyed);

    EXPECT_HRESULT_SUCCEEDED(profiler->ThreadNameChanged(NULL, 0, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_ThreadNameChanged);
    EXPECT_EQ(1, test_instance_profiler2->m_ThreadNameChanged);
    EXPECT_EQ(1, test_instance_profiler3->m_ThreadNameChanged);

    EXPECT_HRESULT_SUCCEEDED(profiler->UnmanagedToManagedTransition(NULL, COR_PRF_TRANSITION_CALL));
    EXPECT_EQ(1, test_instance_profiler->m_UnmanagedToManagedTransition);
    EXPECT_EQ(1, test_instance_profiler2->m_UnmanagedToManagedTransition);
    EXPECT_EQ(1, test_instance_profiler3->m_UnmanagedToManagedTransition);

    // Clean up
    delete profiler;
    delete test_instance_profiler;
    delete test_dispatcher;
}
