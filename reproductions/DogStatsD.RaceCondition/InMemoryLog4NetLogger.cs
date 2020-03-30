using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

namespace DogStatsD.RaceCondition
{
    public class InMemoryLog4NetLogger
    {
        public static MemoryAppender InMemoryAppender;

        public static void Setup()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository(typeof(Logger).Assembly);

            var patternLayout = new PatternLayout();
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
