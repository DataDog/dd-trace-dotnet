#include "iast_util.h"
#include "../../../../shared/src/native-src/pal.h"
#include "../../../../shared/src/native-src/version.h"

#include <cwctype>
#include <iterator>
#include <sstream>
#include <string> //NOLINT
#include <vector>
#include <sys/stat.h> 
#include <stdio.h>
#include "../logger.h"

using namespace shared;

extern char** environ;

namespace iast
{
#ifndef _WIN32
static thread_local std::unordered_map<void *, bool> locked;
#endif
    CS::CS()
    {
#ifdef _WIN32
        InitializeCriticalSection(&cs);
#else
        locked[this] = false;
#endif
    }
    CS::~CS()
    {
#ifdef _WIN32
        DeleteCriticalSection(&cs);
#else
        locked.erase(this); // To avoid uncontrolled map growth
#endif
    }
    bool CS::Lock()
    {
#ifdef _WIN32
        EnterCriticalSection(&cs);
#else
        if (locked[this])
        {
            return false;
        }
        cs.lock();
        locked[this] = true;
#endif
        return true;
    }
    void CS::Unlock()
    {
#ifdef _WIN32
        LeaveCriticalSection(&cs);
#else
        if (locked[this])
        {
            cs.unlock();
            locked[this] = false;
        }
#endif
    }

    CSGuard::CSGuard(CS* cs) : refs(0)
    {
        this->cs = cs;
        Enter();
    }
    CSGuard::~CSGuard()
    {
        Leave();
        cs = nullptr;
    }
    void CSGuard::Enter()
    {
        if (std::atomic_fetch_add(&refs, 1) == 0)
        {
            if (!cs->Lock())
            {
                cs = nullptr;
            }
        }
    }
    void CSGuard::Leave()
    {
        if (cs && refs > 0 && std::atomic_fetch_sub(&refs, 1) == 1)
        {
            try
            {
                cs->Unlock();
            }
            catch (...)
            {
                /* Ignored */
            }
        }
    }

    MatchResult IsMatch(const std::string& exp, const std::string& value)
    {
        return IsMatch(ToWSTRING(exp), ToWSTRING(value));
    }
    MatchResult IsMatch(const WSTRING& expW, const WSTRING& valueW)
    {
        auto exp = ToString(expW);
        auto value = ToString(valueW);
        //Wildcard position
        auto wcPos = exp.rfind('*');
        if (wcPos == std::string::npos)
        {
            //no wildcard
            if (exp == value)
            {
                return MatchResult::Exact;
            }
        }
        if (wcPos == exp.length() - 1)
        {
            //Compare beginings
            auto expr = exp.substr(0, exp.length() - 1);
            if (value.find(expr) == 0)
            {
                return MatchResult::Wildcard;
            }
        }
        if (wcPos == 0)
        {
            //Compare endings
            auto expr = exp.substr(1, exp.length() - 1);
            if (value.rfind(expr) == value.length() - expr.length())
            {
                return MatchResult::Wildcard;
            }
        }
        return MatchResult::NoMatch;
    }
    MatchResult IsMatch(const std::vector<WSTRING>& values, const WSTRING& value, unsigned int *pMatchLen)
    {
        MatchResult finalMatch = MatchResult::NoMatch;
        WSTRING valueTrim = shared::Trim(value);
        for (auto it = values.begin(); it != values.end(); it++)
        {
            MatchResult match = IsMatch(Trim(*it), valueTrim);
            if (match != MatchResult::NoMatch)
            {
                if (pMatchLen != nullptr)
                {
                    *pMatchLen = static_cast<unsigned int>(valueTrim.length());
                }
                finalMatch = match;
                if (match == MatchResult::Exact)
                {
                    if (pMatchLen != nullptr)
                    {
                        *pMatchLen += 1;
                    }
                    return finalMatch;
                }
            }
        }
        return finalMatch;
    }

    bool IsExcluded(const std::vector<WSTRING>& includeFilters, const std::vector<WSTRING>& excludeFilters, const WSTRING& signature, MatchResult* includedMatch, MatchResult* excludedMatch)
    {
        if (includedMatch) { *includedMatch = MatchResult::NoMatch; }
        if (excludedMatch) { *excludedMatch = MatchResult::NoMatch; }

        unsigned int includedLen;
        MatchResult included = IsMatch(includeFilters, signature, &includedLen);
        if (includedMatch) { *includedMatch = included; }
        if (included == MatchResult::Exact)
        {
            return false;
        }
        unsigned int excludedLen;
        MatchResult excluded = IsMatch(excludeFilters, signature, &excludedLen);
        if (excludedMatch) { *excludedMatch = excluded; }
        if (included == MatchResult::Wildcard && excluded == MatchResult::Wildcard)
        {
            return excludedLen >= includedLen;
        }
        return ((included == MatchResult::Wildcard && excluded == MatchResult::Exact) || (included == MatchResult::NoMatch && excluded != MatchResult::NoMatch));
    }


