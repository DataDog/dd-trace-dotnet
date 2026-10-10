cmake_minimum_required(VERSION 3.13.4)

if (IS_ALPINE)
    add_compile_definitions(-DDD_ALPINE)
endif()

add_subdirectory(Datadog.Profiler.Native.Tests)
add_subdirectory(Datadog.Linux.ApiWrapper.Tests)

# Opt-in micro-benchmarks (cmake -DBUILD_PROFILER_BENCHMARKS=ON ...)
option(BUILD_PROFILER_BENCHMARKS "Build the profiler native micro-benchmarks" OFF)
if (BUILD_PROFILER_BENCHMARKS)
    add_subdirectory(Datadog.Profiler.Native.Benchmarks)
endif()

add_custom_target(profiler-native-tests)
add_dependencies(profiler-native-tests Datadog.Profiler.Native.Tests)

add_custom_target(wrapper-native-tests)
add_dependencies(wrapper-native-tests Datadog.Linux.ApiWrapper.Tests)
