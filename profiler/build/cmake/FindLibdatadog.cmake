include(ExternalProject)

set(LIBDATADOG_VERSION "v0.9.0" CACHE STRING "libdatadog version")

if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
   set(LIBDATADOG_BASE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-${LIBATADOG_VERSION}/src/libdatadog-build/libdatadog/libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl)
else()
   set(LIBDATADOG_BASE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-${LIBATADOG_VERSION}/src/libdatadog-build/libdatadog/libdatadog-${CMAKE_SYSTEM_PROCESSOR}-unknown-linux-gnu)
endif()

if (CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64)
  if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
    set(SHA256_LIBDATADOG "a094bae08153b521d467435e3ef5364d3099e4b8aac1291a16c70c51ccc2f171" CACHE STRING "libdatadog sha256")
  else()
    set(SHA256_LIBDATADOG "9e55e8917521edf57c28bc2dad363bcc0a68570380480244f898714e31f356fb" CACHE STRING "libdatadog sha256")
  endif()
else()
  if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
    set(SHA256_LIBDATADOG "15e6d94208b94ff5f0e757310c8de372da67099e982d6ebd5cc88fdf8d9c2756" CACHE STRING "libdatadog sha256")
  else()
    set(SHA256_LIBDATADOG "277d638d7e1cd9c6724ba3094fab5ce0e3a87a2c26b1cf0a89a86ae2a7f1ddc8" CACHE STRING "libdatadog sha256")
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
