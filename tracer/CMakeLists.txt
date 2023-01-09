
add_subdirectory(src/Datadog.Tracer.Native)
enable_testing()
add_subdirectory(test)

add_custom_target(all-tracer)
add_dependencies(all-tracer Datadog.Tracer.Native Datadog.Tracer.Native.Tests)
