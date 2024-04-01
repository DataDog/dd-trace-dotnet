// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once


class IGarbageCollectionsListener;


class IEtwEventsManager
{
public:
    virtual void Register(IGarbageCollectionsListener* pGarbageCollectionsListener) = 0;
    virtual bool Start() = 0;
    virtual void Stop() = 0;

    virtual ~IEtwEventsManager() = default;
};