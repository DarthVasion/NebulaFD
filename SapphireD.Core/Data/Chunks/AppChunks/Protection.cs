﻿using SapphireD.Core.Memory;

namespace SapphireD.Core.Data.Chunks.AppChunks
{
    public class Protection : DataChunk
    {
        public Protection()
        {
            ChunkName = "Protection";
            ChunkID = 0x2242;
        }

        public override void ReadCCN(ByteReader reader, params object[] extraInfo)
        {
            base.ReadCCN(reader, extraInfo);

            SapDCore.PackageData.Protection = Value;
        }

        public override void ReadMFA(ByteReader reader, params object[] extraInfo)
        {

        }

        public override void WriteCCN(ByteWriter writer, params object[] extraInfo)
        {

        }

        public override void WriteMFA(ByteWriter writer, params object[] extraInfo)
        {

        }
    }
}
