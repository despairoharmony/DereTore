using DereTore.Exchange.Archive.ACB.Serialization;

namespace DereTore.Apps.AcbMaker.Taiko {
    public sealed class CommandTable : UtfRowBase {

        [UtfField(0)]
        public byte[] Command;

    }
}
