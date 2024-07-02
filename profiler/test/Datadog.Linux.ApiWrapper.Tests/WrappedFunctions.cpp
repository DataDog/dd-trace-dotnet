// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#define _GNU_SOURCE
#include <dlfcn.h>
#include <link.h>
#include <pthread.h>
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
        (void*)::sendmsg));

} // namespace WrappedFunctionsTest
