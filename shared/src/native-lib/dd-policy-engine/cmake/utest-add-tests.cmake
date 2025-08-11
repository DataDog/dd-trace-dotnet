function(add_command NAME)
  set(_args "")
  # use ARGV* instead of ARGN, because ARGN splits arrays into multiple arguments
  math(EXPR _last_arg ${ARGC}-1)
  foreach(_n RANGE 1 ${_last_arg})
    set(_arg "${ARGV${_n}}")
    if(_arg MATCHES "[^-./:a-zA-Z0-9_]")
      set(_args "${_args} [==[${_arg}]==]") # form a bracket_argument
    else()
      set(_args "${_args} ${_arg}")
    endif()
  endforeach()
  set(script "${script}${NAME}(${_args})\n" PARENT_SCOPE)
endfunction()

function(utest_discover_tests_impl)
  cmake_parse_arguments(
    ""
    ""
    "TEST_EXECUTABLE;CTEST_FILE"
    ""
    ${ARGN}
  )

  set(tests)
  set(script)

  if(NOT EXISTS "${_TEST_EXECUTABLE}")
    message(FATAL_ERROR "Specified test executable '${_TEST_EXECUTABLE}' does not exist")
  endif()

  execute_process(
    COMMAND ${CMAKE_COMMAND} -E echo "Discovering tests for ${target}..."
    COMMAND "${_TEST_EXECUTABLE}" --list-tests
    OUTPUT_VARIABLE listing_output
    RESULT_VARIABLE result
  )

  if(NOT ${result} EQUAL 0)
    message(FATAL_ERROR
      "Error listing tests from executable '${_TEST_EXECUTABLE}':\n"
      "  Result: ${result}\n"
      "  Output: ${listing_output}\n"
    )
  endif()

  string(REPLACE "\n" ";" test_lines "${listing_output}")

  foreach(line IN LISTS test_lines)
    string(STRIP "${line}" test_case)

    if(test_case STREQUAL "")
        continue()
    endif()

    add_command(add_test
      "${test_case}"
      "${_TEST_EXECUTABLE}"
      "--filter=${test_case}"
    )
    list(APPEND tests "${test_case}")
  endforeach()

  file(WRITE "${_CTEST_FILE}" "${script}")
endfunction()

if(CMAKE_SCRIPT_MODE_FILE)
  utest_discover_tests_impl(
    TEST_EXECUTABLE ${TEST_EXECUTABLE}
    CTEST_FILE ${CTEST_FILE}
  )
endif()
