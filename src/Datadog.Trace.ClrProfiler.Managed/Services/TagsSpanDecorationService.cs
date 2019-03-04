using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.Services
{
    internal class TagsSpanDecorationService : ISpanDecorationService
    {
        private TagsSpanDecorationService()
        {
        }

        public static ISpanDecorationService Instance { get; } = new TagsSpanDecorationService();

        public void Decorate(ISpan span, ISpanDecorationSource with)
        {
            var tags = with.GetTags();

            if (tags == null)
            {
                return;
            }

            foreach (var tag in tags)
            {
                span.SetTag(tag.Key, tag.Value);
            }
        }
    }
}
