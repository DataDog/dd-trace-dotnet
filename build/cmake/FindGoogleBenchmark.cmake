# Google Benchmark (only used by the opt-in profiler native micro-benchmarks)

include(FetchContent)
FetchContent_Declare(
  googlebenchmark
  URL https://github.com/google/benchmark/archive/refs/tags/v1.9.1.zip
  DOWNLOAD_EXTRACT_TIMESTAMP TRUE
)

set(BENCHMARK_ENABLE_TESTING OFF CACHE BOOL "" FORCE)
set(BENCHMARK_ENABLE_GTEST_TESTS OFF CACHE BOOL "" FORCE)
set(BENCHMARK_ENABLE_INSTALL OFF CACHE BOOL "" FORCE)
set(BENCHMARK_ENABLE_WERROR OFF CACHE BOOL "" FORCE)
set(BENCHMARK_INSTALL_DOCS OFF CACHE BOOL "" FORCE)

FetchContent_GetProperties(googlebenchmark)
if (NOT googlebenchmark_POPULATED)
  FetchContent_Populate(googlebenchmark)
  add_subdirectory(${googlebenchmark_SOURCE_DIR} ${googlebenchmark_BINARY_DIR} EXCLUDE_FROM_ALL)
endif()
