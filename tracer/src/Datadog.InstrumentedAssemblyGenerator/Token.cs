namespace Datadog.InstrumentedAssemblyGenerator
{
    internal class Token
    {
        private readonly uint _token;
        internal const uint RidMask = 0x00FFFFFF;
        internal const int TableShift = 24;

        internal Token(uint token)
        {
            _token = token;
        }

        internal Token(MetadataTable table, uint rowId)
        {
            _token = ((uint) table << TableShift) | rowId;
        }

        internal uint Raw => _token;
        internal uint RID => _token & RidMask;

        internal bool IsNull => RID == 0;

        internal MetadataTable Table => (MetadataTable) (_token >> TableShift);

        public override bool Equals(object obj)
        {
            var tok = obj as Token;
            return _token.Equals(tok?.Raw);
        }

        public override int GetHashCode()
        {
            return _token.GetHashCode();
        }

        public override string ToString()
        {
            return $"Table: {Table}, RID: {RID:X} ({Raw:x8})";
        }
    }
}