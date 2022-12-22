#include "gtest/gtest.h"
#include "test_dynamic_instance.h"
#include "test_dynamic_dispatcher.h"
#include "test_cor_profiler.h"
#include "../../src/Datadog.Trace.ClrProfiler.Native/cor_profiler.h"

#include <memory>

TEST(cor_profiler, CallBackTests)
{
    // Test dispatcher instance
    auto test_dispatcher = std::make_unique<TestDynamicDispatcherImpl>();

    // Test dynamic instance
    std::unique_ptr<TestDynamicInstanceImpl> test_instance{CreateTestDynamicInstance()};
    // Internal Test cor profiler instance
    auto test_instance_profiler = std::make_unique<TestCorProfiler>();
    // Add test profiler to the dynamic instance
    test_instance->SetProfilerCallback(test_instance_profiler.get());
    // Add dynamic instance to the dynamic dispatcher
    test_dispatcher->SetContinuousProfilerInstance(std::move(test_instance));

    // Test dynamic instance 2
    auto test_instance2 = CreateTestDynamicInstance();
    // Internal Test cor profiler instance 2
    auto test_instance_profiler2 = std::make_unique<TestCorProfiler>();
    // Add test profiler to the dynamic instance 2
    test_instance2->SetProfilerCallback(test_instance_profiler2.get());
    // Add dynamic instance to the dynamic dispatcher
    test_dispatcher->SetTracerInstance(std::move(test_instance2));

    // Test dynamic instance 3
    auto test_instance3 = CreateTestDynamicInstance();
    // Internal Test cor profiler instance 3
    TestCorProfiler* test_instance_profiler3 = new TestCorProfiler();
    // Add test profiler to the dynamic instance 3
    test_instance3->SetProfilerCallback(test_instance_profiler3);
    // Add dynamic instance to the dynamic dispatcher
    test_dispatcher->SetCustomInstance(std::move(test_instance3));

    //
    // User the test dispatcher in the CorProfiler
    //
    auto profiler = std::make_unique<CorProfiler>(test_dispatcher.get());

    // expected to be not S_OK because, we rely on the CLR runtime which is not present
    EXPECT_NE(S_OK, profiler->Initialize(test_instance_profiler.get()));
    EXPECT_EQ(0, test_instance_profiler->m_Initialize);
    EXPECT_EQ(0, test_instance_profiler2->m_Initialize);
    EXPECT_EQ(0, test_instance_profiler3->m_Initialize);

    //

    EXPECT_EQ(S_OK, profiler->AppDomainCreationStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_AppDomainCreationStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_AppDomainCreationStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_AppDomainCreationStarted);

    EXPECT_EQ(S_OK, profiler->AppDomainCreationFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_AppDomainCreationFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_AppDomainCreationFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_AppDomainCreationFinished);

    EXPECT_EQ(S_OK, profiler->AppDomainShutdownStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_AppDomainShutdownStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_AppDomainShutdownStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_AppDomainShutdownStarted);

    EXPECT_EQ(S_OK, profiler->AppDomainShutdownFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_AppDomainShutdownFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_AppDomainShutdownFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_AppDomainShutdownFinished);

    //

    EXPECT_EQ(S_OK, profiler->AssemblyLoadStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_AssemblyLoadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_AssemblyLoadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_AssemblyLoadStarted);

    EXPECT_EQ(S_OK, profiler->AssemblyLoadFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_AssemblyLoadFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_AssemblyLoadFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_AssemblyLoadFinished);

    EXPECT_EQ(S_OK, profiler->AssemblyUnloadStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_AssemblyUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_AssemblyUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_AssemblyUnloadStarted);

    EXPECT_EQ(S_OK, profiler->AssemblyUnloadFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_AssemblyUnloadFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_AssemblyUnloadFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_AssemblyUnloadFinished);

    //

    EXPECT_EQ(S_OK, profiler->ClassLoadStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_ClassLoadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_ClassLoadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_ClassLoadStarted);

    EXPECT_EQ(S_OK, profiler->ClassLoadFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_ClassLoadFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_ClassLoadFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_ClassLoadFinished);

    EXPECT_EQ(S_OK, profiler->ClassUnloadStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_ClassUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_ClassUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_ClassUnloadStarted);

    EXPECT_EQ(S_OK, profiler->ClassUnloadFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_ClassUnloadFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_ClassUnloadFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_ClassUnloadFinished);

    //

    EXPECT_EQ(S_OK, profiler->COMClassicVTableCreated(0, datadog::shared::nativeloader::IID_IUnknown, nullptr, 0));
    EXPECT_EQ(1, test_instance_profiler->m_COMClassicVTableCreated);
    EXPECT_EQ(1, test_instance_profiler2->m_COMClassicVTableCreated);
    EXPECT_EQ(1, test_instance_profiler3->m_COMClassicVTableCreated);

    EXPECT_EQ(S_OK, profiler->COMClassicVTableDestroyed(0, datadog::shared::nativeloader::IID_IUnknown, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_COMClassicVTableDestroyed);
    EXPECT_EQ(1, test_instance_profiler2->m_COMClassicVTableDestroyed);
    EXPECT_EQ(1, test_instance_profiler3->m_COMClassicVTableDestroyed);

    //

    EXPECT_EQ(S_OK, profiler->ConditionalWeakTableElementReferences(0, nullptr, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_ConditionalWeakTableElementReferences);
    EXPECT_EQ(1, test_instance_profiler2->m_ConditionalWeakTableElementReferences);
    EXPECT_EQ(1, test_instance_profiler3->m_ConditionalWeakTableElementReferences);

    EXPECT_EQ(S_OK, profiler->DynamicMethodJITCompilationStarted(0, true, NULL, 0));
    EXPECT_EQ(1, test_instance_profiler->m_DynamicMethodJITCompilationStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_DynamicMethodJITCompilationStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_DynamicMethodJITCompilationStarted);

    EXPECT_EQ(S_OK, profiler->DynamicMethodJITCompilationFinished(0, S_OK, false));
    EXPECT_EQ(1, test_instance_profiler->m_DynamicMethodJITCompilationFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_DynamicMethodJITCompilationFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_DynamicMethodJITCompilationFinished);

    EXPECT_EQ(S_OK, profiler->DynamicMethodUnloaded(0));
    EXPECT_EQ(1, test_instance_profiler->m_DynamicMethodUnloaded);
    EXPECT_EQ(1, test_instance_profiler2->m_DynamicMethodUnloaded);
    EXPECT_EQ(1, test_instance_profiler3->m_DynamicMethodUnloaded);

    //

    EXPECT_EQ(S_OK, profiler->EventPipeProviderCreated(0));
    EXPECT_EQ(1, test_instance_profiler->m_EventPipeProviderCreated);
    EXPECT_EQ(1, test_instance_profiler2->m_EventPipeProviderCreated);
    EXPECT_EQ(1, test_instance_profiler3->m_EventPipeProviderCreated);

    EXPECT_EQ(S_OK, profiler->EventPipeEventDelivered(0, 0, 0, 0, NULL, 0, NULL, NULL, NULL, 0, 0, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_EventPipeEventDelivered);
    EXPECT_EQ(1, test_instance_profiler2->m_EventPipeEventDelivered);
    EXPECT_EQ(1, test_instance_profiler3->m_EventPipeEventDelivered);

    //

    EXPECT_EQ(S_OK, profiler->ExceptionCatcherEnter(0, 0));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionCatcherEnter);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionCatcherEnter);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionCatcherEnter);

    EXPECT_EQ(S_OK, profiler->ExceptionCatcherLeave());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionCatcherLeave);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionCatcherLeave);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionCatcherLeave);

    EXPECT_EQ(S_OK, profiler->ExceptionCLRCatcherExecute());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionCLRCatcherExecute);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionCLRCatcherExecute);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionCLRCatcherExecute);

    EXPECT_EQ(S_OK, profiler->ExceptionCLRCatcherFound());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionCLRCatcherFound);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionCLRCatcherFound);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionCLRCatcherFound);

    EXPECT_EQ(S_OK, profiler->ExceptionOSHandlerEnter(0));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionOSHandlerEnter);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionOSHandlerEnter);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionOSHandlerEnter);

    EXPECT_EQ(S_OK, profiler->ExceptionOSHandlerLeave(0));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionOSHandlerLeave);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionOSHandlerLeave);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionOSHandlerLeave);

    EXPECT_EQ(S_OK, profiler->ExceptionSearchCatcherFound(0));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionSearchCatcherFound);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionSearchCatcherFound);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionSearchCatcherFound);

    EXPECT_EQ(S_OK, profiler->ExceptionSearchFilterEnter(0));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionSearchFilterEnter);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionSearchFilterEnter);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionSearchFilterEnter);

    EXPECT_EQ(S_OK, profiler->ExceptionSearchFilterLeave());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionSearchFilterLeave);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionSearchFilterLeave);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionSearchFilterLeave);

    EXPECT_EQ(S_OK, profiler->ExceptionSearchFunctionEnter(0));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionSearchFunctionEnter);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionSearchFunctionEnter);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionSearchFunctionEnter);

    EXPECT_EQ(S_OK, profiler->ExceptionSearchFunctionLeave());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionSearchFunctionLeave);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionSearchFunctionLeave);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionSearchFunctionLeave);

    EXPECT_EQ(S_OK, profiler->ExceptionThrown(0));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionThrown);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionThrown);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionThrown);

    EXPECT_EQ(S_OK, profiler->ExceptionUnwindFinallyEnter(0));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionUnwindFinallyEnter);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionUnwindFinallyEnter);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionUnwindFinallyEnter);

    EXPECT_EQ(S_OK, profiler->ExceptionUnwindFinallyLeave());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionUnwindFinallyLeave);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionUnwindFinallyLeave);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionUnwindFinallyLeave);

    EXPECT_EQ(S_OK, profiler->ExceptionUnwindFunctionEnter(0));
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionUnwindFunctionEnter);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionUnwindFunctionEnter);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionUnwindFunctionEnter);

    EXPECT_EQ(S_OK, profiler->ExceptionUnwindFunctionLeave());
    EXPECT_EQ(1, test_instance_profiler->m_ExceptionUnwindFunctionLeave);
    EXPECT_EQ(1, test_instance_profiler2->m_ExceptionUnwindFunctionLeave);
    EXPECT_EQ(1, test_instance_profiler3->m_ExceptionUnwindFunctionLeave);

    //

    EXPECT_EQ(S_OK, profiler->FinalizeableObjectQueued(0, 0));
    EXPECT_EQ(1, test_instance_profiler->m_FinalizeableObjectQueued);
    EXPECT_EQ(1, test_instance_profiler2->m_FinalizeableObjectQueued);
    EXPECT_EQ(1, test_instance_profiler3->m_FinalizeableObjectQueued);

    EXPECT_EQ(S_OK, profiler->FunctionUnloadStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_FunctionUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_FunctionUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_FunctionUnloadStarted);

    //

    EXPECT_EQ(S_OK, profiler->GarbageCollectionStarted(0, nullptr, COR_PRF_GC_INDUCED));
    EXPECT_EQ(1, test_instance_profiler->m_GarbageCollectionStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_GarbageCollectionStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_GarbageCollectionStarted);

    EXPECT_EQ(S_OK, profiler->GarbageCollectionFinished());
    EXPECT_EQ(1, test_instance_profiler->m_GarbageCollectionFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_GarbageCollectionFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_GarbageCollectionFinished);

    EXPECT_EQ(S_OK, profiler->GetAssemblyReferences(nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_GetAssemblyReferences);
    EXPECT_EQ(1, test_instance_profiler2->m_GetAssemblyReferences);
    EXPECT_EQ(1, test_instance_profiler3->m_GetAssemblyReferences);

    EXPECT_EQ(S_OK, profiler->GetReJITParameters(0, mdMethodDefNil, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_GetReJITParameters);
    EXPECT_EQ(1, test_instance_profiler2->m_GetReJITParameters);
    EXPECT_EQ(1, test_instance_profiler3->m_GetReJITParameters);

    EXPECT_EQ(S_OK, profiler->HandleCreated(0, 0));
    EXPECT_EQ(1, test_instance_profiler->m_HandleCreated);
    EXPECT_EQ(1, test_instance_profiler2->m_HandleCreated);
    EXPECT_EQ(1, test_instance_profiler3->m_HandleCreated);

    EXPECT_EQ(S_OK, profiler->HandleDestroyed(0));
    EXPECT_EQ(1, test_instance_profiler->m_HandleDestroyed);
    EXPECT_EQ(1, test_instance_profiler2->m_HandleDestroyed);
    EXPECT_EQ(1, test_instance_profiler3->m_HandleDestroyed);

    EXPECT_EQ(S_OK, profiler->InitializeForAttach(nullptr, nullptr, 0));
    EXPECT_EQ(1, test_instance_profiler->m_InitializeForAttach);
    EXPECT_EQ(1, test_instance_profiler2->m_InitializeForAttach);
    EXPECT_EQ(1, test_instance_profiler3->m_InitializeForAttach);

    //

    EXPECT_EQ(S_OK, profiler->JITCachedFunctionSearchStarted(0, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_JITCachedFunctionSearchStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_JITCachedFunctionSearchStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_JITCachedFunctionSearchStarted);

    EXPECT_EQ(S_OK, profiler->JITCachedFunctionSearchFinished(0, COR_PRF_CACHED_FUNCTION_FOUND));
    EXPECT_EQ(1, test_instance_profiler->m_JITCachedFunctionSearchFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_JITCachedFunctionSearchFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_JITCachedFunctionSearchFinished);

    EXPECT_EQ(S_OK, profiler->JITCompilationStarted(0, FALSE)); // do not care about TRUE or FALSE
    EXPECT_EQ(1, test_instance_profiler->m_JITCompilationStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_JITCompilationStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_JITCompilationStarted);

    EXPECT_EQ(S_OK, profiler->JITCompilationFinished(0, S_OK, FALSE)); // do not care about TRUE or FALSE
    EXPECT_EQ(1, test_instance_profiler->m_JITCompilationFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_JITCompilationFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_JITCompilationFinished);

    EXPECT_EQ(S_OK, profiler->JITFunctionPitched(0));
    EXPECT_EQ(1, test_instance_profiler->m_JITFunctionPitched);
    EXPECT_EQ(1, test_instance_profiler2->m_JITFunctionPitched);
    EXPECT_EQ(1, test_instance_profiler3->m_JITFunctionPitched);

    EXPECT_EQ(S_OK, profiler->JITInlining(0, 0, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_JITInlining);
    EXPECT_EQ(1, test_instance_profiler2->m_JITInlining);
    EXPECT_EQ(1, test_instance_profiler3->m_JITInlining);

    //

    EXPECT_EQ(S_OK, profiler->ManagedToUnmanagedTransition(0, COR_PRF_TRANSITION_CALL));
    EXPECT_EQ(1, test_instance_profiler->m_ManagedToUnmanagedTransition);
    EXPECT_EQ(1, test_instance_profiler2->m_ManagedToUnmanagedTransition);
    EXPECT_EQ(1, test_instance_profiler3->m_ManagedToUnmanagedTransition);

    EXPECT_EQ(S_OK, profiler->ModuleAttachedToAssembly(0, 0));
    EXPECT_EQ(1, test_instance_profiler->m_ModuleAttachedToAssembly);
    EXPECT_EQ(1, test_instance_profiler2->m_ModuleAttachedToAssembly);
    EXPECT_EQ(1, test_instance_profiler3->m_ModuleAttachedToAssembly);

    EXPECT_EQ(S_OK, profiler->ModuleInMemorySymbolsUpdated(0));
    EXPECT_EQ(1, test_instance_profiler->m_ModuleInMemorySymbolsUpdated);
    EXPECT_EQ(1, test_instance_profiler2->m_ModuleInMemorySymbolsUpdated);
    EXPECT_EQ(1, test_instance_profiler3->m_ModuleInMemorySymbolsUpdated);

    EXPECT_EQ(S_OK, profiler->ModuleLoadStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_ModuleLoadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_ModuleLoadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_ModuleLoadStarted);

    EXPECT_EQ(S_OK, profiler->ModuleLoadFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_ModuleLoadFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_ModuleLoadFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_ModuleLoadFinished);

    EXPECT_EQ(S_OK, profiler->ModuleUnloadStarted(0));
    EXPECT_EQ(1, test_instance_profiler->m_ModuleUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_ModuleUnloadStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_ModuleUnloadStarted);

    EXPECT_EQ(S_OK, profiler->ModuleUnloadFinished(0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_ModuleUnloadFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_ModuleUnloadFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_ModuleUnloadFinished);

    //

    EXPECT_EQ(S_OK, profiler->MovedReferences(0, nullptr, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_MovedReferences);
    EXPECT_EQ(1, test_instance_profiler2->m_MovedReferences);
    EXPECT_EQ(1, test_instance_profiler3->m_MovedReferences);

    EXPECT_EQ(S_OK, profiler->MovedReferences2(0, nullptr, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_MovedReferences2);
    EXPECT_EQ(1, test_instance_profiler2->m_MovedReferences2);
    EXPECT_EQ(1, test_instance_profiler3->m_MovedReferences2);

    //

    EXPECT_EQ(S_OK, profiler->ObjectAllocated(0, 0));
    EXPECT_EQ(1, test_instance_profiler->m_ObjectAllocated);
    EXPECT_EQ(1, test_instance_profiler2->m_ObjectAllocated);
    EXPECT_EQ(1, test_instance_profiler3->m_ObjectAllocated);

    EXPECT_EQ(S_OK, profiler->ObjectReferences(0, 0, 0, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_ObjectReferences);
    EXPECT_EQ(1, test_instance_profiler2->m_ObjectReferences);
    EXPECT_EQ(1, test_instance_profiler3->m_ObjectReferences);

    EXPECT_EQ(S_OK, profiler->ObjectsAllocatedByClass(0, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_ObjectsAllocatedByClass);
    EXPECT_EQ(1, test_instance_profiler2->m_ObjectsAllocatedByClass);
    EXPECT_EQ(1, test_instance_profiler3->m_ObjectsAllocatedByClass);

    //

    EXPECT_EQ(S_OK, profiler->ProfilerAttachComplete());
    EXPECT_EQ(1, test_instance_profiler->m_ProfilerAttachComplete);
    EXPECT_EQ(1, test_instance_profiler2->m_ProfilerAttachComplete);
    EXPECT_EQ(1, test_instance_profiler3->m_ProfilerAttachComplete);

    EXPECT_EQ(S_OK, profiler->ProfilerDetachSucceeded());
    EXPECT_EQ(1, test_instance_profiler->m_ProfilerDetachSucceeded);
    EXPECT_EQ(1, test_instance_profiler2->m_ProfilerDetachSucceeded);
    EXPECT_EQ(1, test_instance_profiler3->m_ProfilerDetachSucceeded);

    //

    EXPECT_EQ(S_OK, profiler->ReJITCompilationStarted(0, 0, FALSE)); // do not care about TRUE or FALSE
    EXPECT_EQ(1, test_instance_profiler->m_ReJITCompilationStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_ReJITCompilationStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_ReJITCompilationStarted);

    EXPECT_EQ(S_OK, profiler->ReJITCompilationFinished(0, 0, S_OK, FALSE)); // do not care about TRUE or FALSE
    EXPECT_EQ(1, test_instance_profiler->m_ReJITCompilationFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_ReJITCompilationFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_ReJITCompilationFinished);

    EXPECT_EQ(S_OK, profiler->ReJITError(0, mdMethodDefNil, 0, S_OK));
    EXPECT_EQ(1, test_instance_profiler->m_ReJITError);
    EXPECT_EQ(1, test_instance_profiler2->m_ReJITError);
    EXPECT_EQ(1, test_instance_profiler3->m_ReJITError);

    //

    EXPECT_EQ(S_OK, profiler->RemotingClientInvocationStarted());
    EXPECT_EQ(1, test_instance_profiler->m_RemotingClientInvocationStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingClientInvocationStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingClientInvocationStarted);

    EXPECT_EQ(S_OK, profiler->RemotingClientInvocationFinished());
    EXPECT_EQ(1, test_instance_profiler->m_RemotingClientInvocationFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingClientInvocationFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingClientInvocationFinished);

    EXPECT_EQ(S_OK, profiler->RemotingClientReceivingReply(nullptr, FALSE)); // do not care about TRUE or FALSE
    EXPECT_EQ(1, test_instance_profiler->m_RemotingClientReceivingReply);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingClientReceivingReply);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingClientReceivingReply);

    EXPECT_EQ(S_OK, profiler->RemotingClientSendingMessage(nullptr, FALSE)); // do not care about TRUE or FALSE
    EXPECT_EQ(1, test_instance_profiler->m_RemotingClientSendingMessage);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingClientSendingMessage);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingClientSendingMessage);

    EXPECT_EQ(S_OK, profiler->RemotingServerInvocationReturned());
    EXPECT_EQ(1, test_instance_profiler->m_RemotingServerInvocationReturned);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingServerInvocationReturned);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingServerInvocationReturned);

    EXPECT_EQ(S_OK, profiler->RemotingServerInvocationStarted());
    EXPECT_EQ(1, test_instance_profiler->m_RemotingServerInvocationStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingServerInvocationStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingServerInvocationStarted);

    EXPECT_EQ(S_OK, profiler->RemotingServerReceivingMessage(nullptr, FALSE)); // do not care about TRUE or FALSE
    EXPECT_EQ(1, test_instance_profiler->m_RemotingServerReceivingMessage);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingServerReceivingMessage);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingServerReceivingMessage);

    EXPECT_EQ(S_OK, profiler->RemotingServerSendingReply(nullptr, FALSE)); // do not care about TRUE or FALSE
    EXPECT_EQ(1, test_instance_profiler->m_RemotingServerSendingReply);
    EXPECT_EQ(1, test_instance_profiler2->m_RemotingServerSendingReply);
    EXPECT_EQ(1, test_instance_profiler3->m_RemotingServerSendingReply);

    EXPECT_EQ(S_OK, profiler->RootReferences(0, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_RootReferences);
    EXPECT_EQ(1, test_instance_profiler2->m_RootReferences);
    EXPECT_EQ(1, test_instance_profiler3->m_RootReferences);

    EXPECT_EQ(S_OK, profiler->RootReferences2(0, nullptr, nullptr, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_RootReferences2);
    EXPECT_EQ(1, test_instance_profiler2->m_RootReferences2);
    EXPECT_EQ(1, test_instance_profiler3->m_RootReferences2);

    EXPECT_EQ(S_OK, profiler->RuntimeResumeFinished());
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeResumeFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeResumeFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeResumeFinished);

    EXPECT_EQ(S_OK, profiler->RuntimeResumeStarted());
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeResumeStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeResumeStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeResumeStarted);

    EXPECT_EQ(S_OK, profiler->RuntimeSuspendAborted());
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeSuspendAborted);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeSuspendAborted);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeSuspendAborted);

    EXPECT_EQ(S_OK, profiler->RuntimeSuspendFinished());
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeSuspendFinished);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeSuspendFinished);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeSuspendFinished);

    EXPECT_EQ(S_OK, profiler->RuntimeSuspendStarted(COR_PRF_SUSPEND_FOR_PROFILER));
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeSuspendStarted);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeSuspendStarted);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeSuspendStarted);

    EXPECT_EQ(S_OK, profiler->RuntimeThreadResumed(0));
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeThreadResumed);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeThreadResumed);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeThreadResumed);

    EXPECT_EQ(S_OK, profiler->RuntimeThreadSuspended(0));
    EXPECT_EQ(1, test_instance_profiler->m_RuntimeThreadSuspended);
    EXPECT_EQ(1, test_instance_profiler2->m_RuntimeThreadSuspended);
    EXPECT_EQ(1, test_instance_profiler3->m_RuntimeThreadSuspended);

    EXPECT_EQ(S_OK, profiler->Shutdown());
    EXPECT_EQ(1, test_instance_profiler->m_Shutdown);
    EXPECT_EQ(1, test_instance_profiler2->m_Shutdown);
    EXPECT_EQ(1, test_instance_profiler3->m_Shutdown);

    EXPECT_EQ(S_OK, profiler->SurvivingReferences(0, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_SurvivingReferences);
    EXPECT_EQ(1, test_instance_profiler2->m_SurvivingReferences);
    EXPECT_EQ(1, test_instance_profiler3->m_SurvivingReferences);

    EXPECT_EQ(S_OK, profiler->SurvivingReferences2(0, nullptr, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_SurvivingReferences2);
    EXPECT_EQ(1, test_instance_profiler2->m_SurvivingReferences2);
    EXPECT_EQ(1, test_instance_profiler3->m_SurvivingReferences2);

    EXPECT_EQ(S_OK, profiler->ThreadAssignedToOSThread(0, 0));
    EXPECT_EQ(1, test_instance_profiler->m_ThreadAssignedToOSThread);
    EXPECT_EQ(1, test_instance_profiler2->m_ThreadAssignedToOSThread);
    EXPECT_EQ(1, test_instance_profiler3->m_ThreadAssignedToOSThread);

    EXPECT_EQ(S_OK, profiler->ThreadCreated(0));
    EXPECT_EQ(1, test_instance_profiler->m_ThreadCreated);
    EXPECT_EQ(1, test_instance_profiler2->m_ThreadCreated);
    EXPECT_EQ(1, test_instance_profiler3->m_ThreadCreated);

    EXPECT_EQ(S_OK, profiler->ThreadDestroyed(0));
    EXPECT_EQ(1, test_instance_profiler->m_ThreadDestroyed);
    EXPECT_EQ(1, test_instance_profiler2->m_ThreadDestroyed);
    EXPECT_EQ(1, test_instance_profiler3->m_ThreadDestroyed);

    EXPECT_EQ(S_OK, profiler->ThreadNameChanged(0, 0, nullptr));
    EXPECT_EQ(1, test_instance_profiler->m_ThreadNameChanged);
    EXPECT_EQ(1, test_instance_profiler2->m_ThreadNameChanged);
    EXPECT_EQ(1, test_instance_profiler3->m_ThreadNameChanged);

    EXPECT_EQ(S_OK, profiler->UnmanagedToManagedTransition(0, COR_PRF_TRANSITION_CALL));
    EXPECT_EQ(1, test_instance_profiler->m_UnmanagedToManagedTransition);
    EXPECT_EQ(1, test_instance_profiler2->m_UnmanagedToManagedTransition);
    EXPECT_EQ(1, test_instance_profiler3->m_UnmanagedToManagedTransition);
}
