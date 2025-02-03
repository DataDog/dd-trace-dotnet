if (CMAKE_VERSION VERSION_GREATER_EQUAL "3.24.0")
    cmake_policy(SET CMP0135 NEW)
endif()

include(FetchContent)

set(LIBDATADOG_VERSION "v15.0.0" CACHE STRING "libdatadog version")


if (CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64 OR CMAKE_SYSTEM_PROCESSOR STREQUAL arm64)
    if (CMAKE_SYSTEM_NAME MATCHES "Darwin")
        set(SHA256_LIBDATADOG "4540ffb8ccb671550a39ba79226117086582c1eaf9714180a9e26bd6bb175860" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-apple-darwin.tar.gz)
    else()
        if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
            set(SHA256_LIBDATADOG "d5b969b293e5a9e5e36404a553bbafdd55ff6af0b089698bd989a878534df0c7" CACHE STRING "libdatadog sha256")
            set(FILE_TO_DOWNLOAD libdatadog-aarch64-alpine-linux-musl.tar.gz)
        else()
            set(SHA256_LIBDATADOG "31bceab4f56873b03b3728760d30e3abc493d32ca8fdc9e1f2ec2147ef4d5424" CACHE STRING "libdatadog sha256")
            set(FILE_TO_DOWNLOAD libdatadog-aarch64-unknown-linux-gnu.tar.gz)
        endif()
    endif()
else()
    if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
        set(SHA256_LIBDATADOG "530348c4b02cc7096de2231476ec12db82e2cc6de12a87e5b28af47ea73d4e56" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "5073ffc657bc4698f8bdd4935475734577bfb18c54dcbebc4f7d8c7595626e52" CACHE STRING "libdatadog sha256")
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
