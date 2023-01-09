# This is is a hacky solution until https://gitlab.kitware.com/cmake/cmake/-/merge_requests/8011
# is merged and depoyed.
# The previous attempt (passing git clone command in the DOWNLOAD_COMMAND arg) was failing
# if the download is run more than once.

if (EXISTS "${PROJECT_NAME}_gitclone")
  message (STATUS "Module ${PROJECT_NAME} was already cloned. Skip the download step")
  return()
endif()

set(error_code 1)
execute_process(
  COMMAND "/usr/bin/git" clone --quiet --depth 1 --config advice.detachedHead=false --branch ${PROJECT_BRANCH} --origin "origin" "${PROJECT_REPOSITORY}" "${PROJECT_NAME}"
  RESULT_VARIABLE error_code
  )

if(error_code)
  message(FATAL_ERROR "Failed to clone repository: '${PROJECT_REPOSITORY}'")
endif()

execute_process(
  COMMAND ${CMAKE_COMMAND} -E touch "${PROJECT_NAME}_gitclone"
  RESULT_VARIABLE error_code
  )
