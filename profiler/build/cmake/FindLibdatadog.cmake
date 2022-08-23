include(ExternalProject)

set(LIBDATADOG_VERSION "v0.8.0" CACHE STRING "libdatadog version")

if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
   set(LIBDATADOG_BASE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-${LIBATADOG_VERSION}/src/libdatadog-build/libdatadog/libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl)
else()
   set(LIBDATADOG_BASE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-${LIBATADOG_VERSION}/src/libdatadog-build/libdatadog/libdatadog-${CMAKE_SYSTEM_PROCESSOR}-unknown-linux-gnu)
endif()

if (CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64)
  if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
    set(SHA256_LIBDATADOG "68919ddf9bc6491927bf16fb819b18fd052209d77774097b57f7879ebafc9bdf" CACHE STRING "libdatadog sha256")
  else()
    set(SHA256_LIBDATADOG "9c6dd7058c7d0c9af8ffe18b4565fcda08462debc81f60ce0eb87aa5f7b74a0b" CACHE STRING "libdatadog sha256")
  endif()
else()
  if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
    set(SHA256_LIBDATADOG "e410300255d93f016562e7e072dcb09f94d0550ff3e289f97fff4cd155a4d3a4" CACHE STRING "libdatadog sha256")
  else()
    set(SHA256_LIBDATADOG "94f52edaed31f8c2a25cd569b0b065f8bb221120706d57ef2ca592b0512333f2" CACHE STRING "libdatadog sha256")
  endif()
endif()

ExternalProject_Add(libdatadog
  PREFIX "libdatadog-${LIBATADOG_VERSION}"
  INSTALL_COMMAND ""
  CONFIGURE_COMMAND ""
  DOWNLOAD_COMMAND ${CMAKE_SOURCE_DIR}/build/tools/fetch_libdatadog.sh ${LIBDATADOG_VERSION} ${CMAKE_SYSTEM_PROCESSOR} ${SHA256_LIBDATADOG} <BINARY_DIR>
  BUILD_COMMAND ""
)

ExternalProject_Get_property(libdatadog BINARY_DIR)

set_property(DIRECTORY PROPERTY ADDITIONAL_MAKE_CLEAN_FILES
    ${BINARY_DIR})

set(LIBDATADOG_REL_FFI_LIB ${LIBDATADOG_BASE_DIR}/lib/libdatadog_profiling.a)

list(APPEND
    LIBDATADOG_INCLUDE_DIR
    ${LIBDATADOG_BASE_DIR}/include)

add_library(libdatadog-lib STATIC IMPORTED)
set_property(TARGET libdatadog-lib PROPERTY
             IMPORTED_LOCATION ${LIBDATADOG_REL_FFI_LIB})
add_dependencies(libdatadog-lib libdatadog)