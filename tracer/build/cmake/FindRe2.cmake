include(ExternalProject)

SET(RE2_VERSION "2018-10-01")

ExternalProject_Add(re2
	GIT_REPOSITORY https://github.com/google/re2.git
	GIT_TAG 2018-10-01
	CMAKE_ARGS ARFLAGS=-r\ -s\ -c -DCMAKE_CXX_FLAGS=-O3\ -g\ -fPIC\ -D_GLIBCXX_USE_CXX11_ABI=0
	INSTALL_COMMAND ""
	BUILD_COMMAND make -j
)

add_library(re2-lib STATIC IMPORTED)

set_target_properties(re2-lib PROPERTIES
    INTERFACE_INCLUDE_DIRECTORIES ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2/
    IMPORTED_LOCATION ${CMAKE_CURRENT_BINARY_DIR}/re2-prefix/src/re2-build/libre2.a
)

add_dependencies(re2-lib re2)