# portfile.cmake

include(vcpkg_common_functions)

# Define the version and download URL for the prebuilt binaries
set(MYLIB_VERSION ${VERSION})
set(MYLIB_URL "C:\\Users\\gregory.leocadie\\repos\\libdatadog\\myoutaput\\libdatadog-x64-windows.zip")
set(MYLIB_HASH "0")  # Replace with actual SHA512 hash

# Download and extract the prebuilt binaries
#vcpkg_download_distfile(ARCHIVE
#    URLS ${MYLIB_URL}
#    FILENAME "mylib-${MYLIB_VERSION}.zip"
#    SHA512 ${MYLIB_HASH}
#)
# Extract the downloaded archive using vcpkg_extract_source_archive_ex
vcpkg_extract_source_archive_ex(
    OUT_SOURCE_PATH source_path
    ARCHIVE "${MYLIB_URL}"
    NO_REMOVE_ONE_LEVEL
)

message(STATUS "CURRENT_PACKAGES_DIR ${CURRENT_PACKAGES_DIR}")
message(STATUS "CMAKE_CURRENT_LIST_DIR ${CMAKE_CURRENT_LIST_DIR}")

# Move extracted files to appropriate directories
file(COPY "${source_path}/libdatadog-x64-windows/include/" DESTINATION "${CURRENT_PACKAGES_DIR}/include")
if ("${VCPKG_LIBRARY_LINKAGE}" STREQUAL "dynamic")
file(COPY "${source_path}/libdatadog-x64-windows/release/dynamic/datadog_profiling_ffi.dll" DESTINATION "${CURRENT_PACKAGES_DIR}/bin/")
file(COPY "${source_path}/libdatadog-x64-windows/release/dynamic/datadog_profiling_ffi.lib" DESTINATION "${CURRENT_PACKAGES_DIR}/lib")
file(COPY "${source_path}/libdatadog-x64-windows/debug/dynamic/datadog_profiling_ffi.dll" DESTINATION "${CURRENT_PACKAGES_DIR}/debug/bin/")
file(COPY "${source_path}/libdatadog-x64-windows/debug/dynamic/datadog_profiling_ffi.lib" DESTINATION "${CURRENT_PACKAGES_DIR}/debug/lib/")
else()
file(COPY "${source_path}/libdatadog-x64-windows/release/static/datadog_profiling_ffi.lib" DESTINATION "${CURRENT_PACKAGES_DIR}/lib")
file(COPY "${source_path}/libdatadog-x64-windows/debug/static/datadog_profiling_ffi.lib" DESTINATION "${CURRENT_PACKAGES_DIR}/debug/lib/")
file(INSTALL "${source_path}/libdatadog-x64-windows/libdatadog.props" DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}/")
file(INSTALL "${source_path}/libdatadog-x64-windows/usage" DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}/")
endif()

# TODO
# generate usage based on the dynamic/static

if ("${VCPKG_LIBRARY_LINKAGE}" STREQUAL "static")
    file(WRITE "${CURRENT_PACKAGES_DIR}/share/${PORT}/usage" 
	"\nFor your project to link correctly, you will need to add:\n"
	"<Import Project=\"${VCPKG_INSTALLED_DIR}\\${VCPKG_TARGET_TRIPLET}\\share\\libdatadog\\libdatadog.props\"/>\n"
	"into your vcxproj or your Directory.Build.props file\n")
endif()
# example <Import Project="<manifest install dir>\<triplet>\share\libdatadog\libdatadog.props"/>

# Install CMake configuration files
#file(INSTALL "${CMAKE_CURRENT_LIST_DIR}/mylibConfig.cmake" DESTINATION "${CURRENT_PACKAGES_DIR}/debug/share/mylib")
#file(INSTALL "${SOURCE_PATH}/mylibTargets.cmake" DESTINATION "${CURRENT_PACKAGES_DIR}/share/mylib")

# Call vcpkg_cmake_config_fixup to ensure paths are correct in config files
#vcpkg_cmake_config_fixup(PACKAGE_NAME "mylib")

# Optionally, clean up temporary files or directories if needed
