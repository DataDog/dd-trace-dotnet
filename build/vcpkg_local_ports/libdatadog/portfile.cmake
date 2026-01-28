set(LIBDATADOG_VERSION ${VERSION})

if(TARGET_TRIPLET STREQUAL "x64-windows" OR
   TARGET_TRIPLET STREQUAL "x64-windows-static")
    set(PLATFORM "x64")
    # TODO: Update this hash after first libdatadog-dotnet release
    set(LIBDATADOG_HASH "72e24e30f9bf4e047f46038d215f070f8f77613653867024a339c243ef9f47bb7dfdbc4536d957d1deb33181167ef86647447586b31d3404ce8d302b9415bb0c")
elseif(TARGET_TRIPLET STREQUAL "x86-windows" OR
       TARGET_TRIPLET STREQUAL "x86-windows-static")
    set(PLATFORM "x86")
    # TODO: Update this hash after first libdatadog-dotnet release (when x86 support is added)
    set(LIBDATADOG_HASH "03a50287519e48b4fa75ea5d7cd76da5c2083da0d8613bc95707d5f87dfdd86b8a2b6e82092a277513d368c66079b3646dee2f9112dfa142d7a671ccd6bbca79")
else()
    message(FATAL_ERROR "Unsupported triplet: ${TARGET_TRIPLET}")
endif()

# Define the version and download URL for the prebuilt binaries
# Note: Changed to use libdatadog-dotnet repository for custom .NET-specific builds (private repo)
set(LIBDATADOG_FILENAME "libdatadog-${PLATFORM}-windows")
set(LIBDATADOG_ARTIFACT "${LIBDATADOG_FILENAME}.zip")
set(LIBDATADOG_URL "https://github.com/DataDog/libdatadog-dotnet/releases/download/v${LIBDATADOG_VERSION}/${LIBDATADOG_ARTIFACT}")

# Download and extract the prebuilt binaries
# Use HEADERS parameter for authentication (GITHUB_TOKEN in URL is deprecated and returns 404)
if(DEFINED ENV{GITHUB_TOKEN} AND NOT "$ENV{GITHUB_TOKEN}" STREQUAL "")
    message(STATUS "Using authenticated GitHub access for libdatadog-dotnet")
    vcpkg_download_distfile(ARCHIVE
        URLS ${LIBDATADOG_URL}
        FILENAME "${LIBDATADOG_ARTIFACT}"
        SHA512 ${LIBDATADOG_HASH}
        HEADERS "Authorization: token $ENV{GITHUB_TOKEN}"
    )
else()
    message(STATUS "Using unauthenticated GitHub access for libdatadog-dotnet (will fail for private repos)")
    vcpkg_download_distfile(ARCHIVE
        URLS ${LIBDATADOG_URL}
        FILENAME "${LIBDATADOG_ARTIFACT}"
        SHA512 ${LIBDATADOG_HASH}
    )
endif()

# Extract the downloaded archive using vcpkg_extract_source_archive_ex
vcpkg_extract_source_archive_ex(
    OUT_SOURCE_PATH source_path
    ARCHIVE "${ARCHIVE}"
    NO_REMOVE_ONE_LEVEL
)

# Move extracted files to appropriate directories
file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/include/" DESTINATION "${CURRENT_PACKAGES_DIR}/include")

if ("${VCPKG_LIBRARY_LINKAGE}" STREQUAL "dynamic")
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/release/dynamic/datadog_profiling_ffi.dll" DESTINATION "${CURRENT_PACKAGES_DIR}/bin/")
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/release/dynamic/datadog_profiling_ffi.pdb" DESTINATION "${CURRENT_PACKAGES_DIR}/bin/")
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/release/dynamic/datadog_profiling_ffi.lib" DESTINATION "${CURRENT_PACKAGES_DIR}/lib")
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/debug/dynamic/datadog_profiling_ffi.dll" DESTINATION "${CURRENT_PACKAGES_DIR}/debug/bin")
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/debug/dynamic/datadog_profiling_ffi.pdb" DESTINATION "${CURRENT_PACKAGES_DIR}/debug/bin")
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/debug/dynamic/datadog_profiling_ffi.lib" DESTINATION "${CURRENT_PACKAGES_DIR}/debug/lib")
else()
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/release/static/datadog_profiling_ffi.lib" DESTINATION "${CURRENT_PACKAGES_DIR}/lib")
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/debug/static/datadog_profiling_ffi.lib" DESTINATION "${CURRENT_PACKAGES_DIR}/debug/lib")
endif()

vcpkg_install_copyright(FILE_LIST "${source_path}/${LIBDATADOG_FILENAME}/LICENSE")
