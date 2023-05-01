using System;
using System.Collections.Generic;
using System.IO;

namespace StorybrewScripts
{
    internal unsafe class MidiFile
    {
        internal readonly int Format, TicksPerQuarterNote, TracksCount;
        internal readonly MidiTrack[] Tracks;

        internal MidiFile(string path) : this(File.ReadAllBytes(path)) {}
        internal MidiFile(byte[] data)
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
                for (var i = 0; i < TracksCount; ++i) Tracks[i] = ParseTrack(i, pData, ref position);
            }
        }

        static bool ParseMetaEvent(byte* data, ref int i, byte metaEventType, out byte data1, out byte data2)
        {
            switch (metaEventType)
            {
                case (byte)MetaEventType.Tempo:
                    var mspqn = (data[++i] << 16) | (data[++i] << 8) | data[++i];
                    data1 = (byte)(60000000f / mspqn);
                    data2 = 0;
                    ++i;
                    return true;

                case (byte)MetaEventType.TimeSignature:
                    data1 = data[++i];
                    data2 = (byte)Math.Pow(2, data[++i]);
                    i += 3;
                    return true;

                case (byte)MetaEventType.KeySignature:
                    data1 = data[++i];
                    data2 = data[++i];
                    ++i;
                    return true;

                default:
                    var length = Reader.ReadVarInt(data, ref i);
                    i += length;
                    data1 = 0;
                    data2 = 0;
                    return false;
            }
        }
        static MidiTrack ParseTrack(int index, byte* data, ref int position)
        {
            if (Reader.ReadString(data, ref position, 4) != "MTrk") throw new FormatException("Invalid track header (expected MTrk)");

            var trackLength = Reader.Read32(data, ref position);
            var trackEnd = position + trackLength;

            var track = new MidiTrack(index);
            var time = 0;
            byte status = 0;

            while (position < trackEnd)
            {
                time += Reader.ReadVarInt(data, ref position);
                var peekByte = data[position];

                if ((peekByte & (byte)MidiEventType.NoteOff) != 0)
                {
                    status = peekByte;
                    ++position;
                }

                if ((status & 0xF0) != 0xF0)
                {
                    var type = (byte)(status & 0xF0);
                    var channel = (byte)((status & 0x0F) + 1);

                    var arg2 = data[position++];
                    var arg3 = (type & (byte)MidiEventType.PitchBendChange) != (byte)MidiEventType.ProgramChange ?
                        data[position++] : (byte)0;

                    if (type == (byte)MidiEventType.NoteOn && arg3 == 0) type = (byte)MidiEventType.NoteOff;

                    track.MidiEvents.Add(new MidiEvent(time, type, channel, arg2, arg3));
                }
                else
                {
                    if (status == (byte)MidiEventType.MetaEvent)
                    {
                        var metaType = Reader.Read8(data, ref position);
                        if (metaType >= 0x01 && metaType <= 0x0F) track.TextEvents.Add(new TextEvent(time, metaType,
                            Reader.ReadString(data, ref position, Reader.ReadVarInt(data, ref position))));

                        else if (ParseMetaEvent(data, ref position, metaType, out byte arg2, out byte arg3))
                            track.MidiEvents.Add(new MidiEvent(time, status, metaType, arg2, arg3));
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
    }

    static unsafe class Reader
    {
        internal static short Read16(byte* data, ref int i)
            => (short)((data[i++] << 8) | data[i++]);

        internal static int Read32(byte* data, ref int i)
            => (data[i++] << 24) | (data[i++] << 16) | (data[i++] << 8) | data[i++];

        internal static byte Read8(byte* data, ref int i)
            => data[i++];

        internal static string ReadString(byte* data, ref int i, int length)
        {
            var result = new string((sbyte*)data, i, length);
            i += length;
            return result;
        }

        internal static int ReadVarInt(byte* data, ref int i)
        {
            var p = data + i;
            int result = *p++;
            if ((result & (byte)MidiEventType.NoteOff) == 0)
            {
                i = (int)(p - data);
                return result;
            }
            result &= 0x7F;

            for (var j = 0; j < 3; ++j)
            {
                int value = *p++;
                result = (result << 7) | (value & 0x7F);
                if ((value & (byte)MidiEventType.NoteOff) == 0) break;
            }

            i = (int)(p - data);
            return result;
        }
    }

    internal class MidiTrack
    {
        internal int Index;
        internal HashSet<MidiEvent> MidiEvents;
        internal HashSet<TextEvent> TextEvents;

        internal MidiTrack(int index)
        {
            Index = index;
            MidiEvents = new HashSet<MidiEvent>();
            TextEvents = new HashSet<TextEvent>();
        }
    }

    internal struct MidiEvent
    {
        internal int Time;
        byte Type, Arg1, Arg2, Arg3;

        internal MidiEventType MidiEventType => (MidiEventType)Type;
        internal MetaEventType MetaEventType => (MetaEventType)Arg1;

        internal int Channel => Arg1;
        internal int Note => Arg2;
        internal int Velocity => Arg3;
        internal int Value => Arg3;

        internal MidiEvent(int time, byte type, byte arg1, byte arg2, byte arg3)
        {
            Time = time;
            Type = type;
            Arg1 = arg1;
            Arg2 = arg2;
            Arg3 = arg3;
        }
    }

    internal struct TextEvent
    {
        internal int Time;
        internal byte Type;
        internal string Value;

        internal TextEventType TextEventType => (TextEventType)this.Type;

        internal TextEvent(int time, byte type, string value)
        {
            Time = time;
            Type = type;
            Value = value;
        }
    }

    internal enum MidiEventType : byte
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

    internal enum TextEventType : byte
    {
        Text = 0x01,
        TrackName = 0x03,
        Lyric = 0x05
    }

    internal enum MetaEventType : byte
    {
        Tempo = 0x51,
        TimeSignature = 0x58,
        KeySignature = 0x59
    }

    internal enum NoteName
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