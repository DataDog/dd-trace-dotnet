SET(LIBUNWIND_VERSION "1.5")

ExternalProject_Add(libunwind
    GIT_REPOSITORY https://github.com/DataDog/libunwind.git
    GIT_TAG v1.5-stable
    GIT_PROGRESS true
    INSTALL_COMMAND ""
    UPDATE_COMMAND ""
    CONFIGURE_COMMAND ""
    BUILD_COMMAND <SOURCE_DIR>/autogen.sh && <SOURCE_DIR>/configure CXXFLAGS=-fPIC CFLAGS=-fPIC && make
    BUILD_ALWAYS false
)

SET(LIBUNWIND_SOURCE_DIR ${CMAKE_CURRENT_BINARY_DIR}/libunwind-prefix/src/libunwind)
SET(LIBUNWIND_BINARY_DIR ${CMAKE_CURRENT_BINARY_DIR}/libunwind-prefix/src/libunwind-build)

SET(LIBUNWIND_x86_64_PATH ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-x86_64.a CACHE FILEPATH "location of libunwind-x64_64.a")
SET(LIBUNWIND_PATH ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind.a CACHE FILEPATH "location of libunwind.a")

SET(LIBUNWIND_LIBS
    ${LIBUNWIND_x86_64_PATH}
    ${LIBUNWIND_PATH})


SET(LIBUNWIND_INCLUDES
    ${CMAKE_CURRENT_BINARY_DIR}/libunwind-prefix/src/libunwind/include
    ${CMAKE_CURRENT_BINARY_DIR}/libunwind-prefix/src/libunwind-build/include)


add_library(libunwind-lib STATIC IMPORTED)
set_property(TARGET libunwind-lib PROPERTY
             IMPORTED_LOCATION ${LIBUNWIND_PATH})
add_dependencies(libunwind-lib libunwind-build)

add_library(libunwind-x86_64-lib STATIC IMPORTED)
set_property(TARGET libunwind-x86_64-lib PROPERTY
             IMPORTED_LOCATION ${LIBUNWIND_x86_64_PATH})
add_dependencies(libunwind-x86_64-lib libunwind-build)
