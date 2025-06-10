if(CMAKE_VERSION VERSION_GREATER_EQUAL "3.24.0")
    cmake_policy(SET CMP0135 NEW)
endif()

include(FetchContent)

set(LIBDATADOG_VERSION "v18.0.0" CACHE STRING "libdatadog version")

if(CMAKE_SYSTEM_NAME STREQUAL "Darwin")
    # For Darwin, we'll download both architectures and combine them
    set(SHA256_LIBDATADOG_ARM64 "1b63df9650c2d087ec291198616a9bc2237b52ad532244eccbf5923a0662815b" CACHE STRING "libdatadog arm64 sha256")
    set(SHA256_LIBDATADOG_X86_64 "9402b83ecee3a73da8b4bccee1c57a3a8ac6e6d175d50fbee08d458eeda69c16" CACHE STRING "libdatadog x86_64 sha256")
    set(FILE_TO_DOWNLOAD_ARM64 libdatadog-aarch64-apple-darwin.tar.gz)
    set(FILE_TO_DOWNLOAD_X86_64 libdatadog-x86_64-apple-darwin.tar.gz)

    # Download ARM64 version
    FetchContent_Declare(libdatadog-install-arm64
        URL https://github.com/DataDog/libdatadog/releases/download/${LIBDATADOG_VERSION}/${FILE_TO_DOWNLOAD_ARM64}
        URL_HASH SHA256=${SHA256_LIBDATADOG_ARM64}
        SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install-arm64
    )
    if(NOT libdatadog-install-arm64_POPULATED)
        FetchContent_Populate(libdatadog-install-arm64)
    endif()

    # Download x86_64 version
    FetchContent_Declare(libdatadog-install-x86_64
        URL https://github.com/DataDog/libdatadog/releases/download/${LIBDATADOG_VERSION}/${FILE_TO_DOWNLOAD_X86_64}
        URL_HASH SHA256=${SHA256_LIBDATADOG_X86_64}
        SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install-x86_64
    )
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
    if(CMAKE_SYSTEM_PROCESSOR STREQUAL "aarch64" OR CMAKE_SYSTEM_PROCESSOR STREQUAL "arm64")
        if(DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
            FetchContent_Declare(libdatadog-install
                URL https://filebin.net/8wilypkfnvmbyydi/libdatadog_67339337_8ee422a2_aarch64-alpine-linux-musl.tar.gz
                SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install
                PATCH_COMMAND ${CMAKE_COMMAND} -E copy_directory <SOURCE_DIR>/aarch64-alpine-linux-musl <SOURCE_DIR> && ${CMAKE_COMMAND} -E remove_directory <SOURCE_DIR>/aarch64-alpine-linux-musl
            )
            set(FILE_TO_DOWNLOAD aarch64-alpine-linux-musl)
        else()
            FetchContent_Declare(libdatadog-install
                URL https://filebin.net/8wilypkfnvmbyydi/libdatadog_67339337_8ee422a2_aarch64-unknown-linux-gnu.tar.gz
                SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install
                PATCH_COMMAND ${CMAKE_COMMAND} -E copy_directory <SOURCE_DIR>/aarch64-unknown-linux-gnu <SOURCE_DIR> && ${CMAKE_COMMAND} -E remove_directory <SOURCE_DIR>/aarch64-unknown-linux-gnu
            )
            set(FILE_TO_DOWNLOAD aarch64-unknown-linux-gnu)
        endif()
    else()
        if(DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
            FetchContent_Declare(libdatadog-install
                URL https://filebin.net/8wilypkfnvmbyydi/libdatadog_67339337_8ee422a2_x86_64-alpine-linux-musl.tar.gz
                SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install
                PATCH_COMMAND ${CMAKE_COMMAND} -E copy_directory <SOURCE_DIR>/x86_64-alpine-linux-musl <SOURCE_DIR> && ${CMAKE_COMMAND} -E remove_directory <SOURCE_DIR>/x86_64-alpine-linux-musl
            )
            set(FILE_TO_DOWNLOAD x86_64-alpine-linux-musl)
        else()
            FetchContent_Declare(libdatadog-install
                URL https://filebin.net/8wilypkfnvmbyydi/libdatadog_67339337_8ee422a2_x86_64-unknown-linux-gnu.tar.gz
                SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install
                PATCH_COMMAND ${CMAKE_COMMAND} -E copy_directory <SOURCE_DIR>/x86_64-unknown-linux-gnu <SOURCE_DIR> && ${CMAKE_COMMAND} -E remove_directory <SOURCE_DIR>/x86_64-unknown-linux-gnu
            )
            set(FILE_TO_DOWNLOAD x86_64-unknown-linux-gnu)
        endif()
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
