include(ExternalProject)

set(LIBDATADOG_VERSION "v0.7.0" CACHE STRING "libdatadog version")

if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
   set(LIBDATADOG_BASE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-${LIBATADOG_VERSION}/src/libdatadog-build/libddatadog/libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl)
else()
   set(LIBDATADOG_BASE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-${LIBATADOG_VERSION}/src/libdatadog-build/libdatadog/libdatadog-${CMAKE_SYSTEM_PROCESSOR}-unknown-linux-gnu)
endif()

if (CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64)
  if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
    set(SHA256_LIBDATADOG "de0ba9c95da07d89b487d99b36f767f763f1272ebdff5b532b576473d64f3c66" CACHE STRING "libdatadog sha256")
  else()
    set(SHA256_LIBDATADOG "256750fe9ebcd9bf8426b83f89f52572f01559ae5f4add3a4c46df84fc122eb6" CACHE STRING "libdatadog sha256")
  endif()
else()
  if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
    set(SHA256_LIBDATADOG "0ae6b3d9d37e6af8e31a44286424c34ddd4945022efbaed6978fc60a8b923ba6" CACHE STRING "libdatadog sha256")
  else()
    set(SHA256_LIBDATADOG "b5f617d08e637e9a201437198e715b2f3688d5367d0af572988c80dd3c2e6b81" CACHE STRING "libdatadog sha256")
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

set(LIBDATADOG_REL_FFI_LIB ${LIBDATADOG_BASE_DIR}/lib/libddprof_ffi.a)

list(APPEND
    LIBDATADOG_INCLUDE_DIR
    ${LIBDATADOG_BASE_DIR}/include)

add_library(libdatadog-lib STATIC IMPORTED)
set_property(TARGET libdatadog-lib PROPERTY
             IMPORTED_LOCATION ${LIBDATADOG_REL_FFI_LIB})
add_dependencies(libdatadog-lib libdatadog)