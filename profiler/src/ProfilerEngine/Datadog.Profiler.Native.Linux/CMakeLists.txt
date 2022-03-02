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

if(ISLINUX)
    add_custom_command(
            OUTPUT ${OUTPUT_TMP_DIR}/Datadog.AutoInstrumentation.ManagedLoader.dll.o
            COMMAND cp "${MANAGED_LOADER_DIRECTORY}/Datadog.AutoInstrumentation.ManagedLoader.dll" Datadog.AutoInstrumentation.ManagedLoader.dll && ld -r -b binary -o Datadog.AutoInstrumentation.ManagedLoader.dll.o Datadog.AutoInstrumentation.ManagedLoader.dll
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.AutoInstrumentation.ManagedLoader.dll
            WORKING_DIRECTORY ${OUTPUT_TMP_DIR}
    )
    add_custom_command(
            OUTPUT ${OUTPUT_TMP_DIR}/Datadog.AutoInstrumentation.ManagedLoader.pdb.o
            COMMAND cp "${MANAGED_LOADER_DIRECTORY}/Datadog.AutoInstrumentation.ManagedLoader.pdb" Datadog.AutoInstrumentation.ManagedLoader.pdb && ld -r -b binary -o Datadog.AutoInstrumentation.ManagedLoader.pdb.o Datadog.AutoInstrumentation.ManagedLoader.pdb
            DEPENDS ${MANAGED_LOADER_DIRECTORY}/Datadog.AutoInstrumentation.ManagedLoader.pdb
            WORKING_DIRECTORY ${OUTPUT_TMP_DIR}
    )
endif()



SET(GENERATED_OBJ_FILES
    ${OUTPUT_TMP_DIR}/Datadog.AutoInstrumentation.ManagedLoader.dll.o
    ${OUTPUT_TMP_DIR}/Datadog.AutoInstrumentation.ManagedLoader.pdb.o
)

FILE(GLOB LINUX_PROFILER_SRC CONFIGURE_DEPENDS "*.cpp")

FILE(GLOB COMMON_PROFILER_SRC LIST_DIRECTORIES false "../Datadog.Profiler.Native/*.cpp")
FILE(GLOB EXCLUDE_DLLMAIN "../Datadog.Profiler.Native/DllMain.cpp")

list(REMOVE_ITEM COMMON_PROFILER_SRC "${EXCLUDE_DLLMAIN}")

# ******************************************************
# Define static target
# ******************************************************
add_library(${PROFILER_STATIC_LIB_NAME} STATIC
    ${LINUX_PROFILER_SRC}
    ${COMMON_PROFILER_SRC}
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
        PUBLIC ${FMT_INCLUDE}
        PUBLIC ${LIBUNWIND_INCLUDES}
        PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}
        PUBLIC ${LIBDDPROF_INCLUDE_DIR}
)

# Define linker libraries
target_link_libraries(${PROFILER_STATIC_LIB_NAME}
    fmt-lib
    libunwind-x86_64-lib
    libunwind-lib
    libddprof-lib
    -static-libgcc
    -static-libstdc++
    -lstdc++fs
    -pthread
    -Wl,--no-undefined
    -llzma
    -ldl
)

add_dependencies(${PROFILER_STATIC_LIB_NAME} fmt-lib libunwind-x86_64-lib libunwind-lib libddprof-lib)

# ******************************************************
# Define shared target
# ******************************************************

add_library(${PROFILER_SHARED_LIB_NAME} SHARED
    ../Datadog.Profiler.Native/DllMain.cpp
)

set_target_properties(${PROFILER_SHARED_LIB_NAME} PROPERTIES PREFIX "")

# Define linker libraries
target_link_libraries(${PROFILER_SHARED_LIB_NAME} ${PROFILER_STATIC_LIB_NAME})
