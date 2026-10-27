# Reads the version from tracer/build/_build/version.props. Sets DD_VERSION (three-part, for
# project(... VERSION ...)) and adds the DD_VERSION_* compile definitions that
# shared/src/native-src/dd_version.h expects - the same set build/DdVersion.props defines for MSBuild.
# Derived values belong in dd_version.h, not here.
#
# Included only by the root CMakeLists.txt, before any add_subdirectory(), so that every subproject
# inherits DD_VERSION and the compile definitions. CMake must therefore be configured from the
# repository root.

set(DD_VERSION_PROPS "${CMAKE_CURRENT_LIST_DIR}/../../tracer/build/_build/version.props")

# Re-run configure whenever version.props changes, so a version bump reaches an existing build directory
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${DD_VERSION_PROPS}")

file(READ "${DD_VERSION_PROPS}" _dd_version_props_content)

if(NOT _dd_version_props_content MATCHES "<DD_VersionPrefix>(([0-9]+)\\.([0-9]+)\\.([0-9]+))</DD_VersionPrefix>")
    message(FATAL_ERROR "Could not parse <DD_VersionPrefix> out of ${DD_VERSION_PROPS}.")
endif()
set(DD_VERSION "${CMAKE_MATCH_1}")
set(_dd_version_major "${CMAKE_MATCH_2}")
set(_dd_version_minor "${CMAKE_MATCH_3}")
set(_dd_version_patch "${CMAKE_MATCH_4}")

# The revision is derived from DD_IsPrerelease, the same way version.props derives DD_VersionRevision:
# 1 when it is 'true' (case-insensitive, as MSBuild compares it), otherwise 0
if(NOT _dd_version_props_content MATCHES "<DD_IsPrerelease>([^<]*)</DD_IsPrerelease>")
    message(FATAL_ERROR "Could not parse <DD_IsPrerelease> out of ${DD_VERSION_PROPS}.")
endif()
string(TOLOWER "${CMAKE_MATCH_1}" _dd_is_prerelease)
if(_dd_is_prerelease STREQUAL "true")
    set(DD_VERSION_REVISION 1)
else()
    set(DD_VERSION_REVISION 0)
endif()

message(STATUS "Datadog SDK version: ${DD_VERSION} (assembly version ${DD_VERSION}.${DD_VERSION_REVISION})")

add_compile_definitions(
    DD_VERSION_MAJOR=${_dd_version_major}
    DD_VERSION_MINOR=${_dd_version_minor}
    DD_VERSION_PATCH=${_dd_version_patch}
    DD_VERSION_REVISION=${DD_VERSION_REVISION})
