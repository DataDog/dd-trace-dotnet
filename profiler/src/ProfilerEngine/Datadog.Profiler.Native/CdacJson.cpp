// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CdacJson.h"

#include <cstdlib>

namespace cdac
{
const JsonValue& JsonValue::Null()
{
    static const JsonValue s_null;
    return s_null;
}

const JsonValue& JsonValue::operator[](size_t index) const
{
    if (index >= _array.size())
    {
        return Null();
    }
    return _array[index];
}

const JsonValue* JsonValue::Find(const std::string& key) const
{
    for (const auto& member : _object)
    {
        if (member.first == key)
        {
            return &member.second;
        }
    }
    return nullptr;
}

int64_t JsonValue::AsInt64() const
{
    if (_type != Type::Number)
    {
        return 0;
    }
    return static_cast<int64_t>(std::strtoll(_str.c_str(), nullptr, 10));
}

uint64_t JsonValue::AsUInt64() const
{
    if (_type != Type::Number)
    {
        return 0;
    }
    // Numbers may be larger than int64 in theory; use unsigned parse then fall back.
    errno = 0;
    char* end = nullptr;
    unsigned long long v = std::strtoull(_str.c_str(), &end, 10);
    if (end == _str.c_str())
    {
        return 0;
    }
    return static_cast<uint64_t>(v);
}

// Recursive-descent parser over a string buffer.
class JsonParser
{
public:
    explicit JsonParser(const std::string& text) :
        _text(text), _pos(0), _ok(true)
    {
    }

    JsonValue Parse()
    {
        SkipWhitespaceAndComments();
        JsonValue v = ParseValue();
        if (!_ok)
        {
            return JsonValue{};
        }
        return v;
    }

private:
    void SkipWhitespaceAndComments()
    {
        while (_pos < _text.size())
        {
            char c = _text[_pos];
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                _pos++;
            }
            else if (c == '/' && _pos + 1 < _text.size() && _text[_pos + 1] == '/')
            {
                _pos += 2;
                while (_pos < _text.size() && _text[_pos] != '\n')
                {
                    _pos++;
                }
            }
            else if (c == '/' && _pos + 1 < _text.size() && _text[_pos + 1] == '*')
            {
                _pos += 2;
                while (_pos + 1 < _text.size() && !(_text[_pos] == '*' && _text[_pos + 1] == '/'))
                {
                    _pos++;
                }
                _pos += 2;
            }
            else
            {
                break;
            }
        }
    }

    char Peek() const { return _pos < _text.size() ? _text[_pos] : '\0'; }

    JsonValue ParseValue()
    {
        SkipWhitespaceAndComments();
        if (!_ok || _pos >= _text.size())
        {
            _ok = false;
            return JsonValue{};
        }

        char c = _text[_pos];
        switch (c)
        {
            case '{': return ParseObject();
            case '[': return ParseArray();
            case '"': return ParseString();
            case 't':
            case 'f': return ParseBool();
            case 'n': return ParseNull();
            default: return ParseNumber();
        }
    }

    JsonValue ParseObject()
    {
        JsonValue obj;
        obj._type = JsonValue::Type::Object;
        _pos++; // '{'
        SkipWhitespaceAndComments();
        if (Peek() == '}')
        {
            _pos++;
            return obj;
        }

        while (_ok)
        {
            SkipWhitespaceAndComments();
            if (Peek() != '"')
            {
                _ok = false;
                break;
            }
            JsonValue key = ParseString();
            SkipWhitespaceAndComments();
            if (Peek() != ':')
            {
                _ok = false;
                break;
            }
            _pos++; // ':'
            JsonValue value = ParseValue();
            if (!_ok)
            {
                break;
            }
            obj._object.emplace_back(key._str, std::move(value));

            SkipWhitespaceAndComments();
            char c = Peek();
            if (c == ',')
            {
                _pos++;
                SkipWhitespaceAndComments();
                if (Peek() == '}') // trailing comma
                {
                    _pos++;
                    break;
                }
                continue;
            }
            if (c == '}')
            {
                _pos++;
                break;
            }
            _ok = false;
            break;
        }
        return obj;
    }

    JsonValue ParseArray()
    {
        JsonValue arr;
        arr._type = JsonValue::Type::Array;
        _pos++; // '['
        SkipWhitespaceAndComments();
        if (Peek() == ']')
        {
            _pos++;
            return arr;
        }

        while (_ok)
        {
            JsonValue value = ParseValue();
            if (!_ok)
            {
                break;
            }
            arr._array.push_back(std::move(value));

            SkipWhitespaceAndComments();
            char c = Peek();
            if (c == ',')
            {
                _pos++;
                SkipWhitespaceAndComments();
                if (Peek() == ']') // trailing comma
                {
                    _pos++;
                    break;
                }
                continue;
            }
            if (c == ']')
            {
                _pos++;
                break;
            }
            _ok = false;
            break;
        }
        return arr;
    }

    JsonValue ParseString()
    {
        JsonValue str;
        str._type = JsonValue::Type::String;
        _pos++; // opening quote
        std::string out;
        while (_pos < _text.size())
        {
            char c = _text[_pos++];
            if (c == '"')
            {
                str._str = std::move(out);
                return str;
            }
            if (c == '\\' && _pos < _text.size())
            {
                char esc = _text[_pos++];
                switch (esc)
                {
                    case '"': out.push_back('"'); break;
                    case '\\': out.push_back('\\'); break;
                    case '/': out.push_back('/'); break;
                    case 'b': out.push_back('\b'); break;
                    case 'f': out.push_back('\f'); break;
                    case 'n': out.push_back('\n'); break;
                    case 'r': out.push_back('\r'); break;
                    case 't': out.push_back('\t'); break;
                    case 'u':
                    {
                        // Decode \uXXXX into UTF-8 (BMP only, sufficient for descriptor text).
                        if (_pos + 4 > _text.size())
                        {
                            _ok = false;
                            return str;
                        }
                        unsigned int code = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            char h = _text[_pos++];
                            code <<= 4;
                            if (h >= '0' && h <= '9') code |= static_cast<unsigned int>(h - '0');
                            else if (h >= 'a' && h <= 'f') code |= static_cast<unsigned int>(h - 'a' + 10);
                            else if (h >= 'A' && h <= 'F') code |= static_cast<unsigned int>(h - 'A' + 10);
                            else { _ok = false; return str; }
                        }
                        if (code < 0x80)
                        {
                            out.push_back(static_cast<char>(code));
                        }
                        else if (code < 0x800)
                        {
                            out.push_back(static_cast<char>(0xC0 | (code >> 6)));
                            out.push_back(static_cast<char>(0x80 | (code & 0x3F)));
                        }
                        else
                        {
                            out.push_back(static_cast<char>(0xE0 | (code >> 12)));
                            out.push_back(static_cast<char>(0x80 | ((code >> 6) & 0x3F)));
                            out.push_back(static_cast<char>(0x80 | (code & 0x3F)));
                        }
                        break;
                    }
                    default:
                        out.push_back(esc);
                        break;
                }
            }
            else
            {
                out.push_back(c);
            }
        }
        _ok = false; // unterminated string
        return str;
    }

    JsonValue ParseNumber()
    {
        JsonValue num;
        num._type = JsonValue::Type::Number;
        size_t start = _pos;
        if (Peek() == '-' || Peek() == '+')
        {
            _pos++;
        }
        while (_pos < _text.size())
        {
            char c = _text[_pos];
            if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
            {
                _pos++;
            }
            else
            {
                break;
            }
        }
        if (_pos == start)
        {
            _ok = false;
            return num;
        }
        num._str = _text.substr(start, _pos - start);
        return num;
    }

    JsonValue ParseBool()
    {
        JsonValue v;
        v._type = JsonValue::Type::Bool;
        if (_text.compare(_pos, 4, "true") == 0)
        {
            v._bool = true;
            _pos += 4;
        }
        else if (_text.compare(_pos, 5, "false") == 0)
        {
            v._bool = false;
            _pos += 5;
        }
        else
        {
            _ok = false;
        }
        return v;
    }

    JsonValue ParseNull()
    {
        JsonValue v;
        if (_text.compare(_pos, 4, "null") == 0)
        {
            _pos += 4;
        }
        else
        {
            _ok = false;
        }
        return v;
    }

    const std::string& _text;
    size_t _pos;
    bool _ok;
};

JsonValue JsonValue::Parse(const std::string& text)
{
    JsonParser parser(text);
    return parser.Parse();
}
} // namespace cdac
