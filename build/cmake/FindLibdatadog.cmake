if (CMAKE_VERSION VERSION_GREATER_EQUAL "3.24.0")
    cmake_policy(SET CMP0135 NEW)
endif()

include(FetchContent)

set(LIBDATADOG_VERSION "v17.0.0" CACHE STRING "libdatadog version")

if (CMAKE_SYSTEM_PROCESSOR STREQUAL "aarch64" OR CMAKE_SYSTEM_PROCESSOR STREQUAL "arm64")
    if (CMAKE_SYSTEM_NAME STREQUAL "Darwin")
        set(SHA256_LIBDATADOG "e21459dff099abab524894dea0a176ee78ede9a54e142d555964b252cf82b4a9" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-apple-darwin.tar.gz)
    elseif (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
        set(SHA256_LIBDATADOG "d2a30a155553f5baf334c93e9280f161289697981fef20784f9c6317d061fc1b" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "4cac537ac7895fe2e640b4d2f70ce96d543e411dabe06492cc31fa2a2d564824" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-aarch64-unknown-linux-gnu.tar.gz)
    endif()
else()
    if (CMAKE_SYSTEM_NAME STREQUAL "Darwin")
        set(SHA256_LIBDATADOG "9cf64e45d03a6eac3d1406b0c28bdeb401ee8843d26bd7265cb48c98ec611c39" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-x86_64-apple-darwin.tar.gz)
    elseif (DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
        set(SHA256_LIBDATADOG "f4c00a4deab7e2a3ad760e9d9a0d8170322c2a167dede4fdfdec6a350a534257" CACHE STRING "libdatadog sha256")
        set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl.tar.gz)
    else()
        set(SHA256_LIBDATADOG "7601fe816c3d90c3b4756481bd9280c4617866c3f5f6cd391f632459fbca9d1b" CACHE STRING "libdatadog sha256")
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
