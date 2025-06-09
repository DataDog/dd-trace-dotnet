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
                URL https://binaries-ddbuild-io-prod.s3.us-east-1.amazonaws.com/libddprof-build/libdatadog_67339337_8ee422a2_aarch64-alpine-linux-musl.tar.gz?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=ASIAXCNO3BXE4VAJXTFS%2F20250609%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-Date=20250609T223817Z&X-Amz-Expires=3600&X-Amz-SignedHeaders=host&X-Amz-Security-Token=IQoJb3JpZ2luX2VjENf%2F%2F%2F%2F%2F%2F%2F%2F%2F%2FwEaCXVzLWVhc3QtMSJGMEQCIEqd%2BsHYviTxAtIGaKMsTnLJPDO9JHvlPoF9kVaysdmHAiAWEmsXXfpZt1fV8rR6wTy8kvpr1FPaHA4GXQgkMKWhVyqdAwiw%2F%2F%2F%2F%2F%2F%2F%2F%2F%2F8BEAQaDDQ4NjIzNDg1MjgwOSIMzkeJzF5IQLAEW7jXKvEC12ySc%2ByxHmO4an3xQOYY3RcJUtKr4yVbGCrbOfSjCvMvGHuQOy9%2BVHGiP0LymzAB7jS9cSHCHpWKKBo494Uv%2B%2B2%2B5zdMWI0SOVtB5xMjN%2Bsl7mrX%2FsMKE%2FDLcZRUyvZ5NTtBPpYvf43PYZ8D2%2FjWsTQ9CTCkquwGwwr5xDGH4ycKRoHkgSNYvRmjUkuWch%2BP6xpfvObhVbk3%2Bohsi%2BIYCLcN34vfkHFYpe45JJuioKCANitA%2FUlQ3PunF1%2BhwNGX5g1G5blIPat0JoKpZhix05lGrkdr6uCl1KLUjoTj%2FqtaK18C67WjnQBWJwx3h4VGhKO7ANotfg8v1GEbzaFRfSr2Tn87g46O7RNL26l8Lekvl5d9%2BkKtcUx1ArWtcxyMD6zsqu73T%2F6k8Nue4ISIkoT%2Bi3bX%2BY52v6%2FV6efZolSFsb0tQ1HfhPs%2BUkT54MIwgqizMSXProbAaJ%2FyT3b8IjAFdjoF%2FN8xda4W52cbfiIdMIrEncIGOqcB5YM6ETBH8LFIwcvk8bUPVvS6658mNFq1pKMCfSLEBwHplZNDPC6Q8cIFET72D%2F9ZCaiKF%2F2QCy5QUqzOadXdHXxgqGS7CvZ5czlvfFNmmDcmc2kQw5a16e42zLNFnLkRW0921wpmE5ylRDEevbSy95NIzInqCmR%2BY6MbAf4tsNBMJcnCus27KGvH3GNgK803pc9uxRWd%2FzgN563pWLp3aVIO9j3nUJU%3D&X-Amz-Signature=14387d5c359281a3194fb190964880ab2b2272fdd3b6e824b1cf9d3fe2e5e6a4
                SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install
                PATCH_COMMAND ${CMAKE_COMMAND} -E copy_directory <SOURCE_DIR>/aarch64-alpine-linux-musl <SOURCE_DIR> && ${CMAKE_COMMAND} -E remove_directory <SOURCE_DIR>/aarch64-alpine-linux-musl
            )
            set(FILE_TO_DOWNLOAD aarch64-alpine-linux-musl)
        else()
            FetchContent_Declare(libdatadog-install
                URL https://binaries-ddbuild-io-prod.s3.us-east-1.amazonaws.com/libddprof-build/libdatadog_67339337_8ee422a2_aarch64-unknown-linux-gnu.tar.gz?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=ASIAXCNO3BXE4VAJXTFS%2F20250609%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-Date=20250609T223842Z&X-Amz-Expires=3600&X-Amz-SignedHeaders=host&X-Amz-Security-Token=IQoJb3JpZ2luX2VjENf%2F%2F%2F%2F%2F%2F%2F%2F%2F%2FwEaCXVzLWVhc3QtMSJGMEQCIEqd%2BsHYviTxAtIGaKMsTnLJPDO9JHvlPoF9kVaysdmHAiAWEmsXXfpZt1fV8rR6wTy8kvpr1FPaHA4GXQgkMKWhVyqdAwiw%2F%2F%2F%2F%2F%2F%2F%2F%2F%2F8BEAQaDDQ4NjIzNDg1MjgwOSIMzkeJzF5IQLAEW7jXKvEC12ySc%2ByxHmO4an3xQOYY3RcJUtKr4yVbGCrbOfSjCvMvGHuQOy9%2BVHGiP0LymzAB7jS9cSHCHpWKKBo494Uv%2B%2B2%2B5zdMWI0SOVtB5xMjN%2Bsl7mrX%2FsMKE%2FDLcZRUyvZ5NTtBPpYvf43PYZ8D2%2FjWsTQ9CTCkquwGwwr5xDGH4ycKRoHkgSNYvRmjUkuWch%2BP6xpfvObhVbk3%2Bohsi%2BIYCLcN34vfkHFYpe45JJuioKCANitA%2FUlQ3PunF1%2BhwNGX5g1G5blIPat0JoKpZhix05lGrkdr6uCl1KLUjoTj%2FqtaK18C67WjnQBWJwx3h4VGhKO7ANotfg8v1GEbzaFRfSr2Tn87g46O7RNL26l8Lekvl5d9%2BkKtcUx1ArWtcxyMD6zsqu73T%2F6k8Nue4ISIkoT%2Bi3bX%2BY52v6%2FV6efZolSFsb0tQ1HfhPs%2BUkT54MIwgqizMSXProbAaJ%2FyT3b8IjAFdjoF%2FN8xda4W52cbfiIdMIrEncIGOqcB5YM6ETBH8LFIwcvk8bUPVvS6658mNFq1pKMCfSLEBwHplZNDPC6Q8cIFET72D%2F9ZCaiKF%2F2QCy5QUqzOadXdHXxgqGS7CvZ5czlvfFNmmDcmc2kQw5a16e42zLNFnLkRW0921wpmE5ylRDEevbSy95NIzInqCmR%2BY6MbAf4tsNBMJcnCus27KGvH3GNgK803pc9uxRWd%2FzgN563pWLp3aVIO9j3nUJU%3D&X-Amz-Signature=d7c2d1876217e2d818d101af49fb5bcd0d79f503af49e62aaaecffb7a7bd3185
                SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install
                PATCH_COMMAND ${CMAKE_COMMAND} -E copy_directory <SOURCE_DIR>/aarch64-unknown-linux-gnu <SOURCE_DIR> && ${CMAKE_COMMAND} -E remove_directory <SOURCE_DIR>/aarch64-unknown-linux-gnu
            )
            set(FILE_TO_DOWNLOAD aarch64-unknown-linux-gnu)
        endif()
    else()
        if(DEFINED ENV{IsAlpine} AND "$ENV{IsAlpine}" MATCHES "true")
            FetchContent_Declare(libdatadog-install
                URL https://binaries-ddbuild-io-prod.s3.us-east-1.amazonaws.com/libddprof-build/libdatadog_67339337_8ee422a2_x86_64-alpine-linux-musl.tar.gz?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=ASIAXCNO3BXE6RQGGRKR%2F20250609%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-Date=20250609T220153Z&X-Amz-Expires=3600&X-Amz-SignedHeaders=host&X-Amz-Security-Token=IQoJb3JpZ2luX2VjENb%2F%2F%2F%2F%2F%2F%2F%2F%2F%2FwEaCXVzLWVhc3QtMSJIMEYCIQDXXLnQwtCTnqjh5sP7DOQo1HbnEq2cQoCuxecSu8iQ9wIhAOsSds1rCkIpHiL3pTH7zwA0ZFRlZ1%2FWaG7Br6Q2ANHJKp0DCK%2F%2F%2F%2F%2F%2F%2F%2F%2F%2F%2FwEQBBoMNDg2MjM0ODUyODA5IgwklQ6GUpZexfZ4BIkq8QJnAvCEoIVbQaq%2BVLa5ePrIVQ%2FNEDkagWGJUM5ApMvqLEQSHfTtY5vEWWzoOVSHNy8Bg7l9iFAaCpXBUR%2FVaGUvfNIRV8CrozdBYzOKY4ce9M2VvsXrL%2Bcjs2oFy19SkOoKp7SnvMkxYXBjrHLcXsV83YnJygr1MF%2Bldx3C6VJ3MTj4wlYTqKiA4PPngtw8nkbVrr1pK6v1zAI7gQ1Y8IYX4%2F8vpHwW3e6evWYm53oQfYpyEBvPa6UmNXcp6xhy%2BuwItODkXD90gGUyfyTFewdVQ%2FJRwac1%2FKfovUhnv64%2FHh0FwKU%2Fm07q5AmEWyUc9rbYZ2eNkgdnKGAvmRHRUXRqMueBeD20cRz7044Rswc5eg%2FwaJoGZmL%2FiUq1zBi2lwU6zKop9wNN%2FzwcdfoHUIqniBRT6v0PloKJoKLbsJS3MMrBhtdRLjGiJIP0xbBk8k0cBkexmdeX7GTyGh6l81yyma3fmhZhmXRFIM98x0WYjHIwhaadwgY6pQGOn8563Dh7RF2E0Z4ah8JQiMCyOGNBDu7rmgi0nMrgk8mmL61VFCou4XNPiPtAkIYmt3XdtGLyR3QmBWePsRfqMXbo0v6SoeHwuctKX35az5EAF%2FAH55Xm62P5Giw74WLs8cKZZQvAxbL9uWJCkl8VIgfomydZ%2FKKpG%2BS19kOaN4RZEpCKirkqwmwy76NrKte0MTOPyh5iRg9AgstKSNlY5gAvgOM%3D&X-Amz-Signature=73b65bb67b2767e1034d3559b5c49a5c377d093b813805708a80c02ae85c7e94
                SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libdatadog-install
                PATCH_COMMAND ${CMAKE_COMMAND} -E copy_directory <SOURCE_DIR>/x86_64-alpine-linux-musl <SOURCE_DIR> && ${CMAKE_COMMAND} -E remove_directory <SOURCE_DIR>/x86_64-alpine-linux-musl
            )
            set(FILE_TO_DOWNLOAD x86_64-alpine-linux-musl)
        else()
            FetchContent_Declare(libdatadog-install
                URL https://binaries-ddbuild-io-prod.s3.us-east-1.amazonaws.com/libddprof-build/libdatadog_67339337_8ee422a2_x86_64-unknown-linux-gnu.tar.gz?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=ASIAXCNO3BXE4VAJXTFS%2F20250609%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-Date=20250609T223757Z&X-Amz-Expires=3600&X-Amz-SignedHeaders=host&X-Amz-Security-Token=IQoJb3JpZ2luX2VjENf%2F%2F%2F%2F%2F%2F%2F%2F%2F%2FwEaCXVzLWVhc3QtMSJGMEQCIEqd%2BsHYviTxAtIGaKMsTnLJPDO9JHvlPoF9kVaysdmHAiAWEmsXXfpZt1fV8rR6wTy8kvpr1FPaHA4GXQgkMKWhVyqdAwiw%2F%2F%2F%2F%2F%2F%2F%2F%2F%2F8BEAQaDDQ4NjIzNDg1MjgwOSIMzkeJzF5IQLAEW7jXKvEC12ySc%2ByxHmO4an3xQOYY3RcJUtKr4yVbGCrbOfSjCvMvGHuQOy9%2BVHGiP0LymzAB7jS9cSHCHpWKKBo494Uv%2B%2B2%2B5zdMWI0SOVtB5xMjN%2Bsl7mrX%2FsMKE%2FDLcZRUyvZ5NTtBPpYvf43PYZ8D2%2FjWsTQ9CTCkquwGwwr5xDGH4ycKRoHkgSNYvRmjUkuWch%2BP6xpfvObhVbk3%2Bohsi%2BIYCLcN34vfkHFYpe45JJuioKCANitA%2FUlQ3PunF1%2BhwNGX5g1G5blIPat0JoKpZhix05lGrkdr6uCl1KLUjoTj%2FqtaK18C67WjnQBWJwx3h4VGhKO7ANotfg8v1GEbzaFRfSr2Tn87g46O7RNL26l8Lekvl5d9%2BkKtcUx1ArWtcxyMD6zsqu73T%2F6k8Nue4ISIkoT%2Bi3bX%2BY52v6%2FV6efZolSFsb0tQ1HfhPs%2BUkT54MIwgqizMSXProbAaJ%2FyT3b8IjAFdjoF%2FN8xda4W52cbfiIdMIrEncIGOqcB5YM6ETBH8LFIwcvk8bUPVvS6658mNFq1pKMCfSLEBwHplZNDPC6Q8cIFET72D%2F9ZCaiKF%2F2QCy5QUqzOadXdHXxgqGS7CvZ5czlvfFNmmDcmc2kQw5a16e42zLNFnLkRW0921wpmE5ylRDEevbSy95NIzInqCmR%2BY6MbAf4tsNBMJcnCus27KGvH3GNgK803pc9uxRWd%2FzgN563pWLp3aVIO9j3nUJU%3D&X-Amz-Signature=3620ef18f0644c02baf8a29fa115c4b4de13d2f30cdb4f93873dc3fa57666fdf
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