    //----------------------------

    WSTRING VersionInfo::ToString()
    {
        std::stringstream str;
        str << major << "." << minor << "." << build << "." << rev;
        return ToWSTRING(str.str());
    }

    static ASSEMBLYMETADATA* pDatadogAssemblyMetaData = nullptr;

    ASSEMBLYMETADATA* GetDatadogAssemblyMetadata()
    {
        if (!pDatadogAssemblyMetaData)
        {
            auto versionInfo = GetVersionInfo(GetDatadogVersion());
            pDatadogAssemblyMetaData = new ASSEMBLYMETADATA();
            ZeroMemory(pDatadogAssemblyMetaData, sizeof(*pDatadogAssemblyMetaData));
            pDatadogAssemblyMetaData->usMajorVersion = versionInfo.major;
            pDatadogAssemblyMetaData->usMinorVersion = versionInfo.minor;
            pDatadogAssemblyMetaData->usBuildNumber = versionInfo.build;
            pDatadogAssemblyMetaData->usRevisionNumber = versionInfo.rev;
        }
        return pDatadogAssemblyMetaData;
    }

    VersionInfo GetVersionInfo(const shared::WSTRING& versionW)
    {
        return GetVersionInfo(ToString(versionW));
    }
    VersionInfo GetVersionInfo(const std::string& version)
    {
        auto v = version;
        if (StartsWith(v, "V") || StartsWith(v, "v"))
        {
            v = v.substr(1);
        }

        VersionInfo res{0};
        auto parts = Split(v, ".");
        unsigned int x = 0;
        if (parts.size() > x)
        {
            res.major = ConvertToInt(parts[x++]);
        }
        if (parts.size() > x)
        {
            res.minor = ConvertToInt(parts[x++]);
        }
        if (parts.size() > x)
        {
            res.build = ConvertToInt(parts[x++]);
        }
        if (parts.size() > x)
        {
            res.rev = ConvertToInt(parts[x++]);
        }
        return res;
    }
    int Compare(const VersionInfo& v1, const VersionInfo& v2)
    {
        if (v1.major != v2.major)
        {
            return v1.major - v2.major;
        }
        if (v1.minor != v2.minor)
        {
            return v1.minor - v2.minor;
        }
        if (v1.build != v2.build)
        {
            return v1.build - v2.build;
        }
        return v1.rev - v2.rev;
    }
    std::string GetDatadogVersion()
    {
        return PROFILER_VERSION;
    }
    WSTRING GetDatadogVersionW()
    {
        static shared::WSTRING version = EmptyWStr;
        if (version == EmptyWStr)
        {
            version = ToWSTRING(PROFILER_VERSION);
        }
        return version;
    }

    //----------------------------------

    template <typename Out>
    void Split(const WSTRING& s, WCHAR delim, Out result)
    {
        size_t lpos = 0;
        for (size_t i = 0; i < s.length(); i++)
        {
            if (s[i] == delim)
            {
                *(result++) = s.substr(lpos, (i - lpos));
                lpos = i + 1;
            }
        }
        *(result++) = s.substr(lpos);
    }
    std::vector<WSTRING> Split(const WSTRING& s, WCHAR delim)
    {
        std::vector<WSTRING> elems;
        Split(s, delim, std::back_inserter(elems));
        return elems;
    }
    std::vector<WSTRING> Split(const WSTRING& str, const WSTRING& delims)
    {
        std::vector<WSTRING> res;
        std::size_t current, previous = 0;
        current = str.find_first_of(delims);
        WSTRING element;
        while (current != WSTRING::npos)
        {
            element = str.substr(previous, current - previous);
            if (element != EmptyWStr)
            {
                res.push_back(element);
            }
            previous = current + 1;
            current = str.find_first_of(delims, previous);
        }
        element = str.substr(previous, current - previous);
        if (element != EmptyWStr)
        {
            res.push_back(element);
        }
        return res;
    }

    template <typename Out>
    void Split(const std::string& s, char delim, Out result)
    {
        size_t lpos = 0;
        for (size_t i = 0; i < s.length(); i++)
        {
            if (s[i] == delim)
            {
                *(result++) = s.substr(lpos, (i - lpos));
                lpos = i + 1;
            }
        }
        *(result++) = s.substr(lpos);
    }
    std::vector<std::string> Split(const std::string& s, char delim)
    {
        std::vector<std::string> elems;
        Split(s, delim, std::back_inserter(elems));
        return elems;
    }
    std::vector<std::string> Split(const std::string& str, const std::string& delims)
    {
        std::vector<std::string> res;
        std::size_t current, previous = 0;
        current = str.find_first_of(delims);
        std::string element;
        while (current != std::string::npos)
        {
            element = str.substr(previous, current - previous);
            if (element != "")
            {
                res.push_back(element);
            }
            previous = current + 1;
            current = str.find_first_of(delims, previous);
        }
        element = str.substr(previous, current - previous);
        if (element != "")
        {
            res.push_back(element);
        }
        return res;
    }

    std::vector<std::string> SplitParams(const std::string& subject)
    {
        std::vector<std::string> res;
        try
        {
            // "mscorlib,netstandard,System.Private.CoreLib",PROPAGATION,""

            // Manual parsing
            int blockStart = -1;
            char endChar;
            for (unsigned int x = 0; x < subject.length(); x++)
            {
                auto c = subject[x];
                if (blockStart < 0) // Looking for blockStart
                {
                    blockStart = x;
                    if (c == '"')
                    {
                        endChar = '\"';
                        blockStart++;
                    }
                    else if (c == '[')
                    {
                        endChar = ']';
                        blockStart++;
                    }
                    else if (c == '(')
                    {
                        endChar = ')';
                        blockStart++;
                    }
                    else if (c == '<')
                    {
                        endChar = '>';
                        blockStart++;
                    }
                    else
                    {
                        endChar = ',';
                    }
                    continue;
                }
                bool endMatch = (endChar == c);
                if (endMatch || (x == subject.length() - 1))
                {
                    int blockEnd = x;
                    if (endMatch)
                    {
                        blockEnd--;
                    }

                    // end block
                    std::string block = "";
                    if (blockStart <= blockEnd)
                    {
                        block = subject.substr(blockStart, blockEnd - blockStart + 1);
                    }
                    res.push_back(block);

                    if (endMatch && c != ',')
                    {
                        x++;
                    }                // Skip next comma
                    blockStart = -1; // new block required
                }
            }
        }
        catch (std::exception& e)
        {
            trace::Logger::Error("Parsing error : ", e.what(), " -> ", subject);
        }
        return res;
    }
    std::vector<WSTRING> SplitParams(const WSTRING& subject)
    {
        std::vector<WSTRING> res;
        for (auto part : SplitParams(ToString(subject)))
        {
            res.push_back(ToWSTRING(part));
        }
        return res;
    }

    void SplitType(const WSTRING& subjectW, WSTRING* assembliesW, WSTRING* typeW, WSTRING* methodW, WSTRING* paramsW)
    {
        std::string subject = shared::ToString(subjectW);
        try
        {
            auto typeIndex = IndexOf(subject, "|");
            auto methodIndex = IndexOf(subject, "::");
            auto paramsIndex = IndexOf(subject, "(");

            std::string assemblies = "";
            std::string type = "";
            std::string method = "";
            std::string params = "";

            if (typeIndex != std::string::npos)
            {
                assemblies = Trim(subject.substr(0, typeIndex));
                typeIndex++;
            }
            else
            {
                typeIndex = 0;
            }
            if (methodIndex != std::string::npos)
            {
                type = Trim(subject.substr(typeIndex, methodIndex - typeIndex));
                methodIndex += 2;

                if (paramsIndex != std::string::npos)
                {
                    method = Trim(subject.substr(methodIndex, paramsIndex - methodIndex));
                    params = Trim(subject.substr(paramsIndex));
                }
            }
            else
            {
                type = Trim(subject.substr(typeIndex));
            }

            if (assembliesW)
            {
                *assembliesW = ToWSTRING(assemblies);
            }
            if (typeW)
            {
                *typeW = ToWSTRING(type);
            }
            if (methodW)
            {
                *methodW = ToWSTRING(method);
            }
            if (paramsW)
            {
                *paramsW = ToWSTRING(params);
            }
        }
        catch (std::exception& e)
        {
            trace::Logger::Error("Parsing error : ", e.what(), " -> ", subject);
        }
    }

