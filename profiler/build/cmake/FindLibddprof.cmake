include(ExternalProject)

set(LIBDDPROF_VERSION "v0.2.0" CACHE STRING "libddprof version")
set(SHA256_LIBDDPROF "cba0f24074d44781d7252b912faff50d330957e84a8f40a172a8138e81001f27" CACHE STRING "libddprof sha256")


set(LIBDDPROF_BASE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libddprof-${LIBDDPROF_VERSION}/src/libddprof-build/libddprof/libddprof-x86_64-unknown-linux-gnu)

ExternalProject_Add(libddprof
  PREFIX "libddprof-${LIBDDPROF_VERSION}"
  INSTALL_COMMAND ""
  CONFIGURE_COMMAND ""
  DOWNLOAD_COMMAND ${CMAKE_SOURCE_DIR}/build/tools/fetch_libddprof.sh ${LIBDDPROF_VERSION} ${SHA256_LIBDDPROF} <BINARY_DIR>
  BUILD_COMMAND ""
)

set(LIBDDPROF_REL_FFI_LIB ${LIBDDPROF_BASE_DIR}/lib/libddprof_ffi.a)

list(APPEND
    LIBDDPROF_INCLUDE_DIR
    ${LIBDDPROF_BASE_DIR}/include)

add_library(libddprof-lib STATIC IMPORTED)
set_property(TARGET libddprof-lib PROPERTY
             IMPORTED_LOCATION ${LIBDDPROF_REL_FFI_LIB})
add_dependencies(libddprof-lib ddprof-version)
