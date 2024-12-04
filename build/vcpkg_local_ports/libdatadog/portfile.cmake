set(LIBDATADOG_VERSION ${VERSION})

if(#TARGET_TRIPLET STREQUAL "x64-windows" OR
   TARGET_TRIPLET STREQUAL "x64-windows-static")
    set(PLATFORM "x64")
    set(LIBDATADOG_HASH "bd3061cf7811a3a10601c5f5e4be871c9b806a8afac9343802b47878e997a2596426f030b903dfb6aa6ae0961d41c0ad844295d2eb728ee3552db3cfc924a305")
elseif(TARGET_TRIPLET STREQUAL "x86-windows" OR
       TARGET_TRIPLET STREQUAL "x86-windows-static")
    set(PLATFORM "x86")
    set(LIBDATADOG_HASH "e81eb5881a06186111e03d012b10236a32e7a2d1c2d2c0e4a7a19646e280923d6b0a4b2e6ebb588fa358e3304edf5c8fb0ff37383361f577fbfd9ad75dc27aab")
else()
    message(FATAL_ERROR "Unsupported triplet: ${TARGET_TRIPLET}")
endif()

# Define the version and download URL for the prebuilt binaries
set(LIBDATADOG_FILENAME "libdatadog-${PLATFORM}-windows")
set(LIBDATADOG_ARTIFACT "${LIBDATADOG_FILENAME}.zip")
set(LIBDATADOG_URL "https://github.com/gleocadie/libdatadog/releases/download/v${LIBDATADOG_VERSION}/${LIBDATADOG_ARTIFACT}")

message(STATUS "Download ${LIBDATADOG_URL}")
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
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/release/dynamic/datadog_profiling_ffi.lib" DESTINATION "${CURRENT_PACKAGES_DIR}/lib")
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/debug/dynamic/datadog_profiling_ffi.dll" DESTINATION "${CURRENT_PACKAGES_DIR}/debug/bin")
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/debug/dynamic/datadog_profiling_ffi.lib" DESTINATION "${CURRENT_PACKAGES_DIR}/debug/lib")
else()
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/release/static/datadog_profiling_ffi.lib" DESTINATION "${CURRENT_PACKAGES_DIR}/lib")
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/debug/static/datadog_profiling_ffi.lib" DESTINATION "${CURRENT_PACKAGES_DIR}/debug/lib")
    file(INSTALL "${source_path}/${LIBDATADOG_FILENAME}/libdatadog.props" DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}")
endif()


if ("${VCPKG_LIBRARY_LINKAGE}" STREQUAL "static")
    file(WRITE "${CURRENT_PACKAGES_DIR}/share/${PORT}/usage"
	"\n************************************** SETUP\n"
	"\nFor your project to link correctly, you will need to add:\n"
	"<Import Project=\"vpkd_installed\\${TARGET_TRIPLET}\\${TARGET_TRIPLET}\\share\\libdatadog\\libdatadog.props\"/>\n"
    "\n"
	"into your vcxproj or your Directory.Build.props file\n")
endif()

vcpkg_install_copyright(FILE_LIST "${source_path}/${LIBDATADOG_FILENAME}/LICENSE")