using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Tagging
{
    internal class SqlTags : TagsDictionary
    {
        private static readonly Property<SqlTags, string>[] TagsProperties =
        {
            new Property<SqlTags, string>(Tags.DbType, t => t.DbType, (t, v) => t.DbType = v),
            new Property<SqlTags, string>(Tags.InstrumentationName, t => t.InstrumentationName, (t, v) => t.InstrumentationName = v),
            new Property<SqlTags, string>(Tags.DbName, t => t.DbName, (t, v) => t.DbName = v),
            new Property<SqlTags, string>(Tags.DbUser, t => t.DbUser, (t, v) => t.DbUser = v),
            new Property<SqlTags, string>(Tags.OutHost, t => t.OutHost, (t, v) => t.OutHost = v)
        };

        public string DbType { get; set; }

        public string InstrumentationName { get; set; }

        public string DbName { get; set; }

        public string DbUser { get; set; }

        public string OutHost { get; set; }

        protected override IProperty<string>[] GetAdditionalTags()
        {
            return base.GetAdditionalTags();
        }
    }
}
