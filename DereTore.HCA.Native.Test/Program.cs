﻿using System;
using System.Media;

namespace DereTore.HCA.Native.Test {
    public static class Program {

        public static void Main(string[] args) {
            uint key1 = 0, key2 = 0;
            if (args.Length < 1) {
                Console.WriteLine("Usage: <EXE> <File to play> [key1] [key2]");
                return;
            }

            var fileName = args[0];
#if false
            key1 = CgssCipher.Key1;
            key2 = CgssCipher.Key2;
#else
            if (args.Length >= 3) {
                key1 = uint.Parse(args[1]);
                key2 = uint.Parse(args[2]);
            }
#endif

            using (var hca = new HcaAudioStream(fileName, key1, key2)) {
                using (var sp = new SoundPlayer(hca)) {
                    sp.PlaySync();
                }
            }
        }

    }
}
