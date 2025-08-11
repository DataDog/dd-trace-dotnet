add_library(dd-policy-engine.objects OBJECT)
add_library(dd-policy-engine::obj ALIAS dd-policy-engine.objects)

# MSVC doesn't support C23 yet. Use C17 instead.
target_compile_features(dd-policy-engine.objects
  PUBLIC
    c_std_17
)

target_sources(dd-policy-engine.objects
  PUBLIC
    FILE_SET public_headers
    TYPE HEADERS
    BASE_DIRS ${CMAKE_CURRENT_SOURCE_DIR}/include
    FILES 
      ${CMAKE_CURRENT_SOURCE_DIR}/include/dd/policies/action.h
      ${CMAKE_CURRENT_SOURCE_DIR}/include/dd/policies/error_codes.h
      ${CMAKE_CURRENT_SOURCE_DIR}/include/dd/policies/eval_ctx.h
      ${CMAKE_CURRENT_SOURCE_DIR}/include/dd/policies/evaluation_result.h
      ${CMAKE_CURRENT_SOURCE_DIR}/include/dd/policies/evaluator_default.h
      ${CMAKE_CURRENT_SOURCE_DIR}/include/dd/policies/evaluator_types.h
      ${CMAKE_CURRENT_SOURCE_DIR}/include/dd/policies/policies.h
      ${CMAKE_CURRENT_SOURCE_DIR}/include/dd/policies/wls.h
  PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src/eval_ctx.c
    ${CMAKE_CURRENT_SOURCE_DIR}/src/evaluator.c
    ${CMAKE_CURRENT_SOURCE_DIR}/src/evaluator_default.c
    ${CMAKE_CURRENT_SOURCE_DIR}/src/policy.c
)

target_include_directories(dd-policy-engine.objects
  PRIVATE
    ${CMAKE_CURRENT_BINARY_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${CMAKE_CURRENT_SOURCE_DIR}/src/schema
)

set_target_properties(dd-policy-engine.objects
  PROPERTIES
    VERIFY_INTERFACE_HEADER_SETS ON
)

target_link_libraries(dd-policy-engine.objects
  PUBLIC
    flatcc::crt
)

install(
  TARGETS dd-policy-engine.objects
  EXPORT dd-policy-engine-targets
  FILE_SET public_headers DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
)

# Static Lib
add_library(dd-policy-engine.static-lib STATIC)
add_library(dd-policy-engine::static-lib ALIAS dd-policy-engine.static-lib)

target_sources(dd-policy-engine.static-lib
  PRIVATE
    $<TARGET_OBJECTS:dd-policy-engine.objects>
)

target_link_libraries(dd-policy-engine.static-lib
  PUBLIC
    dd-policy-engine.objects
)

set_target_properties(dd-policy-engine.static-lib
  PROPERTIES 
    OUTPUT_NAME "policies"
)

# Debugger
if(DD_POLICY_BUILD_EXAMPLES)
add_executable(dd-policy-engine.debugger 
  ${CMAKE_CURRENT_SOURCE_DIR}/src/debugger/basic-debugger.c
)

target_include_directories(dd-policy-engine.debugger
  PRIVATE
    ${CMAKE_CURRENT_BINARY_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${CMAKE_CURRENT_SOURCE_DIR}/src/schema
)

target_link_libraries(dd-policy-engine.debugger
  PRIVATE
    dd-policy-engine::obj
)

set_target_properties(dd-policy-engine.debugger
  PROPERTIES 
    OUTPUT_NAME "basic-debugger"
)
endif()

install(
  TARGETS dd-policy-engine.static-lib
  EXPORT dd-policy-engine-targets
  LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
  ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
  RUNTIME DESTINATION ${CMAKE_INSTALL_LIBDIR}
)

