include(ExternalProject)

SET(FMT_VERSION "5.3.0")


ExternalProject_Add(fmt
	GIT_REPOSITORY https://github.com/DataDog/fmt.git
	GIT_TAG 5.3.0
	CMAKE_ARGS -DCMAKE_POSITION_INDEPENDENT_CODE=TRUE -DFMT_TEST=0 -DFMT_DOC=0 .
	INSTALL_COMMAND ""
	BUILD_COMMAND make -j
)

add_library(fmt-lib STATIC IMPORTED)

set_target_properties(fmt-lib PROPERTIES
    INCLUDE_DIRECTORIES ${CMAKE_CURRENT_BINARY_DIR}/fmt-prefix/src/fmt/include
    IMPORTED_LOCATION ${CMAKE_CURRENT_BINARY_DIR}/fmt-prefix/src/fmt-build/libfmt.a
)

add_dependencies(fmt-lib fmt)