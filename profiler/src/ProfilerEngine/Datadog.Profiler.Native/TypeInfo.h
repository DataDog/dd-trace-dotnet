#pragma once

#include <string>
#include <vector>

#include "LiveObject.h"

class TypeInfo
{
public:
    TypeInfo();
    void SetId(uint64_t id);
    void SetName(std::string name);
    void AddInstance(uint64_t address, uint64_t size);

public:
    uint64_t _id;
    std::string _name;
    std::vector<LiveObject> _instances;
};
