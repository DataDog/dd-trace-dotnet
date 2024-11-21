if (CMAKE_VERSION VERSION_GREATER_EQUAL "3.24.0")
    cmake_policy(SET CMP0135 NEW)
endif()

include(FetchContent)

<<<<<<< HEAD
set(LIBDATADOG_VERSION "v14.3.1" CACHE STRING "libdatadog version")

if (CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64 OR CMAKE_SYSTEM_PROCESSOR STREQUAL arm64)
    if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
        set(SHA256_LIBDATADOG "57f83aff275628bb1af89c22bb4bd696726daf2a9e09b6cd0d966b29e65a7ad6" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "36db8d50ccabb71571158ea13835c0f1d05d30b32135385f97c16343cfb6ddd4" CACHE STRING "libdatadog sha256")
=======
set(LIBDATADOG_VERSION "v14.2.0" CACHE STRING "libdatadog version")

if (CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64 OR CMAKE_SYSTEM_PROCESSOR STREQUAL arm64)
    if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
        set(SHA256_LIBDATADOG "d794bb19f5d64dcc6b914b818a45003712c518abd0be451df62bab286c123205" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "7b45ceb2ae9fd5f143660b9d63373825f6d9d87f3be8458b5a5385f2c2cacee1" CACHE STRING "libdatadog sha256")
>>>>>>> 634b0518b (Bump libdatadog to 14.2.0)
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-unknown-linux-gnu.tar.gz)
    endif()
else()
    if (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
<<<<<<< HEAD
        set(SHA256_LIBDATADOG "2f61fd21cf2f8147743e414b4a8c77250a17be3aecc42a69ffe54f0a603d5c92" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "f01f05600591063eba4faf388f54c155ab4e6302e5776c7855e3734955f7daf7" CACHE STRING "libdatadog sha256")
=======
        set(SHA256_LIBDATADOG "fffefde7c5bdfcc087ab8c8d43e93e6bdc677eddb980497ec405f92d8b00fb5a" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "b85ffe35337fd5a9efb6f9c0817a6897c78519863c2c983863158262ea40db9c" CACHE STRING "libdatadog sha256")
>>>>>>> 634b0518b (Bump libdatadog to 14.2.0)
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

add_library(libdatadog-lib SHARED IMPORTED)

set_target_properties(libdatadog-lib PROPERTIES
    INTERFACE_INCLUDE_DIRECTORIES ${LIBDATADOG_BASE_DIR}/include
    IMPORTED_LOCATION ${LIBDATADOG_BASE_DIR}/lib/libdatadog_profiling.so
)

add_dependencies(libdatadog-lib libdatadog-${LIBDATADOG_VERSION})

# Override target_link_libraries
function(target_link_libraries target)
    # Call the original target_link_libraries
    _target_link_libraries(${ARGV})

    if("libdatadog-lib" IN_LIST ARGN)
        add_custom_command(
            TARGET ${target}
            POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_if_different
                $<TARGET_FILE:libdatadog-lib>
                $<TARGET_FILE_DIR:${target}>
            COMMENT "Copying libdatadog to ${target} output directory"
        )
    endif()
endfunction()