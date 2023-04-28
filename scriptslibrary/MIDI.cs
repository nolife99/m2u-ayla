using System;
using System.Collections.Generic;
using System.IO;

namespace StorybrewScripts
{
    public unsafe class MidiFile
    {
        public readonly int Format, TicksPerQuarterNote, TracksCount;
        public readonly MidiTrack[] Tracks;

        public MidiFile(string path) : this(File.ReadAllBytes(path)) {}
        public MidiFile(byte[] data)
        {
            var position = 0;

            fixed (byte* pData = data)
            {
                if (Reader.ReadString(pData, ref position, 4) != "MThd") throw new FormatException("Invalid file header (expected MThd)");
                if (Reader.Read32(pData, ref position) != 6) throw new FormatException("Invalid header length (expected 6)");

                Format = Reader.Read16(pData, ref position);
                TracksCount = Reader.Read16(pData, ref position);
                TicksPerQuarterNote = Reader.Read16(pData, ref position);

                if ((TicksPerQuarterNote & 0x8000) != 0) throw new FormatException("Invalid timing mode (SMPTE timecode not supported)");

                Tracks = new MidiTrack[TracksCount];
                for (var i = 0; i < TracksCount; i++) Tracks[i] = ParseTrack(i, pData, ref position);
            }
        }

        static bool ParseMetaEvent(byte* data, ref int position, byte metaEventType, ref byte data1, ref byte data2)
        {
            switch (metaEventType)
            {
                case (byte)MetaEventType.Tempo:
                    var mspqn = (*(data + position + 1) << 16) | (*(data + position + 2) << 8) | *(data + position + 3);
                    data1 = (byte)(60000000D / mspqn);
                    position += 4;
                    return true;

                case (byte)MetaEventType.TimeSignature:
                    data1 = *(data + position + 1);
                    data2 = (byte)Math.Pow(2, *(data + position + 2));
                    position += 5;
                    return true;

                case (byte)MetaEventType.KeySignature:
                    data1 = *(data + position + 1);
                    data2 = *(data + position + 2);
                    position += 3;
                    return true;

                default:
                    var length = Reader.ReadVarInt(data, ref position);
                    position += length;
                    return false;
            }
        }
        static MidiTrack ParseTrack(int index, byte* data, ref int position)
        {
            if (Reader.ReadString(data, ref position, 4) != "MTrk") throw new FormatException("Invalid track header (expected MTrk)");

            var trackLength = Reader.Read32(data, ref position);
            var trackEnd = position + trackLength;

            var track = new MidiTrack { Index = index };
            var time = 0;
            var status = (byte)0;

            while (position < trackEnd)
            {
                time += Reader.ReadVarInt(data, ref position);
                var peekByte = *(data + position);

                if ((peekByte & 0x80) != 0)
                {
                    status = peekByte;
                    ++position;
                }

                if ((status & 0xF0) != 0xF0)
                {
                    var eventType = (byte)(status & 0xF0);
                    var channel = (byte)((status & 0x0F) + 1);

                    var data1 = *(data + position++);
                    var data2 = (eventType & 0xE0) != 0xC0 ? *(data + position++) : (byte)0;

                    if (eventType == (byte)MidiEventType.NoteOn && data2 == 0) eventType = (byte)MidiEventType.NoteOff;

                    track.MidiEvents.Add(new MidiEvent 
                        { Time = time, Type = eventType, Arg1 = channel, Arg2 = data1, Arg3 = data2 }
                    );
                }
                else
                {
                    if (status == 0xFF)
                    {
                        var metaEventType = Reader.Read8(data, ref position);

                        if (metaEventType >= 0x01 && metaEventType <= 0x0F)
                        {
                            // Parsing text events isn't needed in this project
                        }
                        else
                        {
                            var data1 = (byte)0;
                            var data2 = (byte)0;

                            if (ParseMetaEvent(data, ref position, metaEventType, ref data1, ref data2))
                                track.MidiEvents.Add(new MidiEvent
                                    { Time = time, Type = status, Arg1 = metaEventType, Arg2 = data1, Arg3 = data2 }
                                );
                        }
                    }
                    else if (status == 0xF0 || status == 0xF7)
                    {
                        var length = Reader.ReadVarInt(data, ref position);
                        position += length;
                    }
                    else ++position;
                }
            }

            return track;
        }

        static class Reader
        {
            public static short Read16(byte* data, ref int i) 
                => (short)unchecked((*(data + i++) << 8) | *(data + i++));
                            
            public static int Read32(byte* data, ref int i) 
                => unchecked((*(data + i++) << 24) | (*(data + i++) << 16) | (*(data + i++) << 8) | *(data + i++));

            public static byte Read8(byte* data, ref int i) 
                => unchecked(*(data + i++));

            public static string ReadString(byte* data, ref int i, int length)
            {
                var result = new string((sbyte*)data, i, length, System.Text.Encoding.ASCII);
                i += length;
                return result;
            }

            public static int ReadVarInt(byte* data, ref int i)
            {
                var p = data + i;
                int result = *p++;
                if ((result & 0x80) == 0)
                {
                    i = (int)(p - data);
                    return result;
                }
                result &= 0x7F;

                for (var j = 0; j < 3; j++)
                {
                    int value = *p++;
                    result = (result << 7) | (value & 0x7F);
                    if ((value & 0x80) == 0) break;
                }

                i = (int)(p - data);
                return unchecked(result);
            }
        }
    }

    public class MidiTrack
    {
        public int Index;
        public List<MidiEvent> MidiEvents = new List<MidiEvent>();
    }

    public struct MidiEvent
    {
        public int Time;
        public byte Type, Arg1, Arg2, Arg3;

        public MidiEventType MidiEventType => (MidiEventType)Type;
        public MetaEventType MetaEventType => (MetaEventType)Arg1;

        public int Channel => Arg1;
        public int Note => Arg2;
        public int Velocity => Arg3;
        public int Value => Arg3;
    }

    public enum MidiEventType : byte
    {
        NoteOff = 0x80,
        NoteOn = 0x90,
        KeyAfterTouch = 0xA0,
        ControlChange = 0xB0,
        ProgramChange = 0xC0,
        ChannelAfterTouch = 0xD0,
        PitchBendChange = 0xE0,
        MetaEvent = 0xFF
    }

    public enum MetaEventType : byte
    {
        Tempo = 0x51,
        TimeSignature = 0x58,
        KeySignature = 0x59
    }

    public enum NoteName
    {
        C = 0,
        CSharp = 1,
        D = 2,
        DSharp = 3,
        E = 4,
        F = 5,
        FSharp = 6,
        G = 7,
        GSharp = 8,
        A = 9,
        ASharp = 10,
        B = 11
    }
}