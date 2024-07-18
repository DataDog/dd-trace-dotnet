// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "ManagedThreadInfo.h"
#include "ServiceBase.h"


class IStackSamplerLoopManager : public ServiceBase
{
public:
    virtual bool AllowStackWalk(std::shared_ptr<ManagedThreadInfo> pThreadInfo) = 0;
    virtual void NotifyThreadState(bool isSuspended) = 0;
    virtual void NotifyCollectionStart() = 0;
    virtual void NotifyCollectionEnd() = 0;
    virtual void NotifyIterationFinished() = 0;
};
