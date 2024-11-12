#pragma once

#include <algorithm>
#include <sstream>
#include <string>
#include <vector>
#include <map>
#include <set>
#include <unordered_map>
#include <functional>
#include "iast_constants.h"

#include "../../../../shared/src/native-src/string.h"
#include "../logger.h"

namespace iast
{

#define IsLogEnabled(x) true

#define STR(x) #x
#define WSTR(x) Toshared::WSTRING(STR(x))

#define DEL(x)                  if(x){delete(x);(x)=nullptr;}
#define REL(x)                  if(x){x->Release();(x)=nullptr;}
#define DEL_ARR(x)				if(x){delete[](x);(x)=nullptr;}
#define DEL_MAP_VALUES(map)     { for (auto item : map) { if(item.second){delete(item.second); }} map.clear(); }
#define DEL_VEC_VALUES(vec)     { for (auto item : vec) { if(item){delete(item); }} vec.clear(); }
#define CLOSE_HANDLE(x)                                                                                                \
    if (x != nullptr && x != INVALID_HANDLE_VALUE)                                                                     \
    {                                                                                                                  \
        try                                                                                                            \
        {                                                                                                              \
            CloseHandle(x);                                                                                            \
        }                                                                                                              \
        catch (...)                                                                                                    \
        {                                                                                                              \
        };                                                                                                             \
        x = INVALID_HANDLE_VALUE;                                                                                      \
    }

#define GMMD_R_IID(cpi,mid,iid,y)    cpi->GetModuleMetaData(mid, ofRead, iid, (IUnknown**)y)
#define GMMD_RW_IID(cpi,mid,iid,y)   cpi->GetModuleMetaData(mid, ofRead | ofWrite, iid, (IUnknown**)y)
#define GMMD_R(cpi,mid,y)       cpi->GetModuleMetaData(mid, ofRead, __uuidof(*y), (void**)y)
#define GMMD_RW(cpi,mid,y)      cpi->GetModuleMetaData(mid, ofRead | ofWrite, __uuidof(*y), (void**)y)

#define MAKE_HRESULT_FROM_ERRNO(errnoValue) (MAKE_HRESULT(errnoValue == 0 ? SEVERITY_SUCCESS : SEVERITY_ERROR, FACILITY_NULL, HRESULT_CODE(errnoValue)))
#define MIN(a,b) (((a) < (b)) ? (a) : (b))

    ////////////////////// Version Utils ///////////////////
    struct VersionInfo
    {
        int major;
        int minor;
        int build;
        int rev;

        shared::WSTRING ToString();
    };

    VersionInfo GetVersionInfo(const shared::WSTRING& versionW);
    VersionInfo GetVersionInfo(const std::string& version);
    int Compare(const VersionInfo& v1, const VersionInfo& v2);
    
    std::string GetDatadogVersion();
    shared::WSTRING GetDatadogVersionW();
    ASSEMBLYMETADATA* GetDatadogAssemblyMetadata();


    ////////////////////// Container Utils ///////////////////
    template <class Container>
    bool Contains(const Container& items, const typename Container::value_type& value)
    {
        return std::find(items.begin(), items.end(), value) != items.end();
    }
    template <class Container>
    int AddRange(Container& container, const Container& values)
    {
        int res = 0;
        for (auto it = values.begin(); it != values.end(); it++)
        {
            if (!Contains(container, *it))
            {
                container.push_back(*it);
                res++;
            }
        }
        return res;
    }
    template <class Element, class Container>
    int Add(std::set<Element>& container, const Container& values)
    {
        auto size0 = container.size();
        for (auto it = values.begin(); it != values.end(); it++)
        {
            container.insert(*it);
        }
        return (int)(container.size() - size0);
    }

    template<typename T>
    int IndexOf(const std::vector<T>& vec, const T& item)
    {
        auto it = std::find(vec.begin(), vec.end(), item);
        if (it == vec.end()) { return -1; }
        return std::distance(vec.begin(), it);
    }

    template<typename TKey, typename TValue>
    TValue* Get(std::unordered_map<TKey, TValue*>& map, TKey& key, std::function<TValue*()> newValue = nullptr)
    {
        auto it = map.find(key);
        if (it != map.end()) { return it->second; }
        if (newValue)
        {
            auto value = newValue();
            map[key] = value;
            return value;
        }
        return nullptr;
    }

    ////////////////////// String Matching ///////////////////
    enum class MatchResult
    {
        NoMatch = 0, 
        Wildcard = 1,
        Exact = 2
    };

    MatchResult IsMatch(const std::string& exp, const std::string& value);
    MatchResult IsMatch(const shared::WSTRING& exp, const shared::WSTRING& value);
    MatchResult IsMatch(const std::vector<shared::WSTRING>& values, const shared::WSTRING& value, unsigned int* pMatchLen = nullptr);
    bool IsExcluded(const std::vector<shared::WSTRING>& includeFilters,
                    const std::vector<shared::WSTRING>& excludeFilters, const shared::WSTRING& value,
                    MatchResult* includedMath = nullptr, MatchResult* excludedMatch = nullptr);

    ///////////////////// String Utils /////////////////////
    std::vector<WSTRING> Split(const WSTRING& s, WCHAR delim);
    std::vector<WSTRING> Split(const WSTRING& str, const WSTRING& delims = WStr(" "));
    std::vector<std::string> Split(const std::string& s, char delim);
    std::vector<std::string> Split(const std::string& str, const std::string& delims = " ");

    std::vector<std::string> SplitParams(const std::string& subject);
    std::vector<WSTRING> SplitParams(const WSTRING& subject);
    void SplitType(const WSTRING& subjectW, WSTRING* assembliesW, WSTRING* typeW, WSTRING* methodW = nullptr, WSTRING* paramsW = nullptr);

    WSTRING Trim(const WSTRING& str, const WSTRING& c = WStr(" \t\r\n"));
    WSTRING TrimStart(const WSTRING& str, const WSTRING& c = WStr(" \t\r\n"));
    WSTRING TrimEnd(const WSTRING& str, const WSTRING& c = WStr(" \t\r\n"));
    std::string Trim(const std::string& str, const std::string& c = " \t\r\n");
    std::string TrimEnd(const std::string& str, const std::string& c = " \t\r\n");
    std::string TrimStart(const std::string& str, const std::string& c = " \t\r\n");

    int ConvertToInt(const WSTRING& str);
    bool ConvertToBool(const WSTRING& str);
    bool TryParseInt(const std::string& str, int* pValue);
    int ConvertToInt(const std::string& str);
    bool ConvertToBool(const std::string& str);

    std::vector<int> ConvertToIntVector(const WSTRING& str);
    std::vector<bool> ConvertToBoolVector(const WSTRING& str);

    std::string Join(const std::vector<std::string>& cont, const std::string& delim = ";");

    bool BeginsWith(const WSTRING& str, const WSTRING& begining);
    bool EndsWith(const WSTRING& str, const WSTRING& ending);
    bool BeginsWith(const std::string& str, const std::string& begining);
    bool EndsWith(const std::string& str, const std::string& ending);

    inline WSTRING ToUpper(const WSTRING& text)
    {
        WSTRING res = text;
        transform(res.begin(), res.end(), res.begin(), ::toupper);
        return res;
    }
    inline WSTRING ToLower(const WSTRING& text)
    {
        WSTRING res = text;
        transform(res.begin(), res.end(), res.begin(), ::tolower);
        return res;
    }
    inline bool EqualsIgnoreCase(const WSTRING& txt1, const WSTRING& txt2)
    {
        return ToLower(txt1) == ToLower(txt2);
    }

    inline std::string ToUpper(const std::string& text)
    {
        std::string res = text;
        transform(res.begin(), res.end(), res.begin(), ::toupper);
        return res;
    }
    inline std::string ToLower(const std::string& text)
    {
        std::string res = text;
        transform(res.begin(), res.end(), res.begin(), ::tolower);
        return res;
    }
    inline bool EqualsIgnoreCase(const std::string& txt1, const std::string& txt2)
    {
        return ToLower(txt1) == ToLower(txt2);
    }

    std::size_t IndexOf(const WSTRING& where, const WSTRING& what, std::size_t* offset = nullptr);
    std::size_t IndexOf(const std::string& where, const std::string& what, std::size_t* offset = nullptr);
    std::size_t LastIndexOf(const WSTRING& where, const WSTRING& what, std::size_t* offset = nullptr);
    std::size_t LastIndexOf(const std::string& where, const std::string& what, std::size_t* offset = nullptr);
    bool Contains(const WSTRING& where, const WSTRING& what);
    bool Contains(const std::string& where, const std::string& what);

    /////////////////////// Guards /////////////////////////////
    class BoolGuard
    {
    private:
        bool* _bool;

    public:
        BoolGuard(bool* var)
        {
            _bool = var;
            *_bool = TRUE;
        }
        ~BoolGuard()
        {
            *_bool = FALSE;
        }
    };
#define BOOLGUARD(x) BoolGuard __boolGuard_##x(&x);

    template <typename T>
    class ObjectScopeGuard
    {
    public:
        T* data = nullptr;
        ObjectScopeGuard()
        {
        }
        ObjectScopeGuard(T* data)
        {
            this->data = data;
        }
        ~ObjectScopeGuard()
        {
            DEL(data);
        }
    };

    class CS
    {
    private:
#ifdef _WIN32
        CRITICAL_SECTION cs;
#else
        std::mutex cs;
#endif
    public:
        CS();
        virtual ~CS();
        bool Lock();
        void Unlock();
    };

    class CSGuard
    {
        CS* cs;
        std::atomic<int> refs;

    public:
        CSGuard(CS* cs);
        virtual ~CSGuard();
        void Enter();
        void Leave();
    };

#define CSGUARD(x) CSGuard __csGuard_##x(&x);
#define CSENTER(x) __csGuard_##x.Enter();
#define CSLEAVE(x) __csGuard_##x.Leave();

    /////////////////////// Enum Utils /////////////////////////////
#define BEGIN_ENUM_PARSE(enumName) std::unordered_map<enumName, std::string> enumName##_Values = {
#define ENUM_VALUE(enumName, enumValue) {enumName::enumValue, STR(enumValue)},
#define END_ENUM_PARSE(enumName)                                                                                       \
    };                                                                                                                 \
    enumName Parse##enumName(const std::string& txt)                                                                   \
    {                                                                                                                  \
        for (auto value : enumName##_Values)                                                                           \
        {                                                                                                              \
            if (iast::EqualsIgnoreCase(value.second, txt))                                                             \
            {                                                                                                          \
                return value.first;                                                                                    \
            }                                                                                                          \
        }                                                                                                              \
        return (enumName) 0;                                                                                           \
    }                                                                                                                  \
    std::string ToString(enumName e)                                                                                   \
    {                                                                                                                  \
        auto it = enumName##_Values.find(e);                                                                           \
        if (it != enumName##_Values.end()) return it->second;                                                          \
        return "";                                                                                                     \
    }
}
