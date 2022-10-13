include(ExternalProject)

SET(FMT_VERSION "5.3.0")

ExternalProject_Add(fmt
    DOWNLOAD_COMMAND git clone --quiet --depth 1 --branch ${FMT_VERSION} --config advice.detachedHead=false https://github.com/DataDog/fmt.git
    TIMEOUT 5
    INSTALL_COMMAND ""
    BUILD_IN_SOURCE TRUE
    CMAKE_ARGS -DCMAKE_POSITION_INDEPENDENT_CODE=TRUE -DFMT_TEST=0 -DFMT_DOC=0
    BUILD_COMMAND ${CMAKE_COMMAND} -E env CXXFLAGS=-D_GLIBCXX_USE_CXX11_ABI=0 make -j
    BUILD_BYPRODUCTS ${CMAKE_CURRENT_BINARY_DIR}/fmt-prefix/src/fmt/libfmt.a
)

ExternalProject_Get_property(fmt SOURCE_DIR)

get_property(FOLDERS_TO_DELETE DIRECTORY PROPERTY ADDITIONAL_MAKE_CLEAN_FILES)
set_property(DIRECTORY PROPERTY ADDITIONAL_MAKE_CLEAN_FILES "${FOLDERS_TO_DELETE};${CMAKE_BINARY_DIR}/fmt-prefix/src/fmt/")

add_library(fmt-lib STATIC IMPORTED)

set_target_properties(fmt-lib PROPERTIES
    INCLUDE_DIRECTORIES ${CMAKE_CURRENT_BINARY_DIR}/fmt-prefix/src/fmt/include
    IMPORTED_LOCATION ${CMAKE_CURRENT_BINARY_DIR}/fmt-prefix/src/fmt/libfmt.a
)

add_dependencies(fmt-lib fmt)