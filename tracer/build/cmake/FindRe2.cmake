include(ExternalProject)

SET(RE2_VERSION "2018-10-01")

ExternalProject_Add(re2
    DOWNLOAD_COMMAND git clone --quiet --depth 1 --branch ${RE2_VERSION} --config advice.detachedHead=false https://github.com/google/re2.git
    TIMEOUT 5
    INSTALL_COMMAND ""
    CMAKE_ARGS -DCMAKE_CXX_FLAGS=-O3\ -g\ -fPIC\ -D_GLIBCXX_USE_CXX11_ABI=0
    BUILD_COMMAND ${CMAKE_COMMAND} -E env "ARFLAGS=-r -s -c" make -j
)

ExternalProject_Get_property(re2 SOURCE_DIR)

get_property(FOLDERS_TO_DELETE DIRECTORY PROPERTY ADDITIONAL_MAKE_CLEAN_FILES)
set_property(DIRECTORY PROPERTY ADDITIONAL_MAKE_CLEAN_FILES "${FOLDERS_TO_DELETE};${SOURCE_DIR}")

add_library(re2-lib STATIC IMPORTED)

set_target_properties(re2-lib PROPERTIES
    INTERFACE_INCLUDE_DIRECTORIES ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/
    IMPORTED_LOCATION ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2-build/libre2.a
)

add_dependencies(re2-lib re2)