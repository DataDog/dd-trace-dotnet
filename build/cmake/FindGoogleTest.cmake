include(FetchContent)
FetchContent_Declare(
  googletest
  URL https://github.com/google/googletest/archive/f8d7d77c06936315286eb55f8de22cd23c188571.zip
)
# For Windows: Prevent overriding the parent project's compiler/linker settings
set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)

if(CMAKE_VERSION VERSION_GREATER_EQUAL "3.14.0")
    FetchContent_MakeAvailable(googletest)
else()
    FetchContent_Populate(googletest)
    add_subdirectory(${googletest_SOURCE_DIR} ${googletest_BINARY_DIR})
endif()
