if (CMAKE_VERSION VERSION_GREATER_EQUAL "3.24.0")
    cmake_policy(SET CMP0135 NEW)
endif()

include(FetchContent)

set(LIBDATADOG_VERSION "v16.0.3" CACHE STRING "libdatadog version")

if (CMAKE_SYSTEM_PROCESSOR STREQUAL "aarch64" OR CMAKE_SYSTEM_PROCESSOR STREQUAL "arm64")
    if (CMAKE_SYSTEM_NAME STREQUAL "Darwin")
        set(SHA256_LIBDATADOG "2d7933e09dc39706e9c99c7edcff5c60f7567ea2777157596de828f62f39035b" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-apple-darwin.tar.gz)
    elseif (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
        set(SHA256_LIBDATADOG "dd08d3a4dbbd765392121d27b790d7818e80dd28500b554db16e9186b1025ba9" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "decc01a2e0f732cabcc56594429a3dbc13678070e07f24891555dcc02df2e516" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-unknown-linux-gnu.tar.gz)
    endif()
else()
    if (CMAKE_SYSTEM_NAME STREQUAL "Darwin")
        set(SHA256_LIBDATADOG "ced5db61e0ca8e974b9d59b0b6833c28e19445a3e4ec3c548fda965806c17560" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-x86_64-apple-darwin.tar.gz)
    elseif (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
        set(SHA256_LIBDATADOG "8e09afd3cfb5ace85501f37b4bd6378299ebbf71189ccc2173169998b75b4b56" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "caaec84fc9afbcb3ec4618791b3c3f1ead65196009e9f07fd382e863dc3bdc66" CACHE STRING "libdatadog sha256")
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
