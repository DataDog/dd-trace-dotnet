# Reads the single committed version literal out of <repo-root>/version.props and exposes it as
# DD_VERSION, for use in project(... VERSION ${DD_VERSION}).
#
# project() only accepts a numeric major[.minor[.patch[.tweak]]], so this deliberately exposes the
# three-part prefix and nothing else - the dev suffix lives in DD_SEMVER_STR in the generated
# dd_version.g.h header, which the C++ sources include directly.

if(DEFINED DD_VERSION)
    return()
endif()

get_filename_component(DD_REPO_ROOT "${CMAKE_CURRENT_LIST_DIR}/../.." ABSOLUTE)

set(DD_VERSION_PROPS "${DD_REPO_ROOT}/version.props")
if(NOT EXISTS "${DD_VERSION_PROPS}")
    message(FATAL_ERROR "Could not find ${DD_VERSION_PROPS}. This file holds the SDK version number.")
endif()

file(READ "${DD_VERSION_PROPS}" _dd_version_props_content)
string(REGEX MATCH "<DD_VersionPrefix>([0-9]+\\.[0-9]+\\.[0-9]+)</DD_VersionPrefix>"
       _dd_version_match "${_dd_version_props_content}")
if(NOT _dd_version_match)
    message(FATAL_ERROR "Could not parse <DD_VersionPrefix> out of ${DD_VERSION_PROPS}.")
endif()

set(DD_VERSION "${CMAKE_MATCH_1}" CACHE INTERNAL "The three-part Datadog SDK version")
message(STATUS "Datadog SDK version: ${DD_VERSION}")

# The generated header carries the four-part version and the full semver, including any -dev
# suffix. It is produced by the Nuke `GenerateVersionFiles` target; CMake deliberately does not
# re-implement the resolution, so fail loudly rather than silently compiling a stale version in.
set(DD_VERSION_HEADER "${DD_REPO_ROOT}/shared/src/native-src/dd_version.g.h")
if(NOT EXISTS "${DD_VERSION_HEADER}")
    message(FATAL_ERROR
        "${DD_VERSION_HEADER} is missing.\n"
        "Run './tracer/build.sh GenerateVersionFiles' (or build.ps1 on Windows) to generate it.")
endif()

unset(_dd_version_props_content)
unset(_dd_version_match)
