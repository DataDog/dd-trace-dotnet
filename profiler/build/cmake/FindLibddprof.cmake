include(ExternalProject)

set(LIBDDPROF_VERSION "v0.2.0" CACHE STRING "libddprof version")

if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
   set(SHA256_LIBDDPROF "d519a6241d78260522624b8e79e98502510f11d5d9551f5f80fc1134e95fa146" CACHE STRING "libddprof sha256")
   set(LIBDDPROF_BINARY_FOLDER libddprof-x86_64-alpine-linux-musl)
else()
   set(SHA256_LIBDDPROF "cba0f24074d44781d7252b912faff50d330957e84a8f40a172a8138e81001f27" CACHE STRING "libddprof sha256")
   set(LIBDDPROF_BINARY_FOLDER libddprof-x86_64-unknown-linux-gnu)
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

set(LIBDDPROF_REL_FFI_LIB ${BINARY_DIR}/${LIBDDPROF_BINARY_FOLDER}/lib/libddprof_ffi.a)

list(APPEND
    LIBDDPROF_INCLUDE_DIR
    ${BINARY_DIR}/${LIBDDPROF_BINARY_FOLDER}/include)

add_library(libddprof-lib STATIC IMPORTED)
set_property(TARGET libddprof-lib PROPERTY
             IMPORTED_LOCATION ${LIBDDPROF_REL_FFI_LIB})
add_dependencies(libddprof-lib ddprof-version)
