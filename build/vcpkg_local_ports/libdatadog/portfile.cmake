set(LIBDATADOG_VERSION ${VERSION})

if(TARGET_TRIPLET STREQUAL "x64-windows" OR
   TARGET_TRIPLET STREQUAL "x64-windows-static")
    set(PLATFORM "x64")
    # v1.0.7 SHA512 hash
    set(LIBDATADOG_HASH "0b65003d36927b0e028ff6ef8f8f28d4436d697f367fe720e0fbc32d1c397926979d14009bf6ce104c5c7f4423bd4ede75a4997b07c0d599c7cfa85954050f22")
elseif(TARGET_TRIPLET STREQUAL "x86-windows" OR
       TARGET_TRIPLET STREQUAL "x86-windows-static")
    set(PLATFORM "x86")
    # v1.0.7 SHA512 hash
    set(LIBDATADOG_HASH "87f41f54aeced8f035cfad282b14c54ecd5191b065346c35fc8b015ea87ad67d231bbc9f35afedd68ec38de0024e58a753c2f0406f8b2f51d19a8d511735626c")
else()
    message(FATAL_ERROR "Unsupported triplet: ${TARGET_TRIPLET}")
endif()

# Define the version and download URL for the prebuilt binaries
# Note: Changed to use libdatadog-dotnet repository for custom .NET-specific builds (private repo)
set(LIBDATADOG_FILENAME "libdatadog-${PLATFORM}-windows")
set(LIBDATADOG_ARTIFACT "${LIBDATADOG_FILENAME}.zip")
set(LIBDATADOG_URL "https://github.com/DataDog/libdatadog-dotnet/releases/download/v${LIBDATADOG_VERSION}/${LIBDATADOG_ARTIFACT}")

# Download and extract the prebuilt binaries
# Use CMake file(DOWNLOAD) with HTTPHEADER for authentication since vcpkg_download_distfile may not support HEADERS
if(DEFINED ENV{GITHUB_TOKEN} AND NOT "$ENV{GITHUB_TOKEN}" STREQUAL "")
    message(STATUS "Using authenticated GitHub access for libdatadog-dotnet (private repo)")

    # Store token in a CMake variable to ensure proper expansion
    set(GITHUB_AUTH_TOKEN "$ENV{GITHUB_TOKEN}")
    string(LENGTH "${GITHUB_AUTH_TOKEN}" TOKEN_LENGTH)
    message(STATUS "Token length: ${TOKEN_LENGTH} characters")

    set(ARCHIVE "${DOWNLOADS}/libdatadog/${LIBDATADOG_ARTIFACT}")
    file(MAKE_DIRECTORY "${DOWNLOADS}/libdatadog")

    # For private GitHub repos, we MUST use the API asset URL, not the browser download URL
    # Browser URL (doesn't work): https://github.com/.../releases/download/...
    # API URL (works): https://api.github.com/repos/.../releases/assets/{asset_id}

    # Step 1: Get release metadata from API to find the asset ID
    set(RELEASE_API_URL "https://api.github.com/repos/DataDog/libdatadog-dotnet/releases/tags/v${LIBDATADOG_VERSION}")
    set(RELEASE_JSON "${DOWNLOADS}/libdatadog/release.json")

    message(STATUS "Fetching release metadata from GitHub API...")
    file(DOWNLOAD
        "${RELEASE_API_URL}"
        "${RELEASE_JSON}"
        HTTPHEADER "Authorization: Bearer ${GITHUB_AUTH_TOKEN}"
        HTTPHEADER "Accept: application/vnd.github+json"
        STATUS api_status
        LOG api_log
    )

    list(GET api_status 0 api_code)
    if(NOT api_code EQUAL 0)
        message(FATAL_ERROR "Failed to fetch release metadata:\nStatus: ${api_code}\nLog: ${api_log}")
    endif()

    # Step 2: Parse JSON to find the asset ID for our file
    file(READ "${RELEASE_JSON}" release_json_content)

    # GitHub API returns assets as array. Looking at the JSON structure:
    # "id": 341003266,
    # "node_id": "...",
    # "name": "libdatadog-x64-windows.zip",

    # First, find the section containing our asset name
    string(FIND "${release_json_content}" "\"name\": \"${LIBDATADOG_ARTIFACT}\"" name_pos)
    if(name_pos EQUAL -1)
        message(FATAL_ERROR "Could not find asset '${LIBDATADOG_ARTIFACT}' in release JSON")
    endif()

    # Extract a substring before the name (to find the id field that comes before it)
    # Go back 500 characters from where we found the name
    math(EXPR start_pos "${name_pos} - 500")
    if(start_pos LESS 0)
        set(start_pos 0)
    endif()

    string(SUBSTRING "${release_json_content}" ${start_pos} 600 asset_section)

    # Now find the "id" field in this section (id comes before name in GitHub JSON)
    string(REGEX MATCH "\"id\": ([0-9]+)" id_match "${asset_section}")
    if(NOT id_match)
        message(FATAL_ERROR "Could not extract asset ID for '${LIBDATADOG_ARTIFACT}'")
    endif()

    set(ASSET_ID "${CMAKE_MATCH_1}")
    message(STATUS "Found asset ID: ${ASSET_ID}")

    # Step 3: Download the asset using the API URL
    set(ASSET_API_URL "https://api.github.com/repos/DataDog/libdatadog-dotnet/releases/assets/${ASSET_ID}")
    message(STATUS "Downloading from API: ${ASSET_API_URL}")

    file(DOWNLOAD
        "${ASSET_API_URL}"
        "${ARCHIVE}"
        HTTPHEADER "Authorization: Bearer ${GITHUB_AUTH_TOKEN}"
        HTTPHEADER "Accept: application/octet-stream"
        SHOW_PROGRESS
        STATUS download_status
        LOG download_log
    )

    list(GET download_status 0 status_code)
    list(GET download_status 1 status_message)
    if(NOT status_code EQUAL 0)
        message(FATAL_ERROR "Download failed:\nStatus: ${status_code}\nMessage: ${status_message}\nLog:\n${download_log}")
    endif()

    # Verify the hash
    file(SHA512 "${ARCHIVE}" downloaded_hash)
    if(NOT "${downloaded_hash}" STREQUAL "${LIBDATADOG_HASH}")
        message(FATAL_ERROR "Hash mismatch!\nExpected: ${LIBDATADOG_HASH}\nActual:   ${downloaded_hash}")
    endif()

    message(STATUS "Download successful, hash verified")
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
