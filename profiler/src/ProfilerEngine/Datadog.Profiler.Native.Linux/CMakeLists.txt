# ******************************************************
# Project definition
# ******************************************************

project("Datadog.AutoInstrumentation.Profiler.Native.Linux" VERSION 1.0.0)

# ******************************************************
# Compiler options
# ******************************************************

# Sets compiler options
add_compile_options(-std=c++17 -fPIC -fms-extensions -stdlib=libstdc++)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined)
add_compile_options(-DLINUX -Wno-pragmas) 

if (BIT64)
    add_compile_options(-DBIT64)
    add_compile_options(-DHOST_64BIT)
endif()
if (ISAMD64)
    add_compile_options(-DAMD64)
elseif (ISX86)
    add_compile_options(-DBX86)
elseif (ISARM64)
    add_compile_options(-DARM64)
elseif (ISARM)
    add_compile_options(-DARM)
endif()

# ******************************************************
# Environment detection
# ******************************************************

SET(PROFILER_BASENAME Datadog.AutoInstrumentation.Profiler.Native)
SET(PROFILER_STATIC_LIB_NAME ${PROFILER_BASENAME}.static.${ARCH_POSTFIX})
SET(PROFILER_SHARED_LIB_NAME ${PROFILER_BASENAME}.${ARCH_POSTFIX})

# Deployment directory
SET(DEPLOY_DIR ${OUTPUT_BUILD_DIR}/DDProf-Deploy)

# Set Managed Loader folder
SET(MANAGED_LOADER_DIRECTORY ${OUTPUT_BUILD_DIR}/bin/Release-AnyCPU/shared/src/managed-lib/ManagedLoader/netcoreapp2.0/)


SET(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${DEPLOY_DIR})
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${DEPLOY_DIR})
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${DEPLOY_DIR})

# ******************************************************
# Detect prerequisites
# ******************************************************

if (NOT DEFINED RUN_CPPCHECK OR RUN_CPPCHECK EQUAL 0)
    if (NOT EXISTS ${MANAGED_LOADER_DIRECTORY}/Datadog.AutoInstrumentation.ManagedLoader.dll)
        message(FATAL_ERROR "Datadog.AutoInstrumentation.ManagedLoader (Release) must be build first ${MANAGED_LOADER_DIRECTORY}/Datadog.AutoInstrumentation.ManagedLoader.dll.")
    else()
        message(STATUS "Datadog.AutoInstrumentation.ManagedLoader was found")
    endif()
else()
    message(STATUS "Running CppCheck")
endif()


# ******************************************************
# Dependencies
# ******************************************************

# Prepare dependencies
if (NOT EXISTS ${OUTPUT_DEPS_DIR}/fmt)
    message(STATUS "clone fmt")
    add_custom_command(
        OUTPUT ${OUTPUT_DEPS_DIR}/fmt
        COMMAND echo "cloning" && git clone --depth 1 --branch 5.3.0 https://github.com/DataDog/fmt.git && cd fmt && cmake -DCMAKE_POSITION_INDEPENDENT_CODE=TRUE -DFMT_TEST=0 -DFMT_DOC=0 . && make
        WORKING_DIRECTORY ${OUTPUT_DEPS_DIR}
    )
endif()


if (NOT EXISTS ${OUTPUT_DEPS_DIR}/libunwind)
    message(STATUS "clone libunwind 1.5")
    add_custom_command(
        OUTPUT ${OUTPUT_DEPS_DIR}/libunwind
        COMMAND echo "cloning" && git clone --depth 1 --branch v1.5-stable https://github.com/DataDog/libunwind && cd libunwind && ./autogen.sh && ./configure CXXFLAGS="-fPIC" CFLAGS="-fPIC" && make
        WORKING_DIRECTORY ${OUTPUT_DEPS_DIR}
    )
endif()

if(ISLINUX)
    add_custom_command(
            OUTPUT ${OUTPUT_TMP_DIR}/Datadog.AutoInstrumentation.ManagedLoader.dll.o
            COMMAND cp "${MANAGED_LOADER_DIRECTORY}/Datadog.AutoInstrumentation.ManagedLoader.dll" Datadog.AutoInstrumentation.ManagedLoader.dll && ld -r -b binary -o Datadog.AutoInstrumentation.ManagedLoader.dll.o Datadog.AutoInstrumentation.ManagedLoader.dll
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.AutoInstrumentation.ManagedLoader.dll ${OUTPUT_DEPS_DIR}/fmt ${OUTPUT_DEPS_DIR}/libunwind
            WORKING_DIRECTORY ${OUTPUT_TMP_DIR}
    )
    add_custom_command(
            OUTPUT ${OUTPUT_TMP_DIR}/Datadog.AutoInstrumentation.ManagedLoader.pdb.o
            COMMAND cp "${MANAGED_LOADER_DIRECTORY}/Datadog.AutoInstrumentation.ManagedLoader.pdb" Datadog.AutoInstrumentation.ManagedLoader.pdb && ld -r -b binary -o Datadog.AutoInstrumentation.ManagedLoader.pdb.o Datadog.AutoInstrumentation.ManagedLoader.pdb
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.AutoInstrumentation.ManagedLoader.pdb ${OUTPUT_DEPS_DIR}/fmt ${OUTPUT_DEPS_DIR}/libunwind
            WORKING_DIRECTORY ${OUTPUT_TMP_DIR}
    )
