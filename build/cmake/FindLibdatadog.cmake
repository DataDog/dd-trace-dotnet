if(CMAKE_VERSION VERSION_GREATER_EQUAL "3.24.0")
    cmake_policy(SET CMP0135 NEW)
endif()

include(FetchContent)

set(LIBDATADOG_VERSION "v1.0.5" CACHE STRING "libdatadog version")

# Set up authentication header if GITHUB_TOKEN is available (for private repo access)
# Note: Modern GitHub PATs use "Bearer" instead of "token"
if(DEFINED ENV{GITHUB_TOKEN} AND NOT "$ENV{GITHUB_TOKEN}" STREQUAL "")
    message(STATUS "Using authenticated GitHub access for libdatadog-dotnet")
    set(GITHUB_AUTH_HEADER "Authorization: Bearer $ENV{GITHUB_TOKEN}")
else()
    message(STATUS "Using unauthenticated GitHub access for libdatadog-dotnet (will fail for private repos)")
    set(GITHUB_AUTH_HEADER "")
endif()

if(CMAKE_SYSTEM_NAME STREQUAL "Darwin")
    # For Darwin, we'll download both architectures and combine them
    # v1.0.5 SHA256 hashes
    set(SHA256_LIBDATADOG_ARM64 "a82d9da77e0b6db634057655da3ba8133da4ba85d4bc33229296dd8012b699c1" CACHE STRING "libdatadog arm64 sha256")
    set(SHA256_LIBDATADOG_X86_64 "d9e79b9d4dfdcb045feee447afb6df9326a2dd6f0b92c5a405194e3e31acfb86" CACHE STRING "libdatadog x86_64 sha256")
    set(FILE_TO_DOWNLOAD_ARM64 libdatadog-aarch64-apple-darwin.tar.gz)
    set(FILE_TO_DOWNLOAD_X86_64 libdatadog-x86_64-apple-darwin.tar.gz)

    # Download ARM64 version
    if(GITHUB_AUTH_HEADER)
        FetchContent_Declare(libdatadog-install-arm64
            URL https://github.com/DataDog/libdatadog-dotnet/releases/download/${LIBDATADOG_VERSION}/${FILE_TO_DOWNLOAD_ARM64}
            URL_HASH SHA256=${SHA256_LIBDATADOG_ARM64}
            SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install-arm64
            HTTP_HEADER ${GITHUB_AUTH_HEADER}
        )
    else()
        FetchContent_Declare(libdatadog-install-arm64
            URL https://github.com/DataDog/libdatadog-dotnet/releases/download/${LIBDATADOG_VERSION}/${FILE_TO_DOWNLOAD_ARM64}
            URL_HASH SHA256=${SHA256_LIBDATADOG_ARM64}
            SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install-arm64
        )
    endif()
    if(NOT libdatadog-install-arm64_POPULATED)
        FetchContent_Populate(libdatadog-install-arm64)
    endif()

    # Download x86_64 version
    if(GITHUB_AUTH_HEADER)
        FetchContent_Declare(libdatadog-install-x86_64
            URL https://github.com/DataDog/libdatadog-dotnet/releases/download/${LIBDATADOG_VERSION}/${FILE_TO_DOWNLOAD_X86_64}
            URL_HASH SHA256=${SHA256_LIBDATADOG_X86_64}
            SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install-x86_64
            HTTP_HEADER ${GITHUB_AUTH_HEADER}
        )
    else()
        FetchContent_Declare(libdatadog-install-x86_64
            URL https://github.com/DataDog/libdatadog-dotnet/releases/download/${LIBDATADOG_VERSION}/${FILE_TO_DOWNLOAD_X86_64}
            URL_HASH SHA256=${SHA256_LIBDATADOG_X86_64}
            SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install-x86_64
        )
    endif()
    if(NOT libdatadog-install-x86_64_POPULATED)
        FetchContent_Populate(libdatadog-install-x86_64)
    endif()

    set(LIBDATADOG_BASE_DIR_ARM64 ${libdatadog-install-arm64_SOURCE_DIR})
    set(LIBDATADOG_BASE_DIR_X86_64 ${libdatadog-install-x86_64_SOURCE_DIR})
    set(LIBDATADOG_BASE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install)

    # Create universal binary immediately during configuration
    message(STATUS "Creating libdatadog-install directory structure")
    file(MAKE_DIRECTORY ${LIBDATADOG_BASE_DIR})

    message(STATUS "Copying ARM64 structure to libdatadog-install")
    execute_process(
        COMMAND ${CMAKE_COMMAND} -E copy_directory ${LIBDATADOG_BASE_DIR_ARM64} ${LIBDATADOG_BASE_DIR}
    )

    message(STATUS "Creating universal binary with lipo")
    execute_process(
        COMMAND lipo ${LIBDATADOG_BASE_DIR_ARM64}/lib/libdatadog_profiling.dylib ${LIBDATADOG_BASE_DIR_X86_64}/lib/libdatadog_profiling.dylib -create -output ${LIBDATADOG_BASE_DIR}/lib/libdatadog_profiling.dylib
    )

    message(STATUS "Universal binary created at ${LIBDATADOG_BASE_DIR}/lib/libdatadog_profiling.dylib")

    add_library(libdatadog-lib SHARED IMPORTED)

    set_target_properties(libdatadog-lib PROPERTIES
        INTERFACE_INCLUDE_DIRECTORIES ${LIBDATADOG_BASE_DIR}/include
        IMPORTED_LOCATION ${LIBDATADOG_BASE_DIR}/lib/libdatadog_profiling.dylib
    )
