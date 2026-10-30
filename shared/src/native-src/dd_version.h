#ifndef DD_VERSION_H
#define DD_VERSION_H

// The SDK version for native code and the Windows resource scripts. The build passes only the major,
// minor, patch and revision numbers, as preprocessor definitions derived from version.props: see
// build/DdVersion.props for MSBuild and build/cmake/DdVersion.cmake for CMake. Everything else is
// derived here, so adding a new derived value only requires changing this file.
//
// DD_VERSION_REVISION is 0 for releases and 1 for prereleases, so that a prerelease and the release with
// the same major.minor.patch have different four-part versions.
//
// #define rather than constexpr, and include guards rather than #pragma once, because rc.exe
// preprocesses this file too and supports neither.

#if !defined(DD_VERSION_MAJOR) || !defined(DD_VERSION_MINOR) || !defined(DD_VERSION_PATCH) || !defined(DD_VERSION_REVISION)
#error "The DD_VERSION_* definitions are missing. They come from build/DdVersion.props (MSBuild) or build/cmake/DdVersion.cmake (CMake)."
#endif

// Two levels are needed: # stringifies its argument exactly as written, so DD_STRINGIFY_IMPL(DD_VERSION_MAJOR)
// would produce the macro's name rather than its value. Passing through DD_STRINGIFY first expands the
// macros to their values, so DD_STRINGIFY(DD_VERSION_MAJOR.DD_VERSION_MINOR) produces "1.2".
#define DD_STRINGIFY_IMPL(x) #x
#define DD_STRINGIFY(x) DD_STRINGIFY_IMPL(x)

#define DD_THREE_PART_VERSION_STR DD_STRINGIFY(DD_VERSION_MAJOR.DD_VERSION_MINOR.DD_VERSION_PATCH)
#define DD_FOUR_PART_VERSION_STR DD_STRINGIFY(DD_VERSION_MAJOR.DD_VERSION_MINOR.DD_VERSION_PATCH.DD_VERSION_REVISION)

#endif // DD_VERSION_H
