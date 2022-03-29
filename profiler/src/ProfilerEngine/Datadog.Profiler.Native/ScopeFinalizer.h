// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <functional>

template <class T>
struct FinalizerInvocationWrapper
{
public:
    explicit FinalizerInvocationWrapper(T&& finalizerCallback) :
        _finalizerCallback{std::move(finalizerCallback)},
        _isEnabled{true}
    {
    }

    ~FinalizerInvocationWrapper()
    {
        Invoke();
    }

    // make the object not moveable
    FinalizerInvocationWrapper(FinalizerInvocationWrapper&&) = delete;
    FinalizerInvocationWrapper& operator=(FinalizerInvocationWrapper&&) = delete;

    // make the object not copyable
    FinalizerInvocationWrapper(const FinalizerInvocationWrapper&) = delete;
    FinalizerInvocationWrapper& operator=(const FinalizerInvocationWrapper&) = delete;

    bool IsEnabled() const
    {
        return _isEnabled;
    }
    void SetEnabled(bool value)
    {
        _isEnabled = value;
    }

    bool Invoke()
    {
        bool isEnabled = _isEnabled;
        auto& finalizerCallback = _finalizerCallback;

        if (!isEnabled)
        {
            return false;
        }

        _isEnabled = false;
        finalizerCallback();
        return true;
    }

private:
    const T _finalizerCallback;
    bool _isEnabled;
};

template <class T>
inline FinalizerInvocationWrapper<T> CreateScopeFinalizer(T&& finalizerCallback)
{
    return FinalizerInvocationWrapper(std::move(finalizerCallback));
}
