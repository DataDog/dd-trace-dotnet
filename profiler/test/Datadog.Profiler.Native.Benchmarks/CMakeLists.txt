# Micro-benchmarks for the profiler native code (Linux only, opt-in).
#   cmake -DBUILD_PROFILER_BENCHMARKS=ON -DCMAKE_BUILD_TYPE=Release ...
#   cmake --build <dir> --target Datadog.Profiler.Native.Benchmarks

find_package(GoogleBenchmark REQUIRED)

set(CMAKE_CXX_STANDARD 20)

add_compile_options(-fPIC -fms-extensions)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined -DDD_TEST)

if(ISLINUX)
    add_compile_options(-stdlib=libstdc++ -DLINUX -Wno-pragmas)
endif()

if (BIT64)
    add_compile_options(-DBIT64 -DHOST_64BIT)
endif()

if (ISAMD64)
    add_compile_options(-DAMD64)
elseif (ISARM64)
    add_compile_options(-DARM64)
endif()

SET(BENCHMARK_EXECUTABLE_NAME "Datadog.Profiler.Native.Benchmarks")
SET(BENCHMARK_OUTPUT_DIR ${OUTPUT_BUILD_DIR}/bin/${BENCHMARK_EXECUTABLE_NAME})
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${BENCHMARK_OUTPUT_DIR})

FILE(GLOB PROFILER_NATIVE_BENCHMARK_SRC CONFIGURE_DEPENDS "*.cpp")

add_executable(${BENCHMARK_EXECUTABLE_NAME}
    ${PROFILER_NATIVE_BENCHMARK_SRC}
)

target_include_directories(${BENCHMARK_EXECUTABLE_NAME}
    PUBLIC ../../src/ProfilerEngine/Datadog.Profiler.Native
)

target_link_libraries(${BENCHMARK_EXECUTABLE_NAME}
    Datadog.Profiler.Native.static
    benchmark::benchmark
    -static-libgcc
    -static-libstdc++
    -lstdc++fs
)