endif()


SET(GENERATED_OBJ_FILES
        ${OUTPUT_TMP_DIR}/Datadog.AutoInstrumentation.ManagedLoader.dll.o
        ${OUTPUT_TMP_DIR}/Datadog.AutoInstrumentation.ManagedLoader.pdb.o
)

# ******************************************************
# Define static target
# ******************************************************
add_library(${PROFILER_STATIC_LIB_NAME} STATIC
        LinuxStackFramesCollector.cpp
        OsSpecificApi.cpp
        LinuxThreadsCpuManager.cpp
        ../Datadog.Profiler.Native/ThreadsCpuManager.cpp
        ../Datadog.Profiler.Native/DogstatsdService.cpp
        ../Datadog.Profiler.Native/IMetricsSenderFactory.cpp
        ../Datadog.Profiler.Native/ThreadCpuInfo.cpp
        ../Datadog.Profiler.Native/CorProfilerCallback.cpp
        ../Datadog.Profiler.Native/CorProfilerCallbackFactory.cpp
        ../Datadog.Profiler.Native/HResultConverter.cpp
        ../Datadog.Profiler.Native/ManagedThreadInfo.cpp
        ../Datadog.Profiler.Native/ManagedThreadList.cpp
        ../Datadog.Profiler.Native/OpSysTools.cpp
        ../Datadog.Profiler.Native/PInvoke.cpp
        ../Datadog.Profiler.Native/ProfilerEngineStatus.cpp
        ../Datadog.Profiler.Native/RefCountingObject.cpp
        ../Datadog.Profiler.Native/ResolvedSymbolsCache.cpp
        ../Datadog.Profiler.Native/StackFramesCollectorBase.cpp        
        ../Datadog.Profiler.Native/StackFrameInfo.cpp
        ../Datadog.Profiler.Native/StackSamplerLoop.cpp
        ../Datadog.Profiler.Native/StackSamplerLoopManager.cpp
        ../Datadog.Profiler.Native/StackSnapshotResult.cpp
        ../Datadog.Profiler.Native/StackSnapshotResultReusableBuffer.cpp
        ../Datadog.Profiler.Native/StackSnapshotsBufferManager.cpp
        ../Datadog.Profiler.Native/StackSnapshotsBufferSegment.cpp
        ../Datadog.Profiler.Native/SymbolsResolver.cpp
        ../Datadog.Profiler.Native/SynchronousOffThreadWorkerBase.cpp
	../Datadog.Profiler.Native/ClrLifetime.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/loader.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/string.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/util.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/miniutf.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/il_rewriter.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/il_rewriter_wrapper.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/coreclr/src/pal/prebuilt/idl/corprof_i.cpp
        ${GENERATED_OBJ_FILES}
)

set_target_properties(${PROFILER_STATIC_LIB_NAME} PROPERTIES PREFIX "")

# Define directories includes
target_include_directories(${PROFILER_STATIC_LIB_NAME}
        PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}/../Datadog.Profiler.Native
        PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/coreclr/src/pal/inc/rt
        PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/coreclr/src/pal/prebuilt/inc
        PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/coreclr/src/pal/inc
        PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/coreclr/src/inc
        PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/spdlog/include
        PUBLIC ${OUTPUT_DEPS_DIR}/fmt/include
        PUBLIC ${OUTPUT_DEPS_DIR}/libunwind/include
        PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}
)

# Define linker libraries
target_link_libraries(${PROFILER_STATIC_LIB_NAME}
    ${OUTPUT_DEPS_DIR}/fmt/libfmt.a
    ${OUTPUT_DEPS_DIR}/libunwind/src/.libs/libunwind-x86_64.a
    ${OUTPUT_DEPS_DIR}/libunwind/src/.libs/libunwind.a
    -static-libgcc
    -static-libstdc++
    -lstdc++fs
    -pthread
    -Wl,--no-undefined
    -llzma
)

# ******************************************************
# Define shared target
# ******************************************************

add_library(${PROFILER_SHARED_LIB_NAME} SHARED
    ../Datadog.Profiler.Native/DllMain.cpp
)

set_target_properties(${PROFILER_SHARED_LIB_NAME} PROPERTIES PREFIX "")

# Define linker libraries
target_link_libraries(${PROFILER_SHARED_LIB_NAME} ${PROFILER_STATIC_LIB_NAME})
