#pragma once

#include <stdint.h>

class LiveObject
{
public:
    LiveObject();
    LiveObject(uint64_t address, uint64_t typeId, uint64_t size);

public:
    uint64_t _address;
    uint64_t _typeId;
    uint64_t _size;
};