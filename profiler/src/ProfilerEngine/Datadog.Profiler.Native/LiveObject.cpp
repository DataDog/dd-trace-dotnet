#include "LiveObject.h"

LiveObject::LiveObject()
{
    _address = 0;
    _typeId = 0;
    _size = 0;
}

LiveObject::LiveObject(uint64_t address, uint64_t typeId, uint64_t size)
{
    _address = address;
    _typeId = typeId;
    _size = size;
}
