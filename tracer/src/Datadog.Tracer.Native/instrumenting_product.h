#ifndef DD_CLR_INSTRUMENTING_PRODUCT_H_
#define DD_CLR_INSTRUMENTING_PRODUCT_H_

namespace trace
{
// Matches Datadog.Trace.FaultTolerant.InstrumentingProducts
enum class InstrumentingProducts : int
{
    Tracer = 0x01,
    DynamicInstrumentation = 0x02,
    ASM = 0x04
};

constexpr InstrumentingProducts operator|(InstrumentingProducts lhs, InstrumentingProducts rhs)
{
    return static_cast<InstrumentingProducts>(static_cast<int>(lhs) | static_cast<int>(rhs));
}

constexpr InstrumentingProducts operator&(InstrumentingProducts lhs, InstrumentingProducts rhs)
{
    return static_cast<InstrumentingProducts>(static_cast<int>(lhs) & static_cast<int>(rhs));
}

inline InstrumentingProducts& operator|=(InstrumentingProducts& lhs, InstrumentingProducts rhs)
{
    lhs = lhs | rhs;
    return lhs;
}

inline InstrumentingProducts& operator&=(InstrumentingProducts& lhs, InstrumentingProducts rhs)
{
    lhs = lhs & rhs;
    return lhs;
}
} // namespace trace

#endif