if (UNIVERSAL)
   find_package(GlibcCompat REQUIRED)
   message(STATUS "Glibc Compat objects")
endif()

# ******************************************************
# Output folders
# ******************************************************

# Set output folders
set(OUTPUT_BIN_DIR ${CMAKE_CURRENT_SOURCE_DIR}/bin)
set(OUTPUT_TMP_DIR ${CMAKE_BINARY_DIR}/tmp.${CMAKE_SYSTEM_NAME}_${CMAKE_SYSTEM_PROCESSOR})
set(OUTPUT_DEPS_DIR ${CMAKE_BINARY_DIR}/deps)
file(MAKE_DIRECTORY ${OUTPUT_BIN_DIR})
file(MAKE_DIRECTORY ${OUTPUT_TMP_DIR})
file(MAKE_DIRECTORY ${OUTPUT_DEPS_DIR})
file(COPY ${CMAKE_CURRENT_SOURCE_DIR}/loader.conf DESTINATION ${OUTPUT_BIN_DIR})

# ******************************************************
# Compiler options
# ******************************************************

# Sets compiler options
add_compile_options(-fPIC -fms-extensions -g)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined)

if (UNIVERSAL)
    add_compile_options(-stdlib=libc++ -DUNIVERSAL)
elseif (ISMACOS)
    add_compile_options(-stdlib=libc++ -DMACOS -Wno-pragma-pack -Wno-deprecated-declarations -fvisibility=hidden -Wno-deprecated-this-capture)
else()
    add_compile_options(-stdlib=libstdc++)
endif()

if(ISLINUX)
    add_compile_options(-DLINUX -Wno-pragmas)
endif()

if (BIT64)
    add_compile_options(-DBIT64 -DHOST_64BIT)
else()
    add_compile_options(-DBIT86 -DHOST_X86)
endif()
if (ISAMD64)
    add_compile_options(-DAMD64)
elseif (ISX86)
    add_compile_options(-DX86)
elseif (ISARM64)
    add_compile_options(-DARM64)
elseif (ISARM)
    add_compile_options(-DARM)
endif()

# ******************************************************
# Define target
# ******************************************************
add_library("Datadog.Trace.ClrProfiler.Native.static" STATIC
        cor_profiler.cpp
        process_helper.cpp
        single_step_guard_rails.cpp
        cor_profiler_class_factory.cpp
        dynamic_dispatcher.cpp
        dynamic_instance.cpp
        runtimeid_store.cpp
        instrumented_assembly_generator/instrumented_assembly_generator_cor_profiler_function_control.cpp
        instrumented_assembly_generator/instrumented_assembly_generator_cor_profiler_info.cpp
        instrumented_assembly_generator/instrumented_assembly_generator_metadata_interfaces.cpp
        instrumented_assembly_generator/member_signature.cpp
        instrumented_assembly_generator/method_signature.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/miniutf.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/string.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/util.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/dynamic_library_base.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/dynamic_com_library.cpp
        )

find_library(LIBDL dl)

# Define linker libraries
if (ISMACOS)
    target_link_libraries("Datadog.Trace.ClrProfiler.Native.static"
        $<$<BOOL:${LIBDL}>:dl>
        coreclr
        spdlog-headers
    )
elseif(ISLINUX)
    set(dd_profiling_linker_script "${CMAKE_CURRENT_SOURCE_DIR}/native_loader.version")
    find_library(LIBPTHREAD pthread)
    target_link_libraries("Datadog.Trace.ClrProfiler.Native.static"
        $<$<BOOL:${LIBPTHREAD}>:pthread>
        $<$<BOOL:${LIBDL}>:dl>
        coreclr
        spdlog-headers
    )
endif()

set_target_properties("Datadog.Trace.ClrProfiler.Native.static" PROPERTIES
    PREFIX ""
    ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR}
    LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR}
    RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR}
)

if (UNIVERSAL)
   add_dependencies("Datadog.Trace.ClrProfiler.Native.static" glibc-compat)
endif()

# ******************************************************
# Define shared target
# ******************************************************
add_library("Datadog.Trace.ClrProfiler.Native" SHARED
    dllmain.cpp
    exported_functions.cpp
)
  
set_target_properties("Datadog.Trace.ClrProfiler.Native" PROPERTIES
    PREFIX ""
    ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR}
    LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR}
    RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR}
)

if(ISLINUX)
    target_link_options("Datadog.Trace.ClrProfiler.Native" PRIVATE
        -static-libgcc
        -static-libstdc++
        -Wl,--build-id=sha1
        -Wl,-version-script=${dd_profiling_linker_script}
        -Wl,--no-undefined
    )
endif()

# Define linker libraries
target_link_libraries("Datadog.Trace.ClrProfiler.Native" "Datadog.Trace.ClrProfiler.Native.static")

if (UNIVERSAL)
    target_link_libraries("Datadog.Trace.ClrProfiler.Native"
        glibc-compat
    )
    target_link_options("Datadog.Trace.ClrProfiler.Native" PRIVATE -flto)
endif()
