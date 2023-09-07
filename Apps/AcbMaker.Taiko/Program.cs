using DereTore.Apps.AcbMaker.Taiko;
using DereTore.Exchange.Archive.ACB;
using DereTore.Exchange.Archive.ACB.Serialization;
using DereTore.Exchange.Audio.HCA;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VGAudio.Codecs.CriHca;
using VGAudio.Containers.Hca;
using VGAudio.Containers.Wave;
using VGAudio.Formats;

namespace DereTore.Apps.AcbMaker
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //var testacb = AcbFile.FromFile("SONG_GIOSTO.acb");

            foreach (string arg in args)
            {
                string inputFile = arg;
                string songname = Path.GetFileNameWithoutExtension(inputFile);
                string extension = Path.GetExtension(inputFile);

                if (extension == ".wav")
                {
                    string outputFile = songname + ".acb";
                    string tempFile = songname + ".hca";
                    string tempFile_dec = songname + "_dec.hca";

                    var waveReader = new WaveReader();
                    AudioData audioData;

                    using (var fileStream = File.Open(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        audioData = waveReader.Read(fileStream);
                    }

                    var hcaWriter = new HcaWriter();

                    hcaWriter.Configuration.Quality = CriHcaQuality.High;
                    hcaWriter.Configuration.LimitBitrate = false;
                    // Encryption keys are not set here, so the output HCA will always be type 0.

                    var fileData = hcaWriter.GetFile(audioData);

                    using (var fileStream = File.Open(tempFile_dec, FileMode.Create, FileAccess.Write, FileShare.Write))
                    {
                        fileStream.Write(fileData, 0, fileData.Length);
                    }

                    CipherConfig ccFrom = new CipherConfig(), ccTo = new CipherConfig();
                    Random rnd = new Random();

                    ccTo.CipherType = (CipherType)56;

                    ccTo.Key1 = uint.Parse("36327ee6", NumberStyles.HexNumber);
                
                    ccTo.Key2 = uint.Parse("00baa8af", NumberStyles.HexNumber);
                
                    ccTo.KeyModifier = ushort.Parse("0000", NumberStyles.HexNumber);

                    using (var inputStream = new FileStream(tempFile_dec, FileMode.Open, FileAccess.Read))
                    {
                        using (var outputStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                        {
                            var converter = new CipherConverter(inputStream, outputStream, ccFrom, ccTo);
                            converter.Convert();
                        }
                    }

                    DecodeParams deckey = DecodeParams.CreateDefault(ccTo.Key1, ccTo.Key2, ccTo.KeyModifier);

                    var header = GetFullTable(tempFile, songname, deckey, songname.ToUpper().Contains("PSONG_"));
                    var table = new[] { header };
                    var serializer = new AcbSerializer();
                    using (var fs = File.Open(outputFile, FileMode.Create, FileAccess.Write))
                    {
                        serializer.Serialize(table, fs);
                    }

                    File.Delete(tempFile_dec);
                    File.Delete(tempFile);
                }
                else if (extension == ".hca")
                {
                    string outputFile = songname + ".acb";
                    string tempFile = songname + ".enc.hca";

                    CipherConfig ccFrom = new CipherConfig(), ccTo = new CipherConfig();
                    Random rnd = new Random();

                    ccTo.CipherType = (CipherType)56;

                    ccTo.Key1 = uint.Parse("36327ee6", NumberStyles.HexNumber);

                    ccTo.Key2 = uint.Parse("00baa8af", NumberStyles.HexNumber);

                    ccTo.KeyModifier = ushort.Parse("0000", NumberStyles.HexNumber);

                    using (var inputStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
                    {
                        using (var outputStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                        {
                            var converter = new CipherConverter(inputStream, outputStream, ccFrom, ccTo);
                            converter.Convert();
                        }
                    }

                    DecodeParams deckey = DecodeParams.CreateDefault(ccTo.Key1, ccTo.Key2, ccTo.KeyModifier);

                    var header = GetFullTable(tempFile, songname, deckey, songname.ToUpper().Contains("PSONG_"));
                    var table = new[] { header };
                    var serializer = new AcbSerializer();
                    using (var fs = File.Open(outputFile, FileMode.Create, FileAccess.Write))
                    {
                        serializer.Serialize(table, fs);
                    }

                    File.Delete(tempFile);
                }
            }
        }

        private static HeaderTable GetFullTable(string hcaFileName, string songName, DecodeParams key, bool isPreview)
        {
            Exchange.Audio.HCA.HcaInfo info;
            uint lengthInSamples;
            float lengthInSeconds;
            using (var fileStream = File.Open(hcaFileName, FileMode.Open, FileAccess.Read))
            {
                var decoder = new OneWayHcaDecoder(fileStream, key);
                info = decoder.HcaInfo;
                lengthInSamples = decoder.LengthInSamples;
                lengthInSeconds = decoder.LengthInSeconds;
            }
            var cue = new[] {
                new CueTable {
                    CueId = 0,
                    ReferenceType = 3,
                    ReferenceIndex = 0,
                    UserData = string.Empty,
                    WorkSize = 0,
                    AisacControlMap = null,
                    Length = (uint)(lengthInSeconds * 1000),
                    NumAisacControlMaps = 0,
                    HeaderVisibility = 1
               }
            };
            var cueName = new[] {
                new CueNameTable {
                    CueIndex = 0,
                    CueName = songName
                }
            };
            var waveform = new[] {
                new WaveformTable {
                    MemoryAwbId = 0,
                    EncodeType = 2, // HCA
                    Streaming = 0,
                    NumChannels = (byte)info.ChannelCount,
                    LoopFlag = 1,
                    SamplingRate = (ushort)info.SamplingRate,
                    NumSamples = lengthInSamples,
                    ExtensionData = ushort.MaxValue
               }
            };
            var synth = new[] {
                new SynthTable {
                    Type = 0,
                    VoiceLimitGroupName = string.Empty,
                    CommandIndex = ushort.MaxValue,
                    ReferenceItems = new byte[] {0x00, 0x01, 0x00, 0x00},
                    LocalAisacs = null,
                    GlobalAisacStartIndex = ushort.MaxValue,
                    GlobalAisacNumRefs = 0,
                    ControlWorkArea1 = 0,
                    ControlWorkArea2 = 0,
                    TrackValues = null,
                    ParameterPallet = ushort.MaxValue,
                    ActionTrackStartIndex = ushort.MaxValue,
                    NumActionTracks = 0
                }
            };
            var command = new[] {
                new CommandTable {
                    Command = new byte[0x0a] {0x07, 0xd0, 0x04, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00 }
                },
                new CommandTable {
                    Command = new byte[0x10] {0x00, 0x41, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0x57, 0x02, 0x00, 0x32 }
                }
            };
            var track = new[] {
                new TrackTable {
                    EventIndex = 0,
                    CommandIndex = ushort.MaxValue,
                    LocalAisacs = null,
                    GlobalAisacStartIndex = ushort.MaxValue,
                    GlobalAisacNumRefs = 0,
                    ParameterPallet = ushort.MaxValue,
                    TargetType = 0,
                    TargetName = string.Empty,
                    TargetId = uint.MaxValue,
                    TargetAcbName = string.Empty,
                    Scope = 0,
                    TargetTrackNo = ushort.MaxValue
                }
            };
            var sequence = new[] {
                new SequenceTable {
                    PlaybackRatio = 100,
                    NumTracks = 1,
                    TrackIndex = new byte[] {0x00, 0x00}, // {0x01, 0x00}
                    CommandIndex = 1,
                    LocalAisacs = null,
                    GlobalAisacStartIndex = ushort.MaxValue,
                    GlobalAisacNumRefs = 0,
                    ParameterPallet = ushort.MaxValue,
                    ActionTrackStartIndex = ushort.MaxValue,
                    NumActionTracks = 0,
                    TrackValues = null,
                    Type = 0,
                    ControlWorkArea1 = 0,
                    ControlWorkArea2 = 0
                }
            };
            var acfReference_Song = new[] {
                new AcfReferenceTable {
                    Type = 3,
                    Name = "Master",
                    Name2 = null,
                    Id = 0
                },
                new AcfReferenceTable {
                    Type = 3,
                    Name = "Song",
                    Name2 = null,
                    Id = 4
                },
                new AcfReferenceTable {
                    Type = 9,
                    Name = "MasterOut",
                    Name2 = null,
                    Id = 0
                },
                new AcfReferenceTable {
                    Type = 9,
                    Name = "",
                    Name2 = null,
                    Id = 0
                }
            };
            var acfReference_Pre = new[] {
                new AcfReferenceTable {
                    Type = 3,
                    Name = "Master",
                    Name2 = null,
                    Id = 0
                },
                new AcfReferenceTable {
                    Type = 3,
                    Name = "PreSong",
                    Name2 = null,
                    Id = 5
                },
                new AcfReferenceTable {
                    Type = 9,
                    Name = "MasterOut",
                    Name2 = null,
                    Id = 0
                },
                new AcfReferenceTable {
                    Type = 9,
                    Name = "",
                    Name2 = null,
                    Id = 0
                }
            };
            var acbGuid = Guid.NewGuid();
            var hcaData = File.ReadAllBytes(hcaFileName);
            var header = new HeaderTable
            {
                FileIdentifier = 0,
                Size = 0,
                Version = 0x01230100,
                Type = 0,
                Target = 0,
                AcfMd5Hash = new byte[0x10] { 67,204,211,119,189,75,40,163,179,110,237,156,133,115,204,217 },
                CategoryExtension = 0,
                CueTable = cue,
                CueNameTable = cueName,
                WaveformTable = waveform,
                AisacTable = null,
                GraphTable = null,
                AisacNameTable = null,
                GlobalAisacReferenceTable = null,
                SynthTable = synth,
                SeqCommandTable = command,
                TrackTable = track,
                SequenceTable = sequence,
                AisacControlNameTable = null,
                AutoModulationTable = null,
                StreamAwbTocWorkOld = null,
                AwbFile = hcaData,
                VersionString = StandardAcbVersionString,
                CueLimitWorkTable = null,
                NumCueLimitListWorks = 0,
                NumCueLimitNodeWorks = 0,
                AcbGuid = acbGuid.ToByteArray(),
                StreamAwbHash = new byte[0x10],
                StreamAwbTocWork_Old = null,
                AcbVolume = 1f,
                StringValueTable = null,
                OutsideLinkTable = null,
                BlockSequenceTable = null,
                BlockTable = null,
                Name = songName,
                CharacterEncodingType = 0,
                EventTable = null,
                ActionTrackTable = null,
                AcfReferenceTable = isPreview ? acfReference_Pre : acfReference_Song,
                WaveformExtensionDataTable = null,
                BeatSyncInfoTable = null,
                CuePriorityType = byte.MaxValue,
                NumCueLimit = 0,
                TrackCommandTable = null,
                SynthCommandTable = null,
                TrackEventTable = null,
                SeqParameterPalletTable = null,
                TrackParameterPalletTable = null,
                SynthParameterPalletTable = null,
                SoundGeneratorTable = null,
                ProjectKey = new byte[16] { 156,113,207,136,232,212,0,204,33,216,37,123,25,240,217,80 },
                PaddingArea = null,
                StreamAwbTocWork = null,
                StreamAwbAfs2Header = null,
            };
            return header;
        }

        private static readonly string StandardAcbVersionString = "\nACB Format/PC Ver.1.32.01 Build:\n";
        private static readonly string DefaultSongName = "SONG_AAAAAA";
    }
}
