using System;
using System.Collections;
using System.Data.Common;
using System.Linq;

namespace Samples.FakeDbCommand
{
    public class FakeParameterCollection : DbParameterCollection
    {
        public override int Add(object value) => 0;

        public override bool Contains(object value) => false;

        public override void Clear()
        {
        }

        public override int IndexOf(object value) => -1;

        public override void Insert(int index, object value)
        {
        }

        public override void Remove(object value)
        {
        }

        public override void RemoveAt(int index)
        {
        }

        public override void RemoveAt(string parameterName)
        {
        }

        protected override void SetParameter(int index, DbParameter value)
        {
        }

        protected override void SetParameter(string parameterName, DbParameter value)
        {
        }

        public override int Count { get; }
        
        public override object SyncRoot { get; }

        public override int IndexOf(string parameterName) => -1;

        public override IEnumerator GetEnumerator() => Enumerable.Empty<DbParameter>().GetEnumerator();

        protected override DbParameter GetParameter(int index) => null;

        protected override DbParameter GetParameter(string parameterName) => null;

        public override bool Contains(string value) => false;

        public override void CopyTo(Array array, int index)
        {
        }

        public override void AddRange(Array values)
        {
        }
    }
}
