include(FetchContent)

set(LIBDATADOG_VERSION "v9.0.0" CACHE STRING "libdatadog version")

if (CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64 OR CMAKE_SYSTEM_PROCESSOR STREQUAL arm64)
    if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
        set(SHA256_LIBDATADOG "c2c6c32f612e8c1682131e72bd50492d809bac973f516e56e163e797435eef75" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "97c4fc46f92580b8929e8fcc3f51b47226836e29bce0b57ac8d3387a27a81ce1" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-unknown-linux-gnu.tar.gz)
    endif()
else()
    if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
        set(SHA256_LIBDATADOG "68e67c5e87616f830289bc85626d2062277bef54694cc6dbb445105c66fe8885" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "cd89cbe480db0b828a43afd161ddd83e57319dbe3d412fa4a2d096daae244595" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-unknown-linux-gnu.tar.gz)
    endif()
endif()

FetchContent_Declare(libdatadog-${LIBDATADOG_VERSION}
    URL https://github.com/DataDog/libdatadog/releases/download/${LIBDATADOG_VERSION}/${FILE_TO_DOWNLOAD}
    URL_HASH SHA256=${SHA256_LIBDATADOG}
    SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-${LIBDATADOG_VERSION}
)
if(NOT libdatadog-${LIBDATADOG_VERSION}_POPULATED)
    FetchContent_Populate(libdatadog-${LIBDATADOG_VERSION})
endif()

set(LIBDATADOG_BASE_DIR ${libdatadog-${LIBDATADOG_VERSION}_SOURCE_DIR})

add_library(libdatadog-lib STATIC IMPORTED)

set_target_properties(libdatadog-lib PROPERTIES
    INTERFACE_INCLUDE_DIRECTORIES ${LIBDATADOG_BASE_DIR}/include
    IMPORTED_LOCATION ${LIBDATADOG_BASE_DIR}/lib/libdatadog_profiling.a
)

add_dependencies(libdatadog-lib libdatadog-${LIBDATADOG_VERSION})