else()
    # v1.0.5 SHA256 hashes
    # IMPORTANT: FetchContent uses browser download URLs which won't work for private repos!
    # Consider converting to GitHub API approach like portfile.cmake if builds fail with 404
    if(CMAKE_SYSTEM_PROCESSOR STREQUAL "aarch64" OR CMAKE_SYSTEM_PROCESSOR STREQUAL "arm64")
        if(DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
            set(SHA256_LIBDATADOG "3b8dda11bd0d14d9cd6fdc4783c6e3a34f31b2804e58ebe820bfc913fc82c39b" CACHE STRING "libdatadog sha256")
            set(FILE_TO_DOWNLOAD libdatadog-aarch64-alpine-linux-musl.tar.gz)
        else()
            set(SHA256_LIBDATADOG "d299c4bac5b8ba9353826a440a924c3ee103432b1fd517d5af59e6e7eb5477db" CACHE STRING "libdatadog sha256")
            set(FILE_TO_DOWNLOAD libdatadog-aarch64-unknown-linux-gnu.tar.gz)
        endif()
    else()
        if(DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
            set(SHA256_LIBDATADOG "be94c38d0a4f28bed6be9cf16a2817de4f8912b9fbc73961100bd0840ba4d058" CACHE STRING "libdatadog sha256")
            set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl.tar.gz)
        else()
            set(SHA256_LIBDATADOG "be45cd1ed8613cd2194ec64f74cdbead50cf582b1a91cc90f239da10a7b26872" CACHE STRING "libdatadog sha256")
            set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-unknown-linux-gnu.tar.gz)
        endif()
    endif()

    if(GITHUB_AUTH_HEADER)
        FetchContent_Declare(libdatadog-install
            URL https://github.com/DataDog/libdatadog-dotnet/releases/download/${LIBDATADOG_VERSION}/${FILE_TO_DOWNLOAD}
            URL_HASH SHA256=${SHA256_LIBDATADOG}
            SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install
            HTTP_HEADER ${GITHUB_AUTH_HEADER}
        )
    else()
        FetchContent_Declare(libdatadog-install
            URL https://github.com/DataDog/libdatadog-dotnet/releases/download/${LIBDATADOG_VERSION}/${FILE_TO_DOWNLOAD}
            URL_HASH SHA256=${SHA256_LIBDATADOG}
            SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install
        )
    endif()
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
endif()


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
