#pragma once

#include <string>

class DynamicLibraryBase
{
public:
    bool Load();
    bool Unload();

    const std::string& GetFilePath();

protected:
    DynamicLibraryBase(const std::string& filePath);
    virtual ~DynamicLibraryBase() = default;

    void* GetFunction(const std::string& funcName);

private:
    virtual void AfterLoad() = 0;
    virtual void BeforeUnload() = 0;

    std::string _filePath;
    void* _instance;
};
