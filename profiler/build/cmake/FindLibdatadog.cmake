include(ExternalProject)

set(LIBDATADOG_VERSION "v0.8.0-rc1" CACHE STRING "libdatadog version")

if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
   set(LIBDATADOG_BASE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-${LIBATADOG_VERSION}/src/libdatadog-build/libdatadog/libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl)
else()
   set(LIBDATADOG_BASE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-${LIBATADOG_VERSION}/src/libdatadog-build/libdatadog/libdatadog-${CMAKE_SYSTEM_PROCESSOR}-unknown-linux-gnu)
endif()

if (CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64)
  if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
    set(SHA256_LIBDATADOG "54ec84a317d7a68fa147c6fe7fc2ed5604748cbf923589d5b77f76354270f452" CACHE STRING "libdatadog sha256")
  else()
    set(SHA256_LIBDATADOG "c57005f5381f998aa46177da26544ce548c637ca65b10bfcf889fbd4dc7bdc14" CACHE STRING "libdatadog sha256")
  endif()
else()
  if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
    set(SHA256_LIBDATADOG "9328f5f07d158d424688eeaf361ed57c97f7a6becf3cb71d3c1e056e43c2eeb4" CACHE STRING "libdatadog sha256")
  else()
    set(SHA256_LIBDATADOG "016c76ba64eefad2d056cfeb5fb2a72e8f4fa30bcf8cfd46947656a88eb4bd62" CACHE STRING "libdatadog sha256")
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