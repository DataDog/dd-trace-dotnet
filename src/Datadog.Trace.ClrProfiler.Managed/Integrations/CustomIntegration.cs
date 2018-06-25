using System;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The custom integration which can instrument arbitrary method through configuration.
    /// </summary>
    public class CustomIntegration : Integration
    {
        private readonly string _operationName;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomIntegration"/> class.
        /// </summary>
        /// <param name="names">The names of the instrumented method, and its containing type and assembly.</param>
        public CustomIntegration(MetadataNames names)
        {
            _operationName = $"[{names.ModuleName}]{names.TypeName}.{names.MethodName}";
            Console.WriteLine($"Entering {_operationName}()");

            Scope.Span.OperationName = _operationName;
            Scope.Span.ResourceName = string.Empty;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Console.WriteLine($"Exiting {_operationName}()");
            }

            base.Dispose(disposing);
        }
    }
}
