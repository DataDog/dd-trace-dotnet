#include "TypeInfo.h"

TypeInfo::TypeInfo()
{
    _id = 0;
    _name = "";
    _instances.reserve(1024);
}

void TypeInfo::SetId(uint64_t id)
{
    _id = id;
}

void TypeInfo::SetName(std::string name)
{
    _name = name;
}

void TypeInfo::AddInstance(uint64_t address, uint64_t size)
{
    _instances.push_back(LiveObject(address, _id, size));
}