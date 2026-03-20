# ******************************************************
# Output folders
# ******************************************************

# Set output folders
set(OUTPUT_BIN_DIR ${CMAKE_CURRENT_SOURCE_DIR}/build/bin)
set(OUTPUT_TMP_DIR ${CMAKE_BINARY_DIR}/tmp.${CMAKE_SYSTEM_NAME}_${CMAKE_SYSTEM_PROCESSOR})
set(OUTPUT_DEPS_DIR ${CMAKE_BINARY_DIR}/deps)
file(MAKE_DIRECTORY ${OUTPUT_BIN_DIR})
file(MAKE_DIRECTORY ${OUTPUT_TMP_DIR})
file(MAKE_DIRECTORY ${OUTPUT_DEPS_DIR})

# ******************************************************
# Compiler options
# ******************************************************

# Sets compiler options
add_compile_options(-fPIC -fms-extensions -fvisibility=hidden -g)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined)
if (ISMACOS)
    add_compile_options(-stdlib=libc++ -DMACOS -Wno-pragma-pack -Wno-deprecated-declarations -Wno-deprecated-this-capture)
elseif(ISLINUX)
    add_compile_options(-stdlib=libstdc++ -DLINUX -Wno-pragmas)
endif()
if (BIT64)
    add_compile_options(-DBIT64 -DHOST_64BIT)
endif()
if (ISAMD64)
    add_compile_options(-DAMD64)
elseif (ISX86)
    add_compile_options(-DBX86 -DHOST_X86)
elseif (ISARM64)
    add_compile_options(-DARM64)
elseif (ISARM)
    add_compile_options(-DARM)
endif()

# ******************************************************
# Define static target
# ******************************************************
add_library("Datadog.Tracer.Native.static" STATIC
        class_factory.cpp
        clr_helpers.cpp
        cor_profiler_base.cpp
        cor_profiler.cpp
        il_rewriter_wrapper.cpp
        il_rewriter.cpp
        integration.cpp
        metadata_builder.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/miniutf.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/string.cpp
        ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-src/util.cpp
        calltarget_tokens.cpp
        rejit_handler.cpp
        debugger_method_rewriter.cpp
        debugger_probes_instrumentation_requester.cpp
        debugger_rejit_handler_module_method.cpp
        debugger_rejit_preprocessor.cpp
        debugger_tokens.cpp
        rejit_preprocessor.cpp
        rejit_work_offloader.cpp
        environment_variables_util.cpp
        method_rewriter.cpp
        tracer_tokens.cpp
        signature_builder.cpp
        debugger_environment_variables_util.cpp
        debugger_probes_tracker.cpp
        iast/aspect.cpp
        iast/aspect_filter.cpp
        iast/dataflow.cpp
        iast/dataflow_aspects.cpp
        iast/dataflow_il_analysis.cpp
        iast/dataflow_il_rewriter.cpp
        iast/hardcoded_secrets_method_analyzer.cpp
        iast/iast_util.cpp
        iast/method_analyzer.cpp
        iast/method_analyzers.cpp
        iast/method_info.cpp
        iast/module_info.cpp
        iast/signature_info.cpp
        iast/signature_types.cpp
        iast/string_literal_aspect_filter.cpp
        iast/string_optimization_aspect_filter.cpp
        fault_tolerant_envionrment_variables_util.cpp
        fault_tolerant_method_duplicator.cpp
        fault_tolerant_rewriter.cpp
        fault_tolerant_tokens.cpp
        fault_tolerant_tracker.cpp
        fault_tolerant_cor_profiler_function_control.cpp
        threadUtils.cpp
        function_control_wrapper.cpp
        tracer_handler_module_method.cpp
        tracer_integration_definition.cpp
        tracer_method_rewriter.cpp
        tracer_rejit_preprocessor.cpp
        Generated/generated_callsites.g.cpp
        Generated/generated_calltargets.g.cpp
)

set_target_properties("Datadog.Tracer.Native.static" PROPERTIES
    PREFIX ""
    ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR}
    LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR}
    RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR}
)

# Define linker libraries
if (ISMACOS)
    target_link_libraries("Datadog.Tracer.Native.static"
        managed-loader-objs
        coreclr
        re2-lib
        spdlog-headers
        ${CMAKE_DL_LIBS}
        ${GENERATED_OBJ_FILES}
    )
elseif(ISLINUX)
    target_link_libraries("Datadog.Tracer.Native.static"
        managed-loader-objs
        coreclr
        re2-lib
        spdlog-headers
        ${CMAKE_DL_LIBS}
    )
endif()

add_dependencies("Datadog.Tracer.Native.static" managed-loader-objs)

# ******************************************************
# Define shared target
# ******************************************************
add_library("Datadog.Tracer.Native" SHARED
    dllmain.cpp
    interop.cpp
)
  
set_target_properties("Datadog.Tracer.Native" PROPERTIES
    PREFIX ""
    ARCHIVE_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR}
    LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR}
    RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_BIN_DIR}
)

if(ISLINUX)
    target_link_options("Datadog.Tracer.Native" PRIVATE
        -static-libgcc
        -static-libstdc++
        -Wl,--build-id=sha1
    )
endif()

# Define linker libraries
target_link_libraries("Datadog.Tracer.Native" "Datadog.Tracer.Native.static")
