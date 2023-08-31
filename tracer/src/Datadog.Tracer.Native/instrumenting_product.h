#ifndef DD_CLR_INSTRUMENTING_PRODUCT_H_
#define DD_CLR_INSTRUMENTING_PRODUCT_H_

namespace trace
{
// Matches Datadog.Trace.FaultTolerant.InstrumentingProducts
enum class InstrumentingProducts : int
{
    Tracer = 1,
    DynamicInstrumentation = 2,
    ASM = 4
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