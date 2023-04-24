using System;
using System.Collections.Generic;
using System.IO;

namespace MIDI
{
    internal class MidiFile
    {
        internal readonly int Format, TicksPerQuarterNote, TracksCount;
        internal readonly MidiTrack[] Tracks;

        internal MidiFile(string path) : this(File.ReadAllBytes(path)) {}
        internal MidiFile(byte[] data)
        {
            var position = 0;

            if (Reader.ReadString(data, ref position, 4) != "MThd") throw new FormatException("Invalid file header (expected MThd)");
            if (Reader.Read32(data, ref position) != 6) throw new FormatException("Invalid header length (expected 6)");

            this.Format = Reader.Read16(data, ref position);
            this.TracksCount = Reader.Read16(data, ref position);
            this.TicksPerQuarterNote = Reader.Read16(data, ref position);

            if ((this.TicksPerQuarterNote & 0x8000) != 0) throw new FormatException("Invalid timing mode (SMPTE timecode not supported)");

            this.Tracks = new MidiTrack[this.TracksCount];
            for (var i = 0; i < this.TracksCount; i++) this.Tracks[i] = ParseTrack(i, data, ref position);
        }

        static bool ParseMetaEvent(byte[] data, ref int position, byte metaEventType, ref byte data1, ref byte data2)
        {
            switch (metaEventType)
            {
                case (byte)MetaEventType.Tempo:
                    var mspqn = (data[position + 1] << 16) | (data[position + 2] << 8) | data[position + 3];
                    data1 = (byte)(60000000D / mspqn);
                    position += 4;
                    return true;

                case (byte)MetaEventType.TimeSignature:
                    data1 = data[position + 1];
                    data2 = (byte)Math.Pow(2, data[position + 2]);
                    position += 5;
                    return true;

                case (byte)MetaEventType.KeySignature:
                    data1 = data[position + 1];
                    data2 = data[position + 2];
                    position += 3;
                    return true;

                default:
                    var length = Reader.ReadVarInt(data, ref position);
                    position += length;
                    return false;
            }
        }
        static MidiTrack ParseTrack(int index, byte[] data, ref int position)
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
                var peekByte = data[position];

                if ((peekByte & 0x80) != 0)
                {
                    status = peekByte;
                    ++position;
                }

                if ((status & 0xF0) != 0xF0)
                {
                    var eventType = (byte)(status & 0xF0);
                    var channel = (byte)((status & 0x0F) + 1);

                    var data1 = data[position++];
                    var data2 = (eventType & 0xE0) != 0xC0 ? data[position++] : (byte)0;

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
                            var textLength = Reader.ReadVarInt(data, ref position);
                            var textValue = Reader.ReadString(data, ref position, textLength);
                            var textEvent = new TextEvent { Time = time, Type = metaEventType, Value = textValue };
                            track.TextEvents.Add(textEvent);
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
            internal unsafe static short Read16(byte[] data, ref int i) 
            {
                fixed (byte* pData = data)
                    return (short)unchecked((*(pData + i++) << 8) | *(pData + i++));
            }
                            
            internal unsafe static int Read32(byte[] data, ref int i) 
            {
                fixed (byte* pData = data) 
                    return unchecked((*(pData + i++) << 24) | (*(pData + i++) << 16) | (*(pData + i++) << 8) | *(pData + i++));
            }

            internal unsafe static byte Read8(byte[] data, ref int i) 
            {
                fixed (byte* pData = data)
                    return unchecked(*(pData + i++));
            }

            internal static unsafe string ReadString(byte[] data, ref int i, int length)
            {
                fixed (byte* pData = data)
                {
                    var result = new string((sbyte*)pData, i, length, System.Text.Encoding.ASCII);
                    i += length;
                    return result;
                }
            }

            internal static unsafe int ReadVarInt(byte[] data, ref int i)
            {
                fixed (byte* pData = data)
                {
                    var p = pData + i;
                    int result = *p++;
                    if ((result & 0x80) == 0)
                    {
                        i = (int)(p - pData);
                        return result;
                    }
                    result &= 0x7F;

                    for (var j = 0; j < 3; j++)
                    {
                        int value = *p++;
                        result = (result << 7) | (value & 0x7F);
                        if ((value & 0x80) == 0) break;
                    }

                    i = (int)(p - pData);
                    return unchecked(result);
                }
            }
        }
    }

    internal class MidiTrack
    {
        internal int Index;
        internal List<MidiEvent> MidiEvents = new List<MidiEvent>();
        internal List<TextEvent> TextEvents = new List<TextEvent>();
    }

    internal struct MidiEvent
    {
        internal int Time;

        internal byte Type, Arg1, Arg2, Arg3;

        internal MidiEventType MidiEventType => (MidiEventType)this.Type;
        internal MetaEventType MetaEventType => (MetaEventType)this.Arg1;

        internal int Channel => this.Arg1;
        internal int Note => this.Arg2;
        internal int Velocity => this.Arg3;

        internal ControlChangeType ControlChangeType => (ControlChangeType)this.Arg2;

        internal int Value => this.Arg3;
    }

    internal struct TextEvent
    {
        internal int Time;
        internal byte Type;
        internal string Value;
        internal TextEventType TextEventType => (TextEventType)this.Type;
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

    internal enum ControlChangeType : byte
    {
        BankSelect = 0x00,
        Modulation = 0x01,
        Volume = 0x07,
        Balance = 0x08,
        Pan = 0x0A,
        Sustain = 0x40
    }

    internal enum TextEventType : byte
    {
        Text = 0x01,
        TrackName = 0x03,
        Lyric = 0x05,
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