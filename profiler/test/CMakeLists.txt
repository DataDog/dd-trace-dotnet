cmake_minimum_required(VERSION 3.13.4)

if (IS_ALPINE)
    add_compile_definitions(-DDD_ALPINE)
endif()

add_subdirectory(Datadog.Profiler.Native.Tests)
add_subdirectory(Datadog.Linux.ApiWrapper.Tests)

add_custom_target(profiler-native-tests)
add_dependencies(profiler-native-tests Datadog.Profiler.Native.Tests)

add_custom_target(wrapper-native-tests)
add_dependencies(wrapper-native-tests Datadog.Linux.ApiWrapper.Tests)
