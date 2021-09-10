using global::MassTransit.EntityFrameworkCoreIntegration;

namespace ServiceBus.Minimal.MassTransit.Components
{
    public class CustomSqlLockStatementProvider :
        SqlLockStatementProvider
    {
        const string DefaultSchemaName = "dbo";

        public CustomSqlLockStatementProvider(string lockStatement)
            : base(DefaultSchemaName, lockStatement)
        {
        }
    }
}