    inline int _IndexOf(const WCHAR c, const WSTRING& pC)
    {
        for (unsigned int x = 0; x < pC.length(); x++)
        {
            if (pC[x] == c)
            {
                return x;
            }
        }
        return -1;
    }
    inline int _IndexOf(const char c, const std::string& pC)
    {
        for (unsigned int x = 0; x < pC.length(); x++)
        {
            if (pC[x] == c)
            {
                return x;
            }
        }
        return -1;
    }

    WSTRING Trim(const WSTRING& str, const WSTRING& c)
    {
        return TrimStart(TrimEnd(str, c), c);
    }
    WSTRING TrimEnd(const WSTRING& str, const WSTRING& c)
    {
        if (str.length() == 0)
        {
            return EmptyWStr;
        }
        size_t indexFrom = 0;
        auto indexTo = str.length() - 1;
        while (indexFrom <= indexTo)
        {
            if (_IndexOf(str[indexTo], c) < 0)
            {
                break;
            }
            indexTo--;
        }
        if (indexTo < indexFrom)
        {
            return EmptyWStr;
        }
        if (indexFrom == 0 && indexTo == str.length() - 1)
        {
            return str;
        }
        return str.substr(indexFrom, indexTo - indexFrom + 1);
    }
    WSTRING TrimStart(const WSTRING& str, const WSTRING& c)
    {
        if (str.length() == 0)
        {
            return EmptyWStr;
        }
        size_t indexFrom = 0;
        auto indexTo = str.length() - 1;
        while (indexFrom <= indexTo)
        {
            if (_IndexOf(str[indexFrom], c) < 0)
            {
                break;
            }
            indexFrom++;
        }
        if (indexTo < indexFrom)
        {
            return EmptyWStr;
        }
        if (indexFrom == 0 && indexTo == str.length() - 1)
        {
            return str;
        }
        return str.substr(indexFrom, indexTo - indexFrom + 1);
    }

    std::string Trim(const std::string& str, const std::string& c)
    {
        return TrimStart(TrimEnd(str, c), c);
    }
    std::string TrimEnd(const std::string& str, const std::string& c)
    {
        if (str.length() == 0)
        {
            return "";
        }
        size_t indexFrom = 0;
        auto indexTo = str.length() - 1;
        while (indexFrom <= indexTo)
        {
            if (_IndexOf(str[indexTo], c) < 0)
            {
                break;
            }
            indexTo--;
        }
        if (indexTo < indexFrom)
        {
            return "";
        }
        if (indexFrom == 0 && indexTo == str.length() - 1)
        {
            return str;
        }
        return str.substr(indexFrom, indexTo - indexFrom + 1);
    }
    std::string TrimStart(const std::string& str, const std::string& c)
    {
        if (str.length() == 0)
        {
            return "";
        }
        size_t indexFrom = 0;
        auto indexTo = str.length() - 1;
        while (indexFrom <= indexTo)
        {
            if (_IndexOf(str[indexFrom], c) < 0)
            {
                break;
            }
            indexFrom++;
        }
        if (indexTo < indexFrom)
        {
            return "";
        }
        if (indexFrom == 0 && indexTo == str.length() - 1)
        {
            return str;
        }
        return str.substr(indexFrom, indexTo - indexFrom + 1);
    }

    bool TryParseInt(const WSTRING& str, int* pValue)
    {
        return TryParseInt(ToString(str), pValue);
    }
    int ConvertToInt(const WSTRING& str)
    {
        return ConvertToInt(ToString(str));
    }
    bool ConvertToBool(const WSTRING& str)
    {
        return ConvertToBool(ToString(str));
    }

    bool TryParseInt(const std::string& str, int* pValue)
    {
        std::istringstream stream(Trim(str));
        int value = 0;
        stream >> value;
        bool res = !stream.fail();
        if (res && pValue)
        {
            *pValue = value;
        }
        return res;
    }
    int ConvertToInt(const std::string& str)
    {
        int res = 0;
        TryParseInt(str, &res);
        return res;
    }
    unsigned int ConvertHexToInt(const std::string& str)
    {
        std::stringstream stream;
        unsigned int res = 0;
        stream << std::hex << Trim(str);
        stream >> res;
        return res;
    }
    bool ConvertToBool(const std::string& str)
    {
        return str != "0" && str != "False" && str != "FALSE" && str != "false" && str != "";
    }

