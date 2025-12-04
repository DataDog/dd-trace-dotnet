



struct UnwinderTracer
{
    enum class Event : uint8_t
    {
        Start,
        AbortRequested,
        GetContextFailed,
        InitFailed,
        GetIpFailed,
        AddFrameFailed,
        ManagedFrame,
        NativeFrame,
        ManualStart,
        ManualFramePointerUnavailable,
        ManualFramePointerReadFailed,
        ManualFramePointerInvalidReturn,
        ManualFramePointerSuccess,
        ManualLinkRegisterSuccess,
        ManualFallback,
        StepResult,
        Finish,
        ManagedViaJitCache,
        ManagedViaProcMaps,
        ManagedDetectionMiss,
        CacheMissing,
    };

    struct Entry
    {
        Event Event;
        uintptr_t Value;
        uintptr_t Aux;
        std::int32_t Result;
    };

    static constexpr std::size_t MaxEntries = 128;

    void Reset(pid_t threadId, uintptr_t contextPointer);
    void SetInitFlags(std::uint32_t flags);
    void Append(Event event, uintptr_t value, uintptr_t aux, std::int32_t result);
    std::uint32_t Count() const;
    bool HasOverflow() const;
    Entry EntryAt(std::size_t index) const;
    pid_t GetThreadId() const { return _threadId; }
    uintptr_t GetContextPointer() const { return _contextPointer; }
    std::uint32_t GetInitFlags() const { return _initFlags; }
    void ResetAfterFlush();

private:
    pid_t _threadId{0};
    uintptr_t _contextPointer{0};
    std::uint32_t _initFlags{0};
    std::atomic<std::uint32_t> _count{0};
    std::atomic<bool> _overflow{false};
    std::array<Entry, MaxEntries> _entries{};
};