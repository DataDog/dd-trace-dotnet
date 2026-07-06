// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#define _GNU_SOURCE
#include <dlfcn.h>
#include <fcntl.h>
#include <link.h>
#include <pthread.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <unistd.h>

#include "shared/src/native-src/dd_filesystem.hpp"

namespace WrappedFunctionsTest {
class WrappedFunctionsParametersTests : public ::testing::TestWithParam<void*>
{
};

TEST_P(WrappedFunctionsParametersTests, CheckIfFirstResolvedByDynamicLoader)
{
    auto* fn = GetParam();
    Dl_info info;
    ASSERT_NE(0, dladdr(fn, &info));
    auto filePath = fs::path(info.dli_fname);
    ASSERT_STREQ("Datadog.Linux.ApiWrapper.x64.so", filePath.filename().c_str());
}

INSTANTIATE_TEST_SUITE_P(
    WrappedFunctionsTest,
    WrappedFunctionsParametersTests,
    ::testing::Values(
#ifdef DD_ALPINE
        (void*)::pthread_create,
        (void*)::pthread_attr_init,
        (void*)::pthread_getattr_default_np,
        (void*)::pthread_setattr_default_np,
        // Remove the wrapping around fork because in Universal this cause deadlock on 
        // debian stretch slim
        // In debian stretch slim, it's impossible to install gdb and other tools to
        // investigate the deadlock.
        // Since this wrapping was done for safety but no actual issue, we remove it for now.
        // But we leave the code for documentation or if we need to reactivate it.
        // (void*)::fork,
#endif
        (void*)::dl_iterate_phdr,
        (void*)::dlopen,
        (void*)::dladdr,
        (void*)::accept,
        (void*)::accept4,
        (void*)::recv,
        (void*)::recvfrom,
        (void*)::recvmsg,
        (void*)::recvmmsg,
        (void*)::connect,
        (void*)::send,
        (void*)::sendto,
        (void*)::sendmsg,
        (void*)::open,
        (void*)::openat,
        (void*)::read,
        (void*)::write,
        (void*)::pread,
        (void*)::pwrite,
        // Use dlsym instead of (void*)::symbol because on glibc < 2.33,
        // stat/lstat/fstatat are inline functions (calling __xstat etc.),
        // so (void*)::stat would resolve to a local inline, not our wrapper.
        dlsym(RTLD_DEFAULT, "stat"),
        dlsym(RTLD_DEFAULT, "lstat"),
        dlsym(RTLD_DEFAULT, "fstat"),
        dlsym(RTLD_DEFAULT, "fstatat"),
        dlsym(RTLD_DEFAULT, "__xstat"),
        dlsym(RTLD_DEFAULT, "__lxstat"),
        dlsym(RTLD_DEFAULT, "__fxstat"),
        dlsym(RTLD_DEFAULT, "__fxstatat"),
        // Large-file (*64) variants: the .NET runtime is compiled with
        // _FILE_OFFSET_BITS=64, so it references these symbols rather than the
        // un-suffixed ones. They are exported unconditionally (the universal
        // binary is built on musl but must cover glibc consumers), so they are
        // verified on every platform.
        dlsym(RTLD_DEFAULT, "open64"),
        dlsym(RTLD_DEFAULT, "openat64"),
        dlsym(RTLD_DEFAULT, "pread64"),
        dlsym(RTLD_DEFAULT, "pwrite64"),
        dlsym(RTLD_DEFAULT, "__xstat64"),
        dlsym(RTLD_DEFAULT, "__lxstat64"),
        dlsym(RTLD_DEFAULT, "__fxstat64"),
        dlsym(RTLD_DEFAULT, "__fxstatat64"),
        // Fortified (_FORTIFY_SOURCE) entry points. Applications compiled with
        // -D_FORTIFY_SOURCE bind read/pread/open/openat to these instead of the
        // plain symbols. Exported unconditionally for the same universal-binary
        // reason as the *64 variants above.
        dlsym(RTLD_DEFAULT, "__read_chk"),
        dlsym(RTLD_DEFAULT, "__pread_chk"),
        dlsym(RTLD_DEFAULT, "__pread64_chk"),
        dlsym(RTLD_DEFAULT, "__open_2"),
        dlsym(RTLD_DEFAULT, "__open64_2"),
        dlsym(RTLD_DEFAULT, "__openat_2"),
        dlsym(RTLD_DEFAULT, "__openat64_2")));

} // namespace WrappedFunctionsTest
