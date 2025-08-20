set(LIBDATADOG_VERSION ${VERSION})

if(TARGET_TRIPLET STREQUAL "x64-windows" OR
   TARGET_TRIPLET STREQUAL "x64-windows-static")
    set(PLATFORM "x64")
    set(LIBDATADOG_HASH "f43b4e0a41a4f1d20995e13bc43b7d3502120c2809aefa177f3eea73db1f430e9de350f6f54bdadae09820ad7a492b7f054ec0180d94c33e44fb9a462ab3153a")
elseif(TARGET_TRIPLET STREQUAL "x86-windows" OR
       TARGET_TRIPLET STREQUAL "x86-windows-static")
    set(PLATFORM "x86")
    set(LIBDATADOG_HASH "9cdc780f52edcccf272098f46ee2200e2fa3d56b29be21b5bfc1bcf7943f9a0efd7b7d9e84ca6340e618ec92c4b19d7625d6907592027d4ca67241b08b9063a6")
else()
    message(FATAL_ERROR "Unsupported triplet: ${TARGET_TRIPLET}")
endif()

# Define the version and download URL for the prebuilt binaries
set(LIBDATADOG_FILENAME "libdatadog-${PLATFORM}-windows")
set(LIBDATADOG_ARTIFACT "${LIBDATADOG_FILENAME}.zip")
set(LIBDATADOG_URL "https://github.com/DataDog/libdatadog/releases/download/v${LIBDATADOG_VERSION}/${LIBDATADOG_ARTIFACT}")

# Download and extract the prebuilt binaries
vcpkg_download_distfile(ARCHIVE
    URLS ${LIBDATADOG_URL}
    FILENAME "${LIBDATADOG_ARTIFACT}"
    SHA512 ${LIBDATADOG_HASH}
)

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
