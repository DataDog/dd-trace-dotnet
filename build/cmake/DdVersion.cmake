# Exposes the version from tracer/build/_build/version.props as DD_VERSION, for project(... VERSION ...),
# which accepts only a numeric major[.minor[.patch[.tweak]]]. Also adds the DD_VERSION_MAJOR,
# DD_VERSION_MINOR and DD_VERSION_PATCH compile definitions that shared/src/native-src/dd_version.h
# expects (build/DdVersion.props defines the same set for the MSBuild builds). dd_version.h derives
# everything else from them, so new derived values belong there, not here.
#
# Deliberately re-read on every include and stored as a normal (not cached) variable, so that a
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
message(STATUS "Datadog SDK version: ${DD_VERSION}")

# Directory-scoped, so they apply to every target in this directory and below. The subproject
# CMakeLists include this file again, so only add them once: normal variables are inherited by
# subdirectories.
if(NOT DD_VERSION_DEFINITIONS_ADDED)
    add_compile_definitions(
        DD_VERSION_MAJOR=${CMAKE_MATCH_2}
        DD_VERSION_MINOR=${CMAKE_MATCH_3}
        DD_VERSION_PATCH=${CMAKE_MATCH_4})
    set(DD_VERSION_DEFINITIONS_ADDED TRUE)
endif()

unset(_dd_version_props_content)
unset(_dd_version_match)
