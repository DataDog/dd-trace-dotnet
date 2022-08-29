// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <type_traits>

#define ENUM_FLAG_OPERATOR(T, X)                                                                               \
    inline T operator X(T lhs, T rhs)                                                                          \
    {                                                                                                          \
        return (T)(static_cast<std::underlying_type_t<T>>(lhs) X static_cast<std::underlying_type_t<T>>(rhs)); \
    }
#define ENUM_FLAGS(T, BT)                                       \
    enum class T : BT;                                          \
    inline T operator~(T t)                                     \
    {                                                           \
        return (T)(~static_cast<std::underlying_type_t<T>>(t)); \
    }                                                           \
    ENUM_FLAG_OPERATOR(T, |)                                    \
    ENUM_FLAG_OPERATOR(T, ^)                                    \
    ENUM_FLAG_OPERATOR(T, &)                                    \
    inline T& operator|=(T& lhs, T rhs)                         \
    {                                                           \
         lhs = lhs | rhs;                                       \
         return lhs;                                            \
    }                                                           \
    enum class T : BT
