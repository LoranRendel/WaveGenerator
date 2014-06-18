﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace WaveGenerator
{
    class DataChunk : Chunk
    {
        List<byte[]> _samplesBytes = new List<byte[]>();

       // long _allSamplesCount = 0;

        public DataChunk()
            : base("data", 0)
        {

        }

        //public void AddSamples(short[] samples)
        //{
        //    this._samples.Add(samples);
        //    _allSamplesCount += samples.Length;
        //}

        public void AddSamples(byte[] sample)
        {
            _samplesBytes.Add(sample);
            this.byteCount += sample.Length;
        }

        public override byte[] GetChunkBytes()
        {
            int pos = 0;
            byte[] result = new byte[this.byteCount];
            
            this._chunkID.CopyTo(result, pos);
            pos += this._chunkID.Length;
            
            this._chunkDataSize = BitConverter.GetBytes((uint)(_samplesBytes.Count * _samplesBytes[0].Length));           
            this._chunkDataSize.CopyTo(result, pos);            
            pos += this._chunkDataSize.Length;
            
            foreach (byte[] sb in _samplesBytes)
            {
                sb.CopyTo(result, pos);
                pos += sb.Length;
            }
            return result;
        }
    }
}
