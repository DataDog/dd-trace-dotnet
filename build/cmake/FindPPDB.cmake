add_library(PPDB STATIC
  ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/PPDB/Reader/CoreReader.cpp
  ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/PPDB/Reader/Streams.cpp
  ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/PPDB/Reader/Tables.cpp)

target_compile_options(PPDB PRIVATE -fPIC -fms-extensions -g)
target_compile_definitions(PPDB PRIVATE PAL_STDCPP_COMPAT PLATFORM_UNIX UNICODE)
target_compile_options(PPDB PRIVATE -Wno-invalid-noreturn -Wno-macro-redefined)

set_target_properties(PPDB PROPERTIES
    ARCHIVE_OUTPUT_DIRECTORY PPDB_build
    LIBRARY_OUTPUT_DIRECTORY PPDB_build
    RUNTIME_OUTPUT_DIRECTORY PPDB_build
)

target_include_directories(PPDB
   PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/PPDB/inc)
