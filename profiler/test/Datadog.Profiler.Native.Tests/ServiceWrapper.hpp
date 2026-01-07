#pragma once
#include "gtest/gtest.h"

#include <memory>

template <typename T>
class ServiceWrapper
{
public:
    template <typename... Args>
    ServiceWrapper(Args&&... args) : _service(std::make_unique<T>(std::forward<Args>(args)...)) {
        //ASSERT_TRUE(_service->Start()) << "Unable to start the service";
        auto success =_service->Start();

        if (!success)
        {
            throw std::runtime_error("Unable to start the service");
        }
    }
    
    ~ServiceWrapper()
    {
        auto success = _service->Stop();
        if (!success)
        {
            throw std::runtime_error("Unable to stop the service");
        }
    }

    T* operator->() const
    {
        return _service.get();
    }

    operator T*() const
    {
        return _service.get();
    }

private:
    std::unique_ptr<T> _service;
};