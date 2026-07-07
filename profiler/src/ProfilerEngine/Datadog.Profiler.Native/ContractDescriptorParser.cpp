// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ContractDescriptorParser.h"

#include "CdacJson.h"

#include <cctype>
#include <cstdlib>

namespace cdac
{
namespace
{
bool TryParseNumber(const std::string& text, uint64_t& value)
{
    // Trim
    size_t start = 0;
    size_t end = text.size();
    while (start < end && std::isspace(static_cast<unsigned char>(text[start])))
    {
        start++;
    }
    while (end > start && std::isspace(static_cast<unsigned char>(text[end - 1])))
    {
        end--;
    }
    std::string s = text.substr(start, end - start);
    if (s.empty())
    {
        value = 0;
        return false;
    }

    char* parseEnd = nullptr;
    if (s.size() > 2 && (s[0] == '0') && (s[1] == 'x' || s[1] == 'X'))
    {
        value = static_cast<uint64_t>(std::strtoull(s.c_str() + 2, &parseEnd, 16));
        return parseEnd != (s.c_str() + 2);
    }

    // Decimal (handle a possible leading sign as signed then reinterpret).
    value = static_cast<uint64_t>(std::strtoull(s.c_str(), &parseEnd, 10));
    if (parseEnd != s.c_str())
    {
        return true;
    }
    long long signedValue = std::strtoll(s.c_str(), &parseEnd, 10);
    if (parseEnd != s.c_str())
    {
        value = static_cast<uint64_t>(signedValue);
        return true;
    }
    value = 0;
    return false;
}

uint64_t ParseNumber(const JsonValue& element)
{
    if (element.IsNumber())
    {
        return element.AsUInt64();
    }
    if (element.IsString())
    {
        uint64_t v = 0;
        if (TryParseNumber(element.AsString(), v))
        {
            return v;
        }
    }
    return 0;
}

uint64_t ResolveIndirect(const JsonValue& indexArray, const std::vector<uintptr_t>& pointerData)
{
    if (!indexArray.IsArray() || indexArray.Size() == 0)
    {
        return 0;
    }
    int index = indexArray[0].AsInt();
    if (index >= 0 && static_cast<size_t>(index) < pointerData.size())
    {
        return static_cast<uint64_t>(pointerData[static_cast<size_t>(index)]);
    }
    return 0;
}

// Resolves a pointer-shaped JSON value (used for sub-descriptor slots):
//   [[index], "type"] -> indirect with type, [index] -> indirect, [value, "type"] -> direct value.
uint64_t ResolvePointer(const JsonValue& value, const std::vector<uintptr_t>& pointerData)
{
    if (value.IsArray() && value.Size() >= 1)
    {
        const JsonValue& first = value[0];
        if (first.IsArray())
        {
            return ResolveIndirect(first, pointerData);
        }
        if (value.Size() == 1)
        {
            return ResolveIndirect(value, pointerData);
        }
        return ParseNumber(first);
    }
    return ParseNumber(value);
}

GlobalValue ParseGlobalValue(const std::string& name, const JsonValue& value, const std::vector<uintptr_t>& pointerData)
{
    GlobalValue gv;
    gv.Name = name;

    switch (value.GetType())
    {
        case JsonValue::Type::Array:
        {
            // Three shapes (see the runtime's GlobalDescriptorConverter):
            //   [value]            -> indirect: pointer_data[value]
            //   [value, "type"]    -> direct value, with a type name
            //   [[index], "type"]  -> indirect with a type name: pointer_data[index]
            size_t length = value.Size();
            if (length == 0)
            {
                return gv;
            }
            const JsonValue& first = value[0];
            if (length > 1 && value[1].IsString())
            {
                gv.TypeName = value[1].AsString();
            }

            if (first.IsArray())
            {
                gv.NumericValue = ResolveIndirect(first, pointerData);
                return gv;
            }
            if (length == 1)
            {
                gv.NumericValue = ResolveIndirect(value, pointerData);
                return gv;
            }
            if (first.IsString())
            {
                uint64_t parsed = 0;
                if (!TryParseNumber(first.AsString(), parsed))
                {
                    gv.StringValue = first.AsString();
                    return gv;
                }
                gv.NumericValue = parsed;
                return gv;
            }
            gv.NumericValue = ParseNumber(first);
            return gv;
        }

        case JsonValue::Type::String:
        {
            const std::string& s = value.AsString();
            uint64_t numeric = 0;
            if (TryParseNumber(s, numeric))
            {
                gv.NumericValue = numeric;
                gv.StringValue = s;
                return gv;
            }
            gv.StringValue = s;
            return gv;
        }

        case JsonValue::Type::Number:
            gv.NumericValue = value.AsUInt64();
            return gv;

        default:
            return gv;
    }
}

TypeInfo ParseCompactType(const std::string& name, const JsonValue& fields)
{
    TypeInfo type;
    type.Name = name;
    for (const auto& field : fields.Members())
    {
        const std::string& fieldName = field.first;
        const JsonValue& fieldValue = field.second;

        if (fieldName == "!")
        {
            type.Size = fieldValue.AsInt();
            continue;
        }

        FieldInfo info;
        info.Name = fieldName;
        if (fieldValue.IsArray())
        {
            // [offset, "type name"]
            if (fieldValue.Size() >= 1)
            {
                info.Offset = fieldValue[0].AsInt();
            }
            if (fieldValue.Size() > 1 && fieldValue[1].IsString())
            {
                info.TypeName = fieldValue[1].AsString();
            }
        }
        else
        {
            // bare offset
            info.Offset = fieldValue.AsInt();
        }
        type.Fields[fieldName] = info;
    }
    return type;
}

std::string VersionToString(const JsonValue& value)
{
    if (value.IsString())
    {
        return value.AsString();
    }
    if (value.IsNumber())
    {
        return value.AsString(); // the raw numeric token
    }
    return std::string{};
}
} // namespace

ParsedDescriptor ContractDescriptorParser::Parse(const RawContractDescriptor& raw)
{
    ParsedDescriptor result;

    JsonValue root = JsonValue::Parse(raw.Json);
    if (!root.IsObject())
    {
        return result;
    }

    if (const JsonValue* types = root.Find("types"); types != nullptr && types->IsObject())
    {
        for (const auto& type : types->Members())
        {
            result.Types[type.first] = ParseCompactType(type.first, type.second);
        }
    }

    if (const JsonValue* globals = root.Find("globals"); globals != nullptr && globals->IsObject())
    {
        for (const auto& global : globals->Members())
        {
            result.Globals[global.first] = ParseGlobalValue(global.first, global.second, raw.PointerData);
        }
    }

    if (const JsonValue* contracts = root.Find("contracts"); contracts != nullptr && contracts->IsObject())
    {
        for (const auto& c : contracts->Members())
        {
            result.Contracts[c.first] = VersionToString(c.second);
        }
    }

    // The in-memory descriptor emits "subDescriptors" (camelCase); older docs use "sub-descriptors".
    const JsonValue* subs = root.Find("subDescriptors");
    if (subs == nullptr)
    {
        subs = root.Find("sub-descriptors");
    }
    if (subs != nullptr && subs->IsObject())
    {
        for (const auto& s : subs->Members())
        {
            SubDescriptorSlot slot;
            slot.Name = s.first;
            slot.Slot = ResolvePointer(s.second, raw.PointerData);
            result.SubDescriptorSlots.push_back(slot);
        }
    }

    return result;
}
} // namespace cdac
