﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace WaveGenerator
{
    public class SoundGenerator
    {
        private long _generatedSampleCount = 0;
        private DataChunk _data = new DataChunk();
        private HeaderChunk _header;
        private FormatChunk _format;

        private uint _sampleRate;
        private ushort _bitPerSample;
        private ushort _channels;

        private long lastDataChunkPosition = 0;
        Stream _file;

        public SoundGenerator(uint sampleRate, ushort bitPerSample, ushort channels, Stream file)
        {
            this._sampleRate = sampleRate;
            this._bitPerSample = bitPerSample;
            this._channels = channels;
            this._file = file;
        }

        // public double phase = 0;
        double lastSin = 0d;
        bool directionUp = true;



        public double[] AddComplexTone(bool fade, double duration, double[] startPhases, params double[] frequencies)
        {           
            long sampleCount = (long)Math.Floor(duration * _sampleRate / 1000d);
            _generatedSampleCount += sampleCount;
            double amplitude = Math.Pow(2, _bitPerSample - 1) - 1;
            double radPerSample = 2 * Math.PI / _sampleRate;
            double[] combined = new double[sampleCount];
            for (uint i = 0; i < sampleCount; i++)
            {
                double sin = 0;
                for (int f = 0; f < frequencies.Length; f++)
                {
                    sin += Math.Sin(frequencies[f] * i * radPerSample + startPhases[f]);
                }
                sin = sin / frequencies.Length * amplitude*(fade?1-(0.8/sampleCount)*(Math.Abs(-sampleCount+i*2)):1);
                byte[] sinBytes = ConvertNumber((long)sin, (byte)_bitPerSample);
                for (int channel = 0; channel < _channels; channel++)
                {
                    _data.AddSamples(sinBytes);
                }
                   
            }           
            double[] lastPhases = new double[frequencies.Length];
            double sint = 0;
            double cost = 0;
            for (int f = 0; f < frequencies.Length; f++)
            {
                lastPhases[f] = Math.Sin(frequencies[f] * sampleCount * radPerSample + startPhases[f]);
                cost = Math.Cos(frequencies[f] * sampleCount * radPerSample + startPhases[f]);
                if (cost > 0 || sint == -1)
                    lastPhases[f] = Math.Asin(lastPhases[f]);
                else
                    lastPhases[f] = -Math.Asin(lastPhases[f]) + Math.PI;
            }
            return lastPhases;           
        }

        private double Max(double[] values)
        {
            double result = Math.Abs(values[0]);
            foreach (double value in values)
                if (Math.Abs(value) > result)
                    result = Math.Abs(value);
            return result;                
        }

        public double AddSimpleTone(double frequency, double duration, double startPhase)
        {
            double lastPhase = 0;
            long sampleCount = (long)Math.Floor(duration * _sampleRate / 1000d);
            _generatedSampleCount += sampleCount;
            double amplitude = Math.Pow(2, _bitPerSample - 1) - 1;
            double radPerSample = 2 * Math.PI / _sampleRate;
            for (uint i = 0; i < sampleCount; i++)
            {
                double sin = amplitude * Math.Sin(frequency * i * radPerSample + startPhase);
                byte[] sinBytes = ConvertNumber((long)sin, (byte)_bitPerSample);
                for (int channel = 0; channel < _channels; channel++)
                {
                    _data.AddSamples(sinBytes);
                }
            }
            double g = Math.Asin(2);
            lastPhase = (sampleCount % this._sampleRate) * radPerSample * frequency + startPhase;
            double sint = Math.Sin(lastPhase);
            double cost = Math.Cos(lastPhase);
            if (cost > 0 || sint == -1)
                lastPhase = Math.Asin(sint);
            else
                lastPhase = -Math.Asin(sint) + Math.PI;
            return lastPhase;
        }

        public void AddTone(double frequency, double duration)
        {
            long sampleCount = (long)Math.Floor(duration * _sampleRate / 1000d);
            _generatedSampleCount += sampleCount;

            double amplitude = Math.Pow(2, _bitPerSample - 1) - 1;
            double radPerSample = 2 * Math.PI / _sampleRate;
            double shift = this.lastSin;
            if (this.directionUp)
                shift = Math.Asin(shift);
            else
                shift = -Math.Asin(shift) + Math.PI;
            if (_file != null)
            {
                DataChunk uncompleted = new DataChunk();
                for (uint i = 0; i < sampleCount; i++)
                {
                    double sin = Math.Sin(i * radPerSample * frequency + shift);
                    sin = sin * amplitude;
                    byte[] sinBytes = ConvertNumber((int)sin, (byte)_bitPerSample);
                    for (int channel = 0; channel < _channels; channel++)
                    {
                        uncompleted.AddSamples(sinBytes);
                    }
                }
                //Сбрасывание промежуточных результатов
                _file.Position = 0;
                SaveHeadersToFile(_file);
                byte[] uncompleteDataBytes = uncompleted.GetSampleBytes();
                uncompleted.ChangeSize(BitConverter.GetBytes((uint)(_bitPerSample / 8 * (_generatedSampleCount * _channels))));
                byte[] uncompletedDataHeaderBytes = uncompleted.GetHeaderBytes();
                _file.Write(uncompletedDataHeaderBytes, 0, uncompletedDataHeaderBytes.Length);
                long headersEnd = _file.Position;
                _file.Position += lastDataChunkPosition;
                _file.Write(uncompleteDataBytes, 0, uncompleteDataBytes.Length);
                lastDataChunkPosition = _file.Position - headersEnd;
                //padByte
                if ((_generatedSampleCount * _channels * (_bitPerSample / 8)) % 2 != 0)
                {
                    _file.Write(new byte[] { 0 }, 0, 1);
                }
                _file.Flush();
            }
            else
            {
                for (uint i = 0; i < sampleCount; i++)
                {
                    double sin = Math.Sin(i * radPerSample * frequency + shift);
                    sin = sin * amplitude;
                    byte[] sinBytes = ConvertNumber((int)sin, (byte)_bitPerSample);
                    for (int channel = 0; channel < _channels; channel++)
                    {
                        _data.AddSamples(sinBytes);
                    }
                }
            }
            this.lastSin = Math.Sin(sampleCount * radPerSample * frequency + shift);
            double phase = (duration / 1000 + shift / (2 * Math.PI) / frequency) * frequency;
            phase -= (int)(phase);
            if (phase >= 0.25 && phase < 0.75)
            {
                this.directionUp = false;
            }
            else
            {
                this.directionUp = true;
            }
        }

        //public void AddTone(double frequency, double duration)
        //{
        //    long sampleCount = (long)Math.Floor(duration * _sampleRate / 1000d);
        //    _generatedSampleCount += sampleCount;

        //    double amplitude = Math.Pow(2, _bitPerSample-1) - 1;
        //    double radPerSample = 2 * Math.PI / _sampleRate;
        //    if (_file != null)
        //    {
        //        DataChunk uncompleted = new DataChunk();
        //        for (uint i = 0; i < sampleCount; i++)
        //        {
        //            double sin = amplitude * Math.Sin(phase);
        //            phase += frequency * radPerSample;
        //            byte[] sinBytes = ConvertNumber(sin, (byte)_bitPerSample);
        //            for (int channel = 0; channel < _channels; channel++)
        //            {
        //                uncompleted.AddSamples(sinBytes);
        //            }
        //        }
        //        //Сбрасывание промежуточных результатов
        //        _file.Position = 0;
        //        SaveHeadersToFile(_file);
        //        byte[] uncompleteDataBytes = uncompleted.GetSampleBytes();
        //        uncompleted.ChangeSize(BitConverter.GetBytes((uint)(_bitPerSample / 8 * (_generatedSampleCount * _channels))));
        //        byte[] uncompletedDataHeaderBytes = uncompleted.GetHeaderBytes();
        //        _file.Write(uncompletedDataHeaderBytes, 0, uncompletedDataHeaderBytes.Length);
        //        long headersEnd = _file.Position;
        //        _file.Position += lastDataChunkPosition;
        //        _file.Write(uncompleteDataBytes, 0, uncompleteDataBytes.Length);
        //        lastDataChunkPosition = _file.Position - headersEnd;
        //        //padByte
        //        if ((_generatedSampleCount * _channels * (_bitPerSample / 8)) % 2 != 0)
        //        {
        //            _file.Write(new byte[] { 0 }, 0, 1);
        //        }
        //        _file.Flush();
        //    }
        //    else
        //    {
        //        for (uint i = 0; i < sampleCount; i++)
        //        {
        //            double sin = amplitude * Math.Sin(phase);
        //            phase += frequency * radPerSample;
        //            byte[] sinBytes = ConvertNumber(sin, (byte)_bitPerSample);
        //            for (int channel = 0; channel < _channels; channel++)
        //            {
        //                data.AddSamples(sinBytes);
        //            }
        //        }
        //    }
        //}

        public byte[] ConvertNumber(long number, byte bit)
        {
            byte[] fullNumber = BitConverter.GetBytes(number);

            byte[] result = new byte[bit / 8];
            //It bit depth is 8
            if (bit == 8)
            {
                sbyte signed = Convert.ToSByte(number);
                byte unsigned = 0;
                unsigned = (byte)(127 + signed);
                result[0] = unsigned;
                return result;
            }
            for (int i = 0; i < bit / 8; i++)
            {
                result[i] = fullNumber[i];
            }
            return result;
        }

        public void SaveTo(Stream stream)
        {
            _header = new HeaderChunk(0);
            _format = new FormatChunk(_sampleRate, _channels, _bitPerSample);
            uint fileSize = (uint)(_header.Size + _format.Size + _data.Size);
            //Check if there's the pad byte
            if ((_generatedSampleCount * _channels * (_bitPerSample / 8)) % 2 != 0)
            {
                fileSize += 1;
            }
            _header.ChangeSize(BitConverter.GetBytes(fileSize));
            byte[] headerbytes = _header.GetChunkBytes();
            byte[] formatBytes = _format.GetChunkBytes();
            byte[] dataBytes = _data.GetChunkBytes();
            stream.Write(headerbytes, 0, headerbytes.Length);
            stream.Write(formatBytes, 0, formatBytes.Length);
            stream.Write(dataBytes, 0, dataBytes.Length);
        }

        private void SaveHeadersToFile(Stream file)
        {
            file.Position = 0;
            _header = new HeaderChunk(0);
            _format = new FormatChunk(_sampleRate, _channels, _bitPerSample);
            uint fileSize = (uint)(_header.Size + _format.Size + 8 + _channels * (_bitPerSample / 8) * _generatedSampleCount);
            //Check if there's the pad byte
            if ((_generatedSampleCount * _channels * (_bitPerSample / 8)) % 2 != 0)
            {
                fileSize += 1;
            }
            _header.ChangeSize(BitConverter.GetBytes(fileSize));
            byte[] headerbytes = _header.GetChunkBytes();
            byte[] formatBytes = _format.GetChunkBytes();
            file.Write(headerbytes, 0, headerbytes.Length);
            file.Write(formatBytes, 0, formatBytes.Length);
        }
    }
}
