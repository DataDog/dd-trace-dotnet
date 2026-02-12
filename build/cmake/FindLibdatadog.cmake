if(CMAKE_VERSION VERSION_GREATER_EQUAL "3.24.0")
    cmake_policy(SET CMP0135 NEW)
endif()

include(FetchContent)

set(LIBDATADOG_VERSION "v1.2.5" CACHE STRING "libdatadog version")

if(CMAKE_SYSTEM_NAME STREQUAL "Darwin")
    # For Darwin, we'll download both architectures and combine them
    set(SHA256_LIBDATADOG_ARM64 "9e4599e7f2e86ee6394703b740a733b07a2d2c983554e388636a5840d294164e" CACHE STRING "libdatadog arm64 sha256")
    set(SHA256_LIBDATADOG_X86_64 "3898bb2de484a3c3f33cd97a077591001b6c63e4292b2edd11049f5b323949c8" CACHE STRING "libdatadog x86_64 sha256")
    set(FILE_TO_DOWNLOAD_ARM64 libdatadog-aarch64-apple-darwin.tar.gz)
    set(FILE_TO_DOWNLOAD_X86_64 libdatadog-x86_64-apple-darwin.tar.gz)

    # Download ARM64 version
    FetchContent_Declare(libdatadog-install-arm64
        URL https://github.com/DataDog/libdatadog-dotnet/releases/download/${LIBDATADOG_VERSION}/${FILE_TO_DOWNLOAD_ARM64}
        URL_HASH SHA256=${SHA256_LIBDATADOG_ARM64}
        SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install-arm64
    )
    if(NOT libdatadog-install-arm64_POPULATED)
        FetchContent_Populate(libdatadog-install-arm64)
    endif()

    # Download x86_64 version
    FetchContent_Declare(libdatadog-install-x86_64
        URL https://github.com/DataDog/libdatadog-dotnet/releases/download/${LIBDATADOG_VERSION}/${FILE_TO_DOWNLOAD_X86_64}
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
            set(SHA256_LIBDATADOG "2ee9c217c80be0a43e143a469c20bf4ffa8206b5dc4a360314c7adfc3a4a103c" CACHE STRING "libdatadog sha256")
            set(FILE_TO_DOWNLOAD libdatadog-aarch64-alpine-linux-musl.tar.gz)
        else()
            set(SHA256_LIBDATADOG "08bb10545e3e9a28c96ad680e707e948a38faa50ae401e55f18a671276a06ec8" CACHE STRING "libdatadog sha256")
            set(FILE_TO_DOWNLOAD libdatadog-aarch64-unknown-linux-gnu.tar.gz)
        endif()
    else()
        if(DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
            set(SHA256_LIBDATADOG "80e68ee4115ead704ac839a0f5df0309bdec447c5e822851256169c5f039c4e4" CACHE STRING "libdatadog sha256")
            set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-alpine-linux-musl.tar.gz)
        else()
            set(SHA256_LIBDATADOG "069a96a08f1a010ed02406f1b497e1a4e792756c99123d89057983257dd40d73" CACHE STRING "libdatadog sha256")
            set(FILE_TO_DOWNLOAD libdatadog-${CMAKE_SYSTEM_PROCESSOR}-unknown-linux-gnu.tar.gz)
        endif()
    endif()

    FetchContent_Declare(libdatadog-install
        URL https://github.com/DataDog/libdatadog-dotnet/releases/download/${LIBDATADOG_VERSION}/${FILE_TO_DOWNLOAD}
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
