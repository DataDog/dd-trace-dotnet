include(ExternalProject)

SET(FMT_VERSION "5.3.0")


ExternalProject_Add(fmt
	GIT_REPOSITORY https://github.com/DataDog/fmt.git
	GIT_TAG 5.3.0
	CMAKE_ARGS -DCMAKE_POSITION_INDEPENDENT_CODE=TRUE -DFMT_TEST=0 -DFMT_DOC=0 .
	INSTALL_COMMAND ""
	BUILD_COMMAND "make"
)

SET(FMT_LIB ${CMAKE_CURRENT_BINARY_DIR}/fmt-prefix/src/fmt-build/libfmt.a)
SET(FMT_INCLUDE ${CMAKE_CURRENT_BINARY_DIR}/fmt-prefix/src/fmt/include)


add_library(fmt-lib STATIC IMPORTED)

set_property(TARGET fmt-lib PROPERTY
             IMPORTED_LOCATION ${FMT_LIB})

add_dependencies(fmt-lib fmt-build)