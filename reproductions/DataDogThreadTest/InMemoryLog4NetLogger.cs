using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

namespace DataDogThreadTest
{
    public class InMemoryLog4NetLogger
    {
        public static MemoryAppender InMemoryAppender;

        public static void Setup()
        {
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository(typeof(Logger).Assembly);

            PatternLayout patternLayout = new PatternLayout();
            //patternLayout.ConversionPattern = "%date [%thread] %-5level %logger - %message%newline";
            patternLayout.ConversionPattern = "%message";
            patternLayout.ActivateOptions();

            InMemoryAppender = new MemoryAppender();
            InMemoryAppender.ActivateOptions();
            hierarchy.Root.AddAppender(InMemoryAppender);

            hierarchy.Root.Level = Level.Info;
            hierarchy.Configured = true;
        }
    }
}
