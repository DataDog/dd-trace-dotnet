SET(LIBUNWIND_VERSION "v1.8.3-custom-1")

SET(LIBUNWIND_BINARY_DIR ${CMAKE_CURRENT_BINARY_DIR}/libunwind-prefix/src/libunwind-build)

set(LIBUNWIND_CFLAGS "-fPIC -O3 -g")
set(LIBUNWIND_CXXFLAGS "-fPIC -D_GLIBCXX_USE_CXX11_ABI=0 -O3 -g")
set(LIBUNWIND_TOOLCHAIN_CONFIGURE_ARGS)

if (USE_GLIBC217_SYSROOT)
    # Autoconf probes and the subsequent make do not inherit CMake's toolchain
    # settings. Keep compilation and feature detection on the same compiler target,
    # C++ headers, and glibc217 link sysroot as the parent native build.
    set(LIBUNWIND_SYSROOT_FLAG "--sysroot=${CMAKE_SYSROOT}")
    set(LIBUNWIND_CFLAGS "${CMAKE_C_FLAGS} ${LIBUNWIND_SYSROOT_FLAG} --target=${CMAKE_C_COMPILER_TARGET} ${LIBUNWIND_CFLAGS}")
    set(LIBUNWIND_CXXFLAGS "${CMAKE_CXX_FLAGS} ${LIBUNWIND_SYSROOT_FLAG} --target=${CMAKE_CXX_COMPILER_TARGET} ${LIBUNWIND_CXXFLAGS}")
    string(STRIP "${LIBUNWIND_CFLAGS}" LIBUNWIND_CFLAGS)
    string(STRIP "${LIBUNWIND_CXXFLAGS}" LIBUNWIND_CXXFLAGS)
    list(APPEND LIBUNWIND_TOOLCHAIN_CONFIGURE_ARGS
        "LDFLAGS=${CMAKE_EXE_LINKER_FLAGS} ${LIBUNWIND_SYSROOT_FLAG}")
endif()

ExternalProject_Add(libunwind
    GIT_REPOSITORY https://github.com/DataDog/libunwind.git
    GIT_TAG gleocadie/v1.8.3-custom-1
    GIT_PROGRESS false
    INSTALL_COMMAND ""
    UPDATE_COMMAND ""
    CONFIGURE_COMMAND ""
    # Pass CC/CXX explicitly — autoconf's default PATH-search may pick a different compiler.
    BUILD_COMMAND autoreconf -i <SOURCE_DIR> &&
                  <SOURCE_DIR>/configure
                  "CC=${CMAKE_C_COMPILER}"
                  "CXX=${CMAKE_CXX_COMPILER}"
                  "CFLAGS=${LIBUNWIND_CFLAGS}"
                  "CXXFLAGS=${LIBUNWIND_CXXFLAGS}"
                  ${LIBUNWIND_TOOLCHAIN_CONFIGURE_ARGS}
                  --disable-minidebuginfo
                  --disable-zlibdebuginfo
                  --disable-tests &&
                  make -j$(nproc)
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
