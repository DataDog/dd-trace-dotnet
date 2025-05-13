// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2024 Datadog, Inc.

#include "AutoResetEvent.h"

#include <pthread.h>
#include <time.h>
#include <cassert>
#include "Log.h"

using namespace std::chrono_literals;

struct AutoResetEvent::EventImpl
{
public:
    pthread_mutex_t _mutex;
    pthread_cond_t _cond;
    bool _isSet;
};

AutoResetEvent::AutoResetEvent(bool initialValue)
 : _impl{std::make_unique<EventImpl>()}
{
    pthread_mutex_init(&_impl->_mutex, 0);
    pthread_cond_init(&_impl->_cond, 0);
    _impl->_isSet = initialValue;
}

AutoResetEvent::~AutoResetEvent()
{
    pthread_mutex_destroy(&_impl->_mutex);
    pthread_cond_destroy(&_impl->_cond);
    _impl->_isSet = false;
}

void AutoResetEvent::Set()
{
    pthread_mutex_lock(&_impl->_mutex);
    _impl->_isSet = true;
    pthread_cond_signal(&_impl->_cond);
    pthread_mutex_unlock(&_impl->_mutex);
}

bool AutoResetEvent::Wait(std::chrono::milliseconds timeout)
{
    // in the timeout=0ms case, we still go through the whole code
    // and pthread_cond_timedwait will return immediatly
    // And we will return the value from _isSet
    pthread_mutex_lock(&_impl->_mutex);
 
    bool isSignaled = true;

    struct timespec ts;
    if (timeout >= 0ms)
    {
        clock_gettime(CLOCK_REALTIME, &ts);
        ts.tv_nsec += timeout.count() % 1000 * 1'000'000;
        ts.tv_sec += timeout.count() / 1000;
    }

    while(!_impl->_isSet)
    {
        if (timeout >= 0ms)
        {
            auto res = pthread_cond_timedwait(&_impl->_cond, &_impl->_mutex, &ts);
            if (res == ETIMEDOUT)
            {
                // We have a race when the timeout occurs but the lock was taken
                // before pthread_cond_timedwait acquires the lock on returned.
                isSignaled = _impl->_isSet;
                break;
            }
        }
        else
        {
            pthread_cond_wait(&_impl->_cond, &_impl->_mutex);
        }
    }

    _impl->_isSet = false;
    pthread_mutex_unlock(&_impl->_mutex);

    return isSignaled;
}
