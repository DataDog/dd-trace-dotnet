// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ServiceBase.h"

#include "Log.h"

static std::string to_string(ServiceBase::State state)
{
    switch (state)
    {
        case ServiceBase::State::Init:
            return "Init";
        case ServiceBase::State::Started:
            return "Started";
        case ServiceBase::State::Starting:
            return "Starting";
        case ServiceBase::State::Stopping:
            return "Stopping";
        case ServiceBase::State::Stopped:
            return "Stopped";
    }

    return "Unknown state";
}

ServiceBase::ServiceBase() :
    _currentState{State::Init}
{
}

bool ServiceBase::Start()
{
    auto expected = State::Init;
    auto exchange = _currentState.compare_exchange_strong(expected, State::Starting);

    if (!exchange)
    {
        Log::Debug("Unable to start the service '", GetName(), "'. Current state : ", to_string(_currentState.load()));
        return false;
    }

    auto result = StartImpl();
    if (result)
    {
        _currentState = State::Started;
    }
    else
    {
        _currentState = State::Init;
        Log::Debug("Unable to start the service '", GetName(), "'. Call to StartImpl failed. Current state : ", to_string(_currentState.load()));
    }

    return result;
}

bool ServiceBase::Stop()
{
    auto expected = State::Started;
    auto exchange = _currentState.compare_exchange_strong(expected, State::Stopping);

    if (!exchange)
    {
        Log::Debug("Unable to stop the service '", GetName(), "'.Current state : ", to_string(_currentState.load()));
        return false;
    }

    auto result = StopImpl();
    if (result)
    {
        _currentState = State::Stopped;
    }
    else
    {
        _currentState = State::Started;
        Log::Debug("Unable to stop the service '", GetName(), "'. Call to StartImpl failed.Current state : ", to_string(_currentState.load()));
    }

    return result;
}

ServiceBase::State ServiceBase::GetState() const
{
    return _currentState;
}
