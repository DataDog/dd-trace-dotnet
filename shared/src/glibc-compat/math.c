// Unless explicitly stated otherwise all files in this repository are
// dual-licensed under the Apache-2.0 License or BSD-3-Clause License.
//
// This product includes software developed at Datadog
// (https://www.datadoghq.com/). Copyright 2023 Datadog, Inc.

#if defined(__linux__)

#ifndef _GNU_SOURCE
#define _GNU_SOURCE
#endif
#include <dlfcn.h>
#include <stdint.h>
#include <stdlib.h>

#if defined(__aarch64__)
// Extracted from https://git.musl-libc.org/cgit/musl/tree/src/math/aarch64/ceilf.c
static float ceilf_local(float x)
{
        __asm__ ("frintp %s0, %s1" : "=w"(x) : "w"(x));
        return x;
}
#else
#if defined(__x86_64__)
static float ceilf_local_sse41(float x)
{
    float result;
    __asm__(
        "roundss $0x0A, %[x], %[result]"
        : [result] "=x" (result)
        : [x] "x" (x)
    );
    return result;
}
#endif
/* fp_force_eval ensures that the input value is computed when that's
   otherwise unused.  To prevent the constant folding of the input
   expression, an additional fp_barrier may be needed or a compilation
   mode that does so (e.g. -frounding-math in gcc). Then it can be
   used to evaluate an expression for its fenv side-effects only.   */

static inline void fp_force_evalf(float x)
{
    volatile float y;
    y = x;
    (void)y;
}

static float ceilf_local(float x)
{
        // Extracted from https://git.musl-libc.org/cgit/musl/tree/src/math/ceilf.c
        union {float f; uint32_t i;} u = {x};
        int e = (int)(u.i >> 23 & 0xff) - 0x7f;
        uint32_t m;

        if (e >= 23) {
            return x;
        }
        if (e >= 0) {
            m = 0x007fffff >> e;
            if ((u.i & m) == 0) {
                return x;
            }
            fp_force_evalf(x + 0x1p120f);
            if (u.i >> 31 == 0){
                u.i += m;
            }
            u.i &= ~m;
        } else {
            fp_force_evalf(x + 0x1p120f);
            if (u.i >> 31) {
                u.f = -0.0;
        } else if (u.i << 1) {
                u.f = 1.0;
            }
        }
        return u.f;
}
#endif

#define unlikely(x)    __builtin_expect(!!(x), 0)

typedef float (*ceilf_t)(float);

__attribute__((weak))
float ceilf(float x)
{
    static ceilf_t ceilf_global_;

    // benign race
    if (unlikely(ceilf_global_ == NULL)) {
        void *ceilf_sym = dlsym(RTLD_DEFAULT, "ceilf");
        if (ceilf_sym == NULL || ceilf_sym == &ceilf) {
#if defined(__x86_64__)
            __builtin_cpu_init();
            if (__builtin_cpu_supports("sse4.1")) {
                ceilf_global_ = &ceilf_local_sse41;
            } else {
                ceilf_global_ = &ceilf_local;
            }
# else
            ceilf_global_ = &ceilf_local;
#endif
        } else {
            ceilf_global_ = (ceilf_t)ceilf_sym;
        }
    }
    return ceilf_global_(x);
}
#endif
