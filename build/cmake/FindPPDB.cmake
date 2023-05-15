
# Sets compiler options
add_compile_options(-std=c++17 -fPIC -fms-extensions -g)
add_compile_options(-DPAL_STDCPP_COMPAT -DPLATFORM_UNIX -DUNICODE)
add_compile_options(-Wno-invalid-noreturn -Wno-macro-redefined)


SET(CMAKE_ARCHIVE_OUTPUT_DIRECTORY PPDB_build)
SET(CMAKE_LIBRARY_OUTPUT_DIRECTORY PPDB_build)
SET(CMAKE_RUNTIME_OUTPUT_DIRECTORY PPDB_build)

add_library(PPDB STATIC
  ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/PPDB/Reader/CoreReader.cpp
  ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/PPDB/Reader/Streams.cpp
  ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/PPDB/Reader/Tables.cpp)

target_include_directories(PPDB
   PUBLIC ${DOTNET_TRACER_REPO_ROOT_PATH}/shared/src/native-lib/PPDB/inc)
