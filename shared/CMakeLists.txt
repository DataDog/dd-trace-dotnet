add_subdirectory(src/Datadog.Trace.ClrProfiler.Native)

enable_testing()
add_subdirectory(test/Datadog.Trace.ClrProfiler.Native.Tests)

add_custom_target(all-native-loader)
add_dependencies(all-native-loader Datadog.Trace.ClrProfiler.Native Datadog.Trace.ClrProfiler.Native.Tests)

