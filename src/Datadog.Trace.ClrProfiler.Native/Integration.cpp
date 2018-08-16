#include "Integration.h"

const integration aspnet_mvc5_integration
    = ::integration(
                    // integration_type
                    IntegrationType_AspNet_Mvc5,
                    // integration_name
                    L"aspNetMvc5",
                    {
                        method_replacement(
                                           // caller
                                           method_reference(
                                                            // assembly
                                                            L"System.Web.Mvc",
                                                            // type
                                                            L"",
                                                            // method name
                                                            L"",
                                                            // method signature
                                                            {}),
                                           // target
                                           method_reference(
                                                            // assembly
                                                            L"System.Web.Mvc",
                                                            // type
                                                            L"System.Web.Mvc.Async.IAsyncActionInvoker",
                                                            // method name
                                                            L"BeginInvokeAction",
                                                            // method signature
                                                            {}),
                                           // wrapper
                                           method_reference(
                                                            // assembly
                                                            L"Datadog.Trace.ClrProfiler.Managed",
                                                            // type
                                                            L"Datadog.Trace.ClrProfiler.Integrations.AspNetMvc5Integration",
                                                            // method name
                                                            L"BeginInvokeAction",
                                                            // method signature
                                                            {
                                                                // calling convention
                                                                IMAGE_CEE_CS_CALLCONV_DEFAULT,
                                                                // parameter count
                                                                0x05,
                                                                // return type
                                                                ELEMENT_TYPE_OBJECT,
                                                                // parameter types
                                                                ELEMENT_TYPE_OBJECT,
                                                                ELEMENT_TYPE_OBJECT,
                                                                ELEMENT_TYPE_OBJECT,
                                                                ELEMENT_TYPE_OBJECT,
                                                                ELEMENT_TYPE_OBJECT,
                                                            })
                                          ),
                        method_replacement(
                                           // caller method
                                           method_reference(
                                                            // assembly
                                                            L"System.Web.Mvc",
                                                            // type
                                                            L"",
                                                            // method name
                                                            L"",
                                                            // method signature
                                                            {}),
                                           // target method
                                           method_reference(
                                                            // assembly
                                                            L"System.Web.Mvc",
                                                            // type
                                                            L"System.Web.Mvc.Async.IAsyncActionInvoker",
                                                            // method name
                                                            L"EndInvokeAction",
                                                            // method signature
                                                            {}),
                                           // wrapper method
                                           method_reference(
                                                            // assembly
                                                            L"Datadog.Trace.ClrProfiler.Managed",
                                                            // type
                                                            L"Datadog.Trace.ClrProfiler.Integrations.AspNetMvc5Integration",
                                                            // method name
                                                            L"EndInvokeAction",
                                                            // method signature
                                                            {
                                                                // calling convention
                                                                IMAGE_CEE_CS_CALLCONV_DEFAULT,
                                                                // parameter count
                                                                0x02,
                                                                // return type
                                                                ELEMENT_TYPE_BOOLEAN,
                                                                // parameter types
                                                                ELEMENT_TYPE_OBJECT,
                                                                ELEMENT_TYPE_OBJECT,
                                                            })
                                          )
                    });

const integration aspnetcore_mvc2_integration
    = ::integration(
                    // integration_type
                    IntegrationType_AspNetCore_Mvc2,
                    // integration_name
                    L"aspNetCoreMvc2",
                    {
                        method_replacement(
                                           // caller
                                           method_reference(
                                                            // assembly
                                                            L"Microsoft.AspNetCore.Mvc.Core",
                                                            // type
                                                            L"Microsoft.AspNetCore.Mvc.Internal.ResourceInvoker",
                                                            // method name
                                                            L"",
                                                            // method signature
                                                            {}),
                                           // target
                                           method_reference(
                                                            // assembly
                                                            L"Microsoft.AspNetCore.Mvc.Core",
                                                            // type
                                                            L"Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions",
                                                            // method name
                                                            L"BeforeAction",
                                                            // method signature
                                                            {}),
                                           // wrapper
                                           method_reference(
                                                            // assembly
                                                            L"Datadog.Trace.ClrProfiler.Managed",
                                                            // type
                                                            L"Datadog.Trace.ClrProfiler.Integrations.AspNetCoreMvc2Integration",
                                                            // method name
                                                            L"BeforeAction",
                                                            // method signature
                                                            {
                                                                // calling convention
                                                                IMAGE_CEE_CS_CALLCONV_DEFAULT,
                                                                // parameter count
                                                                0x04,
                                                                // return type
                                                                ELEMENT_TYPE_OBJECT,
                                                                // parameter types
                                                                ELEMENT_TYPE_OBJECT,
                                                                ELEMENT_TYPE_OBJECT,
                                                                ELEMENT_TYPE_OBJECT,
                                                                ELEMENT_TYPE_OBJECT,
                                                            })
                                          ),
                        method_replacement(
                                           // caller method
                                           method_reference(
                                                            // assembly
                                                            L"Microsoft.AspNetCore.Mvc.Core",
                                                            // type
                                                            L"Microsoft.AspNetCore.Mvc.Internal.ResourceInvoker",
                                                            // method name
                                                            L"",
                                                            // method signature
                                                            {}),
                                           // target method
                                           method_reference(
                                                            // assembly
                                                            L"Microsoft.AspNetCore.Mvc.Core",
                                                            // type
                                                            L"Microsoft.AspNetCore.Mvc.Internal.MvcCoreDiagnosticSourceExtensions",
                                                            // method name
                                                            L"AfterAction",
                                                            // method signature
                                                            {}),
                                           // wrapper method
                                           method_reference(
                                                            // assembly
                                                            L"Datadog.Trace.ClrProfiler.Managed",
                                                            // type
                                                            L"Datadog.Trace.ClrProfiler.Integrations.AspNetCoreMvc2Integration",
                                                            // method name
                                                            L"AfterAction",
                                                            // method signature
                                                            {
                                                                // calling convention
                                                                IMAGE_CEE_CS_CALLCONV_DEFAULT,
                                                                // parameter count
                                                                0x04,
                                                                // return type
                                                                ELEMENT_TYPE_VOID,
                                                                // parameter types
                                                                ELEMENT_TYPE_OBJECT,
                                                                ELEMENT_TYPE_OBJECT,
                                                                ELEMENT_TYPE_OBJECT,
                                                                ELEMENT_TYPE_OBJECT,
                                                            })
                                          )
                    });

const std::vector<integration> all_integrations = {
    aspnet_mvc5_integration,
    aspnetcore_mvc2_integration,
};
