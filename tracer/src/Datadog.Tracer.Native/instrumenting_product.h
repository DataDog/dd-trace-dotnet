#ifndef DD_CLR_INSTRUMENTING_PRODUCT_H_
#define DD_CLR_INSTRUMENTING_PRODUCT_H_

namespace trace
{
// Matches Datadog.Trace.FaultTolerant.InstrumentingProduct
enum class InstrumentingProduct : int
{
    Tracer = 0x01,
    DynamicInstrumentation = 0x02,
    ASM = 0x04
};

constexpr InstrumentingProduct operator|(InstrumentingProduct lhs, InstrumentingProduct rhs)
{
    return static_cast<InstrumentingProduct>(static_cast<int>(lhs) | static_cast<int>(rhs));
}

constexpr InstrumentingProduct operator&(InstrumentingProduct lhs, InstrumentingProduct rhs)
{
    return static_cast<InstrumentingProduct>(static_cast<int>(lhs) & static_cast<int>(rhs));
}

inline InstrumentingProduct& operator|=(InstrumentingProduct& lhs, InstrumentingProduct rhs)
{
    lhs = lhs | rhs;
    return lhs;
}

inline InstrumentingProduct& operator&=(InstrumentingProduct& lhs, InstrumentingProduct rhs)
{
    lhs = lhs & rhs;
    return lhs;
}
} // namespace trace

#endif