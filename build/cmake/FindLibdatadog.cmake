if (CMAKE_VERSION VERSION_GREATER_EQUAL "3.24.0")
    cmake_policy(SET CMP0135 NEW)
endif()

include(FetchContent)

set(LIBDATADOG_VERSION "v18.0.0" CACHE STRING "libdatadog version")

if (CMAKE_SYSTEM_PROCESSOR STREQUAL "aarch64" OR CMAKE_SYSTEM_PROCESSOR STREQUAL "arm64")
    if (CMAKE_SYSTEM_NAME STREQUAL "Darwin")
        set(SHA256_LIBDATADOG "1b63df9650c2d087ec291198616a9bc2237b52ad532244eccbf5923a0662815b" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-apple-darwin.tar.gz)
    elseif (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
        set(SHA256_LIBDATADOG "4b64b58162d215a4f16b6ced4d602667565ebe20015341219daa998e3cf4e0a8" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "f544316a2b58476979a3b05f0236837790320c385a73f1e111f8736b95ca3a87" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-unknown-linux-gnu.tar.gz)
    endif()
else()
    if (CMAKE_SYSTEM_NAME STREQUAL "Darwin")
        set(SHA256_LIBDATADOG "9402b83ecee3a73da8b4bccee1c57a3a8ac6e6d175d50fbee08d458eeda69c16" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-x86_64-apple-darwin.tar.gz)
    elseif (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
        set(SHA256_LIBDATADOG "8af91ff3f7d266a6acc55b3a12a927a3d1b6ab51845b3d54333965086453c1c6" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "c7c7f0ce597d515ce6aa8bcf3edd12a009c2c02dd5e715ea318a3bcf3221a65d" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-unknown-linux-gnu.tar.gz)
    endif()
endif()

FetchContent_Declare(libdatadog-install
    URL https://github.com/DataDog/libdatadog/releases/download/${LIBDATADOG_VERSION}/${FILE_TO_DOWNLOAD}
    URL_HASH SHA256=${SHA256_LIBDATADOG}
    SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install
)
if(NOT libdatadog-install_POPULATED)
    FetchContent_Populate(libdatadog-install)
endif()

set(LIBDATADOG_BASE_DIR ${libdatadog-install_SOURCE_DIR})

add_library(libdatadog-lib SHARED IMPORTED)

set_target_properties(libdatadog-lib PROPERTIES
    INTERFACE_INCLUDE_DIRECTORIES ${LIBDATADOG_BASE_DIR}/include
    IMPORTED_LOCATION ${LIBDATADOG_BASE_DIR}/lib/libdatadog_profiling.so
)

add_dependencies(libdatadog-lib libdatadog-install)

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
