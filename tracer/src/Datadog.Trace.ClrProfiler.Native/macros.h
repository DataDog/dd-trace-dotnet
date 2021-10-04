#ifndef DD_CLR_PROFILER_MACROS_H_
#define DD_CLR_PROFILER_MACROS_H_

#include <corhlpr.h>
#include <fstream>

#define RETURN_IF_FAILED(EXPR)                                                                                         \
    do                                                                                                                 \
    {                                                                                                                  \
        hr = (EXPR);                                                                                                   \
        if (FAILED(hr))                                                                                                \
        {                                                                                                              \
            return (hr);                                                                                               \
        }                                                                                                              \
    } while (0)

#define IfFalseRetFAIL(EXPR)                                                                                           \
    do                                                                                                                 \
    {                                                                                                                  \
        if ((EXPR) == false) return E_FAIL;                                                                            \
    } while (0)

#endif // DD_CLR_PROFILER_MACROS_H_
