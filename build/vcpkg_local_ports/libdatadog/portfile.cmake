set(LIBDATADOG_VERSION ${VERSION})

if(TARGET_TRIPLET STREQUAL "x64-windows" OR
   TARGET_TRIPLET STREQUAL "x64-windows-static")
    set(PLATFORM "x64")
    set(LIBDATADOG_HASH "03a9ba3c4469e58eae122587c0da51d395fcb2de72878286bab63ee460acc1c236d01a72b1bd38fde1312c335c1d4a7d43d6ec03c2cc9dbcdf518e9f656935bd")
elseif(TARGET_TRIPLET STREQUAL "x86-windows" OR
       TARGET_TRIPLET STREQUAL "x86-windows-static")
    set(PLATFORM "x86")
    set(LIBDATADOG_HASH "8a2d2669ffeda8fe4c2c3d9db8254085a64772eedc47cfe243883a6ba4431d936e25c33f17dc9e5096679b9999616c11edc62e2b2451260b23b69da9564eebc4")
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
