include(CMakeFindDependencyMacro)

find_dependency(another_dependency)

# Specify include directories and libraries
set(MY_LIB_INCLUDE_DIRS "${CMAKE_CURRENT_LIST_DIR}/../include")
set(MY_LIB_LIBRARIES "${CMAKE_CURRENT_LIST_DIR}/../lib/libdatadog_profiling.a")

# Create a target for consumers to link against
add_library(libdatado INTERFACE IMPORTED)
target_include_directories(libdatadog INTERFACE ${MY_LIB_INCLUDE_DIRS})
target_link_libraries(libdatadog INTERFACE ${MY_LIB_LIBRARIES})
