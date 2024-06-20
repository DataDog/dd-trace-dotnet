add_subdirectory(src/Datadog.Trace.ClrProfiler.Native)

enable_testing()
add_subdirectory(test/Datadog.Trace.ClrProfiler.Native.Tests)

add_custom_target(native-loader)
add_dependencies(native-loader Datadog.Trace.ClrProfiler.Native)
