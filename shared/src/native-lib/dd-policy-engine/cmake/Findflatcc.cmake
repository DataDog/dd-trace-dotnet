# Copyright 2014 Stefan.Eilemann@epfl.ch
# Copyright 2014 Google Inc. All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
# Find the flatcc schema compiler
#
# Output Variables:
# * FLATCC_FOUND
# * FLATCC_INCLUDE_DIR
# * FLATCC_RUNTIME_LIBRARY

set(FLATCC_CMAKE_DIR ${CMAKE_CURRENT_LIST_DIR})

find_path(FLATCC_INCLUDE_DIR
  NAMES flatcc/flatcc.h
  HINTS
  PATH_SUFFIXES include
  PATHS "${VCPKG_INSTALLED_DIR}/${VCPKG_TARGET_TRIPLET}"
)

find_library(FLATCC_RUNTIME_LIBRARY
  NAMES flatccrt
  PATHS "${VCPKG_INSTALLED_DIR}/${VCPKG_TARGET_TRIPLET}"
)

include(FindPackageHandleStandardArgs)
find_package_handle_standard_args(flatcc
  REQUIRED_VARS FLATCC_INCLUDE_DIR FLATCC_RUNTIME_LIBRARY
)

if(FLATCC_FOUND)
  add_library(flatcc-crt STATIC IMPORTED)
  add_library(flatcc::crt ALIAS flatcc-crt)

  set_target_properties(flatcc-crt PROPERTIES
    IMPORTED_LOCATION ${FLATCC_RUNTIME_LIBRARY}
    INTERFACE_INCLUDE_DIRECTORIES "${FLATCC_INCLUDE_DIR}"
  )

  install(FILES ${FLATCC_RUNTIME_LIBRARY} DESTINATION ${CMAKE_INSTALL_LIBDIR})
  install(DIRECTORY ${FLATCC_INCLUDE_DIR}/flatcc DESTINATION ${CMAKE_INSTALL_INCLUDEDIR})
endif()

if(NOT TARGET flatcc::crt)
  message(FATAL_ERROR "flatcc::crt target was not imported")
endif()
