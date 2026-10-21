SET(LIBUNWIND_VERSION "v1.8.3-custom-4")

SET(LIBUNWIND_GIT_TAG 1e3629f07c6edbc1a85237dfb4d065248be22b0b)

# The prefix depends on the pin: when GIT_TAG changes, an existing build folder
# fetches and builds the new commit instead of reusing the old checkout.
string(SUBSTRING ${LIBUNWIND_GIT_TAG} 0 12 LIBUNWIND_SHORT_SHA)
SET(LIBUNWIND_PREFIX ${CMAKE_CURRENT_BINARY_DIR}/libunwind-${LIBUNWIND_SHORT_SHA})
SET(LIBUNWIND_BINARY_DIR ${LIBUNWIND_PREFIX}/src/libunwind-build)

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
    PREFIX ${LIBUNWIND_PREFIX}
    GIT_TAG ${LIBUNWIND_GIT_TAG}
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
    ${LIBUNWIND_PREFIX}/src/libunwind-build/include
    ${LIBUNWIND_PREFIX}/src/libunwind/include
)

target_link_libraries(libunwind-lib INTERFACE
    ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-${CMAKE_SYSTEM_PROCESSOR}.a
    ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind.a
    ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-ptrace.a
    ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-dwarf-common.a
    ${LIBUNWIND_BINARY_DIR}/src/.libs/libunwind-dwarf-generic.a
)

add_dependencies(libunwind-lib libunwind)
