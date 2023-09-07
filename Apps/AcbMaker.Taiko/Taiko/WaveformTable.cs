using DereTore.Exchange.Archive.ACB.Serialization;

namespace DereTore.Apps.AcbMaker.Taiko {
    public sealed class WaveformTable : UtfRowBase {

        [UtfField(0)]
        public ushort MemoryAwbId;
        [UtfField(1)]
        public byte EncodeType;
        [UtfField(2)]
        public byte Streaming;
        [UtfField(3)]
        public byte NumChannels;
        [UtfField(4)]
        public byte LoopFlag;
        [UtfField(5)]
        public ushort SamplingRate;
        [UtfField(6)]
        public uint NumSamples;
        [UtfField(7)]
        public ushort ExtensionData;

    }
}
