# We intentionally pin to the DataDog/libunwind fork on tag
# gleocadie/v1.8.1-custom-3 rather than upstream libunwind v1.8.1 or v1.8.3.
# The fork carries the following patches that the Continuous Profiler's
# HybridUnwinder depends on (arm64 Linux):
#   - unw_cursor_snapshot_t / unw_cursor_snapshot(): a signal-safe public API
#     to inspect the dwarf cursor (CFA, loc_fp/loc_lr/loc_sp, frame_type,
#     cfa_reg_sp, cfa_reg_offset, dwarf_step_ret, step_method, loc_info).
#     Used by UnwinderTracer.h / HybridUnwinder.cpp without having to mirror
#     libunwind's internal layouts.
#   - unw_init_local2() + UNW_INIT_SIGNAL_FRAME flag: lets us initialize the
#     cursor directly from a signal-delivered ucontext_t and flag the first
#     frame as a signal frame so libunwind returns the interrupted PC instead
#     of the signal-trampoline PC.
#   - Additional accessors on the tdep_frame (fp_cfa_offset / lr_cfa_offset /
#     sp_cfa_offset, cfa_is_unreliable, next_to_signal_frame) consumed by the
#     tracer for diagnostics.
# Upstream libunwind 1.8.3 does not expose these APIs. When they land upstream
# (or when the fork is rebased on top of a newer upstream tag), update this
# file accordingly.
SET(LIBUNWIND_VERSION "v1.8.1-custom-3")

SET(LIBUNWIND_BINARY_DIR ${CMAKE_CURRENT_BINARY_DIR}/libunwind-prefix/src/libunwind-build)

ExternalProject_Add(libunwind
    GIT_REPOSITORY https://github.com/DataDog/libunwind.git
    GIT_TAG gleocadie/v1.8.1-custom-3
    GIT_PROGRESS true
    INSTALL_COMMAND ""
    UPDATE_COMMAND ""
    CONFIGURE_COMMAND ""
    BUILD_COMMAND autoreconf -i <SOURCE_DIR> && <SOURCE_DIR>/configure CXXFLAGS=-fPIC\ -D_GLIBCXX_USE_CXX11_ABI=0\ -O3\ -g CFLAGS=-fPIC\ -O3\ -g --disable-minidebuginfo --disable-zlibdebuginfo --disable-tests && make -j$(nproc)
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
