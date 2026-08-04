// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <string>
#include <utility>
#include <vector>

// Minimal, dependency-free JSON parser tailored to the cDAC in-memory data descriptor (compact
// format). It supports objects, arrays, strings, numbers, booleans and null - everything the
// descriptor blob can contain. It is deliberately small and tolerant (skips // and /* */ comments
// and trailing commas, like System.Text.Json with the runtime's options) and never throws: on a
// malformed blob it returns a Null root.
namespace cdac
{
class JsonValue
{
public:
    enum class Type
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object
    };

    Type GetType() const { return _type; }

    bool IsNull() const { return _type == Type::Null; }
    bool IsArray() const { return _type == Type::Array; }
    bool IsObject() const { return _type == Type::Object; }
    bool IsString() const { return _type == Type::String; }
    bool IsNumber() const { return _type == Type::Number; }

    const std::string& AsString() const { return _str; }

    // Numeric accessors. JSON number tokens in the descriptor are always decimal; hex values are
    // carried as strings ("0x4") and handled by the parser helpers, not here.
    int64_t AsInt64() const;
    uint64_t AsUInt64() const;
    int AsInt() const { return static_cast<int>(AsInt64()); }

    size_t Size() const { return _array.size(); }
    const JsonValue& operator[](size_t index) const;

    // Object lookup; returns nullptr when absent.
    const JsonValue* Find(const std::string& key) const;

    const std::vector<JsonValue>& Items() const { return _array; }
    const std::vector<std::pair<std::string, JsonValue>>& Members() const { return _object; }

    // Parse a full JSON document. Returns a Null value on any error.
    static JsonValue Parse(const std::string& text);

private:
    static const JsonValue& Null();

    Type _type = Type::Null;
    bool _bool = false;
    std::string _str;                                       // String value or Number token
    std::vector<JsonValue> _array;                          // Array items
    std::vector<std::pair<std::string, JsonValue>> _object; // Object members (insertion order)

    friend class JsonParser;
};
} // namespace cdac
