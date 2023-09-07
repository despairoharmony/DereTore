using DereTore.Exchange.Archive.ACB.Serialization;

namespace DereTore.Apps.AcbMaker.Taiko {
    [UtfTable("CueName")] // Optional. The 'Table' in 'CueNameTable' is ignored by default.
    public sealed class CueNameTable : UtfRowBase {

        [UtfField(0)]
        public string CueName;
        [UtfField(1)]
        public ushort CueIndex;

    }
}
