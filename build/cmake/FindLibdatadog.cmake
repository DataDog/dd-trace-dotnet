include(FetchContent)

set(LIBDATADOG_VERSION "v0.9.0" CACHE STRING "libdatadog version")

if (CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64 OR CMAKE_SYSTEM_PROCESSOR STREQUAL arm64)
    if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
        set(SHA256_LIBDATADOG "a094bae08153b521d467435e3ef5364d3099e4b8aac1291a16c70c51ccc2f171" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "9e55e8917521edf57c28bc2dad363bcc0a68570380480244f898714e31f356fb" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-unknown-linux-gnu.tar.gz)
    endif()
else()
    if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
        #set(SHA256_LIBDATADOG "15e6d94208b94ff5f0e757310c8de372da67099e982d6ebd5cc88fdf8d9c2756" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl.tar.gz)
    else()
        #set(SHA256_LIBDATADOG "277d638d7e1cd9c6724ba3094fab5ce0e3a87a2c26b1cf0a89a86ae2a7f1ddc8" CACHE STRING "libdatadog sha256")
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
