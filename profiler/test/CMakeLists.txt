cmake_minimum_required(VERSION 3.13.4)

add_subdirectory(Datadog.Profiler.Native.Tests)
add_subdirectory(Datadog.Linux.ApiWrapper.Tests)

add_custom_target(profiler-native-tests)
add_dependencies(profiler-native-tests Datadog.Profiler.Native.Tests Datadog.Linux.ApiWrapper.Tests)
