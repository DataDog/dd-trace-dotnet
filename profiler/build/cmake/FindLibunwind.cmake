SET(LIBUNWIND_VERSION "1.6.2")

ExternalProject_Add(libunwind
    GIT_REPOSITORY https://github.com/DataDog/libunwind.git
    GIT_TAG v1.6.2
    GIT_PROGRESS true
    INSTALL_COMMAND ""
    UPDATE_COMMAND ""
    CONFIGURE_COMMAND ""
    BUILD_COMMAND autoreconf -i <SOURCE_DIR> && <SOURCE_DIR>/configure CXXFLAGS=-fPIC\ -D_GLIBCXX_USE_CXX11_ABI=0\ -O3 CFLAGS=-fPIC\ -O3 --disable-minidebuginfo && make -j
    BUILD_ALWAYS false
)

SET(LIBUNWIND_BINARY_DIR ${CMAKE_CURRENT_BINARY_DIR}/libunwind-prefix/src/libunwind-build)

add_library(libunwind-lib INTERFACE)

target_include_directories(libunwind-lib INTERFACE
    ${CMAKE_CURRENT_BINARY_DIR}/libunwind-prefix/src/libunwind-build/include
    ${CMAKE_CURRENT_BINARY_DIR}/libunwind-prefix/src/libunwind/include
)

target_link_libraries(libunwind-lib INTERFACE
    ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-${CMAKE_SYSTEM_PROCESSOR}.a
    ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind.a)

add_dependencies(libunwind-lib libunwind)
