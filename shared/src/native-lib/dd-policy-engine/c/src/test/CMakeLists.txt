function(utest_discover_tests target)
  ## Generate a unique name based on the extra arguments
  set(args_hash "1234")
  # string(SHA1 args_hash "${_TEST_SPEC} ${_EXTRA_ARGS} ${_REPORTER} ${_OUTPUT_DIR} ${_OUTPUT_PREFIX} ${_OUTPUT_SUFFIX}")
  # string(SUBSTRING ${args_hash} 0 7 args_hash)

  # Define rule to generate test list for aforementioned test executable
  set(ctest_file_base "${CMAKE_CURRENT_BINARY_DIR}/${target}-${args_hash}")
  set(ctest_include_file "${ctest_file_base}_include.cmake")
  set(ctest_tests_file "${ctest_file_base}_tests.cmake")

  add_custom_command(
    TARGET ${target} POST_BUILD
    BYPRODUCTS "${ctest_tests_file}"
    COMMAND "${CMAKE_COMMAND}"
            -D "TEST_EXECUTABLE=$<TARGET_FILE:${target}>"
            -D "CTEST_FILE=${ctest_tests_file}"
            -P "${CMAKE_SOURCE_DIR}/cmake/utest-add-tests.cmake"
    VERBATIM
  )

  file(WRITE "${ctest_include_file}"
    "if(EXISTS \"${ctest_tests_file}\")\n"
    "  include(\"${ctest_tests_file}\")\n"
    "else()\n"
    "  add_test(${TARGET}_NOT_BUILT-${args_hash} ${TARGET}_NOT_BUILT-${args_hash})\n"
    "endif()\n"
  )

  # Add discovered tests to directory TEST_INCLUDE_FILES
  set_property(DIRECTORY
    APPEND PROPERTY TEST_INCLUDE_FILES "${ctest_include_file}"
  )
endfunction()

add_executable(dd-policy-engine.unit-tests)
target_sources(dd-policy-engine.unit-tests
  PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/test_actions.c
    ${CMAKE_CURRENT_SOURCE_DIR}/test_eval_ctx.c
    ${CMAKE_CURRENT_SOURCE_DIR}/test_evaluator.c
    ${CMAKE_CURRENT_SOURCE_DIR}/test_main.c
    ${CMAKE_CURRENT_SOURCE_DIR}/test_policy_reader.c
)

target_include_directories(dd-policy-engine.unit-tests
  PRIVATE
    ${CMAKE_BINARY_DIR}/c
    ${CMAKE_SOURCE_DIR}/c/src
    ${CMAKE_SOURCE_DIR}/c/src/schema
)

target_link_libraries(dd-policy-engine.unit-tests
  PUBLIC
    dd-policy-engine::obj
)

utest_discover_tests(dd-policy-engine.unit-tests)