    std::vector<int> ConvertToIntVector(const WSTRING& str)
    {
        std::vector<int> res;
        auto in = Trim(str, WStr("[]"));
        if (in.length() > 0)
        {
            for (auto v : Split(in, WStr(',')))
            {
                res.push_back(ConvertToInt(v));
            }
        }
        return res;
    }
    std::vector<bool> ConvertToBoolVector(const WSTRING& str)
    {
        std::vector<bool> res;
        auto in = Trim(str, WStr("[]"));
        if (in.length() > 0)
        {
            for (auto v : Split(in, WStr(',')))
            {
                res.push_back(ConvertToBool(v));
            }
        }
        return res;
    }


    std::string Join(const std::vector<std::string>& cont, const std::string& delim)
    {
        std::stringstream res;
        bool first = true;
        for (auto it = cont.begin(); it != cont.end(); it++)
        {
            if (!first)
            {
                res << delim;
            }
            else
            {
                first = false;
            }
            res << *it;
        }
        return res.str();
    }

#ifndef _WIN32
    int _wcsnicmp(const WCHAR* begining, const WCHAR* str, int beginingLen)
    {
        for (int x = 0; x < beginingLen; x++)
        {
            auto c1 = begining[x];
            auto c2 = str[x];
            if (c1 == 0)
            {
                return -1;
            }
            if (c2 == 0)
            {
                return 1;
            }
            int lc1 = std::tolower(c1);
            int lc2 = std::tolower(c2);
            if (lc1 < lc2)
            {
                return -1;
            }
            else if (lc1 > lc2)
            {
                return 1;
            }
        }
        return 0;
    }
#endif

    bool BeginsWith(const WSTRING& str, const WSTRING& begining)
    {
        auto strLen = str.length();
        auto beginingLen = begining.length();

        if (strLen < beginingLen) return FALSE;

        if (beginingLen == 0) return FALSE;

        if (_wcsnicmp(begining.c_str(), str.c_str(), beginingLen) != 0)
        {
            return FALSE;
        }

        return TRUE;
    }
    bool EndsWith(const WSTRING& str, const WSTRING& ending)
    {
        auto strLen = str.length();
        auto endingLen = ending.length();

        if (strLen < endingLen) return FALSE;

        if (endingLen == 0) return FALSE;

        if (_wcsnicmp(ending.c_str(), &(str.c_str()[strLen - endingLen]), endingLen) != 0)
        {
            return FALSE;
        }

        return TRUE;
    }
    bool BeginsWith(const std::string& str, const std::string& begining)
    {
        return BeginsWith(ToWSTRING(str), ToWSTRING(begining));
    }
    bool EndsWith(const std::string& str, const std::string& ending)
    {
        return EndsWith(ToWSTRING(str), ToWSTRING(ending));
    }

    static WSTRING forbiddenChars(WStr("\\/:?\"<>|.,;*'$&%@![]{}!+=#- "));
    bool isForbidden(WCHAR c)
    {
        return WSTRING::npos != forbiddenChars.find(c);
    }

    std::size_t IndexOf(const WSTRING& where, const WSTRING& what, std::size_t* offset)
    {
        size_t off = 0;
        if (offset)
        {
            off = *offset;
        }
        auto pos = where.find(what, off);
        if (pos != WSTRING::npos && offset)
        {
            *offset = pos + what.length();
        }
        return pos;
    }
    std::size_t IndexOf(const std::string& where, const std::string& what, std::size_t* offset)
    {
        size_t off = 0;
        if (offset)
        {
            off = *offset;
        }
        auto pos = where.find(what, off);
        if (pos != std::string::npos && offset)
        {
            *offset = pos + what.length();
        }
        return pos;
    }

    std::size_t LastIndexOf(const WSTRING& where, const WSTRING& what, std::size_t* offset)
    {
        size_t off = -1;
        if (offset)
        {
            off = *offset;
        }
        auto pos = where.find_last_of(what, off);
        if (pos != WSTRING::npos && offset)
        {
            *offset = pos + what.length();
        }
        return pos;
    }
    std::size_t LastIndexOf(const std::string& where, const std::string& what, std::size_t* offset)
    {
        size_t off = -1;
        if (offset)
        {
            off = *offset;
        }
        auto pos = where.find_last_of(what, off);
        if (pos != std::string::npos && offset)
        {
            *offset = pos + what.length();
        }
        return pos;
    }

    bool Contains(const WSTRING& where, const WSTRING& what)
    {
        return IndexOf(where, what) != WSTRING::npos;
    }
    bool Contains(const std::string& where, const std::string& what)
    {
        return IndexOf(where, what) != std::string::npos;
    }
}