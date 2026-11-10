#ifndef DD_VERSION_H
#define DD_VERSION_H

// The SDK version for native code and the Windows resource scripts. The build passes only the major,
// minor and patch numbers and the semver suffix, as preprocessor definitions derived from version.props:
// see build/DdVersion.props for MSBuild and build/cmake/DdVersion.cmake for CMake. Everything else is
// derived here, so adding a new derived value only requires changing this file.
//
// #define rather than constexpr, and include guards rather than #pragma once, because rc.exe
// preprocesses this file too and supports neither.

#if !defined(DD_VERSION_MAJOR) || !defined(DD_VERSION_MINOR) || !defined(DD_VERSION_PATCH) || !defined(DD_VERSION_SUFFIX)
#error "The DD_VERSION_* definitions are missing. They come from build/DdVersion.props (MSBuild) or build/cmake/DdVersion.cmake (CMake)."
#endif

// The native tracer and the managed loader both resolve Datadog.Trace by its exact strong name, so the
// revision stays at zero.
#define DD_VERSION_REVISION 0

// Two levels are needed: # stringifies its argument exactly as written, so DD_STRINGIFY_IMPL(DD_VERSION_MAJOR)
// would produce the macro's name rather than its value. Passing through DD_STRINGIFY first expands the
// macros to their values, so DD_STRINGIFY(DD_VERSION_MAJOR.DD_VERSION_MINOR) produces "1.2".
#define DD_STRINGIFY_IMPL(x) #x
#define DD_STRINGIFY(x) DD_STRINGIFY_IMPL(x)

#define DD_THREE_PART_VERSION_STR DD_STRINGIFY(DD_VERSION_MAJOR.DD_VERSION_MINOR.DD_VERSION_PATCH)
#define DD_FOUR_PART_VERSION_STR DD_STRINGIFY(DD_VERSION_MAJOR.DD_VERSION_MINOR.DD_VERSION_PATCH.DD_VERSION_REVISION)

// The semver, e.g. "1.2.3" or "1.2.3-prerelease": the version the SDK reports everywhere. DD_VERSION_SUFFIX
// is an unquoted token sequence, so it is stringified and concatenated as a separate literal. It is empty for
// releases, which needs care:
//   - Passing an empty argument to a one-parameter macro such as DD_STRINGIFY is "not enough arguments" for
//     MSVC (C4003), so the suffix goes through variadic macros instead, where an empty __VA_ARGS__ is valid.
//   - MSVC's traditional preprocessor stringifies an empty __VA_ARGS__ to nothing rather than "", so
//     DD_VERSION_SUFFIX_STR starts with an empty literal to be a valid string either way.
// Because the suffix is macro-expanded before being stringified, it must not contain identifiers that are
// predefined macros, such as "linux".
//
// Not defined for rc.exe, which doesn't need it and whose preprocessor may not support variadic macros.
#ifndef RC_INVOKED
#define DD_STRINGIFY_VA_IMPL(...) #__VA_ARGS__
#define DD_STRINGIFY_VA(...) "" DD_STRINGIFY_VA_IMPL(__VA_ARGS__)
#define DD_VERSION_SUFFIX_STR DD_STRINGIFY_VA(DD_VERSION_SUFFIX)
#define DD_SEMANTIC_VERSION_STR DD_THREE_PART_VERSION_STR DD_VERSION_SUFFIX_STR
#endif

#endif // DD_VERSION_H
