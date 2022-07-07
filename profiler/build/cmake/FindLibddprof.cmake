include(ExternalProject)

set(LIBDDPROF_VERSION "v0.6.0" CACHE STRING "libddprof version")

if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
   set(SHA256_LIBDDPROF "ca5e49636465ee977943d64815442d4bff2de2b74678b1376e6368280534f909" CACHE STRING "libddprof sha256")
   set(LIBDDPROF_BASE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libddprof-${LIBDDPROF_VERSION}/src/libddprof-build/libddprof/libddprof-x86_64-alpine-linux-musl)
else()
   set(SHA256_LIBDDPROF "8eaec92d14bcfa8839843ba2ddfeae254804e087a4984985132a508d6f841645" CACHE STRING "libddprof sha256")
   set(LIBDDPROF_BASE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libddprof-${LIBDDPROF_VERSION}/src/libddprof-build/libddprof/libddprof-x86_64-unknown-linux-gnu)
endif()

ExternalProject_Add(libddprof
  PREFIX "libddprof-${LIBDDPROF_VERSION}"
  INSTALL_COMMAND ""
  CONFIGURE_COMMAND ""
  DOWNLOAD_COMMAND ${CMAKE_SOURCE_DIR}/build/tools/fetch_libddprof.sh ${LIBDDPROF_VERSION} ${SHA256_LIBDDPROF} <BINARY_DIR>
  BUILD_COMMAND ""
)

ExternalProject_Get_property(libddprof BINARY_DIR)

set_property(DIRECTORY PROPERTY ADDITIONAL_MAKE_CLEAN_FILES
    ${BINARY_DIR})

set(LIBDDPROF_REL_FFI_LIB ${LIBDDPROF_BASE_DIR}/lib/libddprof_ffi.a)

list(APPEND
    LIBDDPROF_INCLUDE_DIR
    ${LIBDDPROF_BASE_DIR}/include)

add_library(libddprof-lib STATIC IMPORTED)
set_property(TARGET libddprof-lib PROPERTY
             IMPORTED_LOCATION ${LIBDDPROF_REL_FFI_LIB})
add_dependencies(libddprof-lib ddprof-version)