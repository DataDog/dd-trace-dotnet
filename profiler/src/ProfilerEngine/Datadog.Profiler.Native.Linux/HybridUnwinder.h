#include <cstdint>

class JitCodeCache;
class UnwinderTracer;

class HybridUnwinder
{
public:
    HybridUnwinder(JitCodeCache* jitCodeCache);
    ~HybridUnwinder();

    std::int32_t Unwind(void* ctx, std::uintptr_t* buffer, std::size_t bufferSize, UnwinderTracer* tracer);

private:
    bool IsManagedCode(std::uintptr_t ip);
    bool IsValidReturnAddress(std::uintptr_t address);

    JitCodeCache* _pJitCodeCache;
};