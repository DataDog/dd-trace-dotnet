set(LIBDATADOG_VERSION ${VERSION})

if(TARGET_TRIPLET STREQUAL "x64-windows" OR
   TARGET_TRIPLET STREQUAL "x64-windows-static")
    set(PLATFORM "x64")
    set(LIBDATADOG_HASH "ac9020f33f227544f0e66ab40ab854104d7c27459ca02c5e9afb566fc0721e63f36c61f1e9de8f0ba203898b5e75e58b3f3a200e17e23341c41ba6e8744e5c25")
elseif(TARGET_TRIPLET STREQUAL "x86-windows" OR
       TARGET_TRIPLET STREQUAL "x86-windows-static")
    set(PLATFORM "x86")
    set(LIBDATADOG_HASH "f8853437a2e70f003bab4abede8bed5ac0c1e98134b59d7af7f7ef8e466fccb9e070be887fc915850765fc0a1868bed748b6610fbd52791bf06ddf008d0a898c")
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
