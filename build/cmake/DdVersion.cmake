# Exposes the version from tracer/build/_build/version.props as DD_VERSION, for project(... VERSION ...),
# which accepts only a numeric major[.minor[.patch[.tweak]]]. Also adds the DD_VERSION_MAJOR,
# DD_VERSION_MINOR, DD_VERSION_PATCH and DD_VERSION_SUFFIX compile definitions that
# shared/src/native-src/dd_version.h expects (build/DdVersion.props defines the same set for the MSBuild
# builds). dd_version.h derives everything else from them, so new derived values belong there, not here.
#
# Included only by the root CMakeLists.txt, before any add_subdirectory(), so that every subproject
# inherits DD_VERSION and the compile definitions. CMake must therefore be configured from the
# repository root.
#
# Deliberately re-read on every configure and stored as a normal (not cached) variable, so that a
# version bump is picked up by an existing build directory, and you don't get weird instrumentation
# mismatches locally, just after a release.

get_filename_component(DD_REPO_ROOT "${CMAKE_CURRENT_LIST_DIR}/../.." ABSOLUTE)

set(DD_VERSION_PROPS "${DD_REPO_ROOT}/tracer/build/_build/version.props")
if(NOT EXISTS "${DD_VERSION_PROPS}")
    message(FATAL_ERROR "Could not find ${DD_VERSION_PROPS}. This file holds the SDK version number.")
endif()

# Re-run configure whenever version.props changes
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${DD_VERSION_PROPS}")

file(READ "${DD_VERSION_PROPS}" _dd_version_props_content)
string(REGEX MATCH "<DD_VersionPrefix>(([0-9]+)\.([0-9]+)\.([0-9]+))</DD_VersionPrefix>"
       _dd_version_match "${_dd_version_props_content}")
if(NOT _dd_version_match)
    message(FATAL_ERROR "Could not parse <DD_VersionPrefix> out of ${DD_VERSION_PROPS}.")
endif()

set(DD_VERSION "${CMAKE_MATCH_1}")
set(_dd_version_major "${CMAKE_MATCH_2}")
set(_dd_version_minor "${CMAKE_MATCH_3}")
set(_dd_version_patch "${CMAKE_MATCH_4}")

# The semver suffix (e.g. -prerelease), empty for a release
string(REGEX MATCH "<DD_VersionSuffix>([^<]*)</DD_VersionSuffix>"
       _dd_suffix_match "${_dd_version_props_content}")
if(NOT _dd_suffix_match)
    message(FATAL_ERROR "Could not parse <DD_VersionSuffix> out of ${DD_VERSION_PROPS}.")
endif()
set(_dd_version_suffix "${CMAKE_MATCH_1}")

# The same rule UpdateVersion enforces, re-checked here because the suffix reaches C++ as an unquoted
# token, so a hand-edited value with spaces or quotes would otherwise produce a confusing build error
if(NOT _dd_version_suffix MATCHES "^(-[0-9A-Za-z-]+(\\.[0-9A-Za-z-]+)*)?$")
    message(FATAL_ERROR "<DD_VersionSuffix> in ${DD_VERSION_PROPS} must be empty or a semver prerelease suffix such as -prerelease, but was '${_dd_version_suffix}'.")
endif()

message(STATUS "Datadog SDK version: ${DD_VERSION}${_dd_version_suffix}")

# Directory-scoped, so they apply to every target in this directory and below.
# DD_VERSION_SUFFIX is an unquoted token (possibly empty), which dd_version.h stringifies.
add_compile_definitions(
    DD_VERSION_MAJOR=${_dd_version_major}
    DD_VERSION_MINOR=${_dd_version_minor}
    DD_VERSION_PATCH=${_dd_version_patch}
    DD_VERSION_SUFFIX=${_dd_version_suffix})

unset(_dd_version_props_content)
unset(_dd_version_match)
unset(_dd_version_major)
unset(_dd_version_minor)
unset(_dd_version_patch)
unset(_dd_suffix_match)
unset(_dd_version_suffix)
