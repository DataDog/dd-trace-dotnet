SET(LIBUNWIND_VERSION "v1.8.3-custom-1")

SET(LIBUNWIND_BINARY_DIR ${CMAKE_CURRENT_BINARY_DIR}/libunwind-prefix/src/libunwind-build)

ExternalProject_Add(libunwind
    GIT_REPOSITORY https://github.com/DataDog/libunwind.git
    GIT_TAG gleocadie/v1.8.3-custom-1
    GIT_PROGRESS true
    INSTALL_COMMAND ""
    UPDATE_COMMAND ""
    CONFIGURE_COMMAND ""
    # CC=/CXX= are threaded through explicitly (matching whatever the outer CMake configure
    # was given via -DCMAKE_C_COMPILER=/-DCMAKE_CXX_COMPILER=) rather than relying on
    # autoconf's default PATH-search for `cc`/`c++`. On a build host whose ambient compiler
    # differs from the rest of the project (e.g. Ubuntu's own system GCC instead of clang),
    # that default resolution would silently build this static lib with a different
    # toolchain than everything it gets linked into - a source of subtle ABI/codegen drift
    # (and, in edge cases, unexpected glibc symbol versions from fortify-source builtins)
    # that's easy to miss since it doesn't fail loudly.
    BUILD_COMMAND autoreconf -i <SOURCE_DIR> && <SOURCE_DIR>/configure CC=${CMAKE_C_COMPILER} CXX=${CMAKE_CXX_COMPILER} CXXFLAGS=-fPIC\ -D_GLIBCXX_USE_CXX11_ABI=0\ -O3\ -g CFLAGS=-fPIC\ -O3\ -g --disable-minidebuginfo --disable-zlibdebuginfo --disable-tests && make -j$(nproc)
    BUILD_ALWAYS false
    BUILD_BYPRODUCTS ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-${CMAKE_SYSTEM_PROCESSOR}.a
                     ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind.a
                     ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-ptrace.a
                     ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-dwarf-common.a
                     ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-dwarf-generic.a
)


add_library(libunwind-lib INTERFACE)

target_include_directories(libunwind-lib INTERFACE
    ${CMAKE_CURRENT_BINARY_DIR}/libunwind-prefix/src/libunwind-build/include
    ${CMAKE_CURRENT_BINARY_DIR}/libunwind-prefix/src/libunwind/include
)

target_link_libraries(libunwind-lib INTERFACE
    ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-${CMAKE_SYSTEM_PROCESSOR}.a
    ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind.a
    ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-ptrace.a
    ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-dwarf-common.a
    ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-dwarf-generic.a
)

add_dependencies(libunwind-lib libunwind)
