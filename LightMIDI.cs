using BrewLib.Util;
using StorybrewCommon.Storyboarding;
using System;
using System.IO.MemoryMappedFiles;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;

namespace StorybrewScripts
{
    class LightMIDI : StorybrewCommon.Scripting.StoryboardObjectGenerator
    {
        static readonly char[] keyNames = { 'C', 'D', 'E', 'F', 'G', 'A', 'B' };
        static readonly Dictionary<char, string> keyFiles = new Dictionary<char, string>
        {
            {'C', "10"},
            {'D', "00"},
            {'E', "01"},
            {'F', "10"},
            {'G', "00"},
            {'A', "00"},
            {'B', "01"}
        };

        const int keyCount = 52;
        const float pWidth = 700, keySpacing = pWidth / keyCount;

        [Configurable] public string MIDIPath = "";
        protected override void Generate()
        {
            #region Initialize Constants

            var layer = GetLayer("");
            var keyRect = BitmapHelper.FindTransparencyBounds(GetMapsetBitmap(getKeyFile("00"))).Size;
            var pScale = (float)Math.Round(keySpacing / (keyRect.Width - keyCount / 8f), 3);

            #endregion

            #region Draw Piano

            var keys = new HashSet<OsbSprite>(88);
            var keyPositions = new Dictionary<string, float>();
            var keyHighlights = new Dictionary<string, (OsbSprite, OsbSprite)>();

            for (int i = 0, keyOctave = 0; i < keyCount; ++i)
            {
                var keyNameIndex = i % 7 - 2;
                if (keyNameIndex == 0) ++keyOctave;
                else if (keyNameIndex < 0) keyNameIndex = keyNames.Length + keyNameIndex;

                var keyName = keyNames[keyNameIndex];
                var keyType = keyFiles[keyName];
                var keyFile = getKeyFile(keyType);

                unsafe
                {
                    fixed (char* pChars = keyFile)
                        if (i == 0) pChars[keyFile.Length - 6] = '1';
                        else if (i == keyCount - 1) pChars[keyFile.Length - 5] = '1';
                }

                var pX = (float)Math.Round(320 - pWidth * .5f + i * keySpacing + keyRect.Width * pScale * .5f, 1);

                var p = layer.CreateSprite(keyFile, OsbOrigin.TopCentre, new Vector2(pX, 240));
                p.Scale(-1843, pScale);

                var hl = layer.CreateSprite(getKeyFile(keyType, true), OsbOrigin.TopCentre, new Vector2(pX, 240));
                hl.Scale(25, pScale);

                var sp = layer.CreateSprite("sb/l.png", OsbOrigin.BottomCentre, new Vector2(pX, 240));
                sp.ScaleVec(25, (float)Math.Round(pScale * 2.5f, 2), pScale);

                var keyFullName = $"{keyName}{keyOctave}";
                keyHighlights[keyFullName] = (hl, sp);
                keyPositions[keyFullName] = pX;
                keys.Add(p);

                if (keyFile[keyFile.Length - 5] == '0')
                {
                    pX += (float)Math.Round(keySpacing * .5f, 1);

                    p = layer.CreateSprite("sb/k/bb.png", OsbOrigin.TopCentre, new Vector2(pX, 240));
                    p.Scale(-1843, pScale);

                    hl = layer.CreateSprite("sb/k/bbl.png", OsbOrigin.TopCentre, new Vector2(pX, 240));
                    hl.Scale(25, pScale);

                    sp = layer.CreateSprite("sb/l.png", OsbOrigin.BottomCentre, new Vector2(pX, 240));
                    sp.ScaleVec(25, (float)Math.Round(pScale * 1.25f, 2), pScale);

                    keyFullName = $"{keyName}Sharp{keyOctave}";
                    keyHighlights[keyFullName] = (hl, sp);
                    keyPositions[keyFullName] = pX;
                    keys.Add(p);
                }
            }

            var delay = (float)Beatmap.TimingPoints.First().BeatDuration * 2 / keys.Count;
            var keyTime = 0f;

            foreach (var key in keys)
            {
                var introStartTime = -1843 + keyTime;
                var introEndTime = introStartTime + 100;
                var outroStartTime = 171756 + keyTime;
                var outroEndTime = outroStartTime + 100;
                key.Fade(introStartTime, introEndTime, 0, 1);
                key.Fade(outroStartTime, outroEndTime, 1, 0);

                keyTime += delay;
            }
            keys.Clear();

            #endregion
            
            CreateNotes(keyPositions, keyHighlights, layer);
        }
        void CreateNotes(
            Dictionary<string, float> positions, Dictionary<string, (OsbSprite, OsbSprite)> highlights, 
            StoryboardSegment layer)
        {
            const int scrollTime = 2300;

            AddDependency(AssetPath + "/" + MIDIPath);
            var file = new MidiFile(AssetPath + "/" + MIDIPath);
            var offset = (float)Beatmap.TimingPoints.First().BeatDuration / file.TicksPerQuarterNote;

            foreach (var track in file.Tracks)
            {
                var offEvent = track.MidiEvents.Where(ev => ev.Type == (byte)MidiEventType.NoteOff).ToArray();
                var onEvent = track.MidiEvents.Where(ev => ev.Type == (byte)MidiEventType.NoteOn).ToArray();

                using (var pool = new SpritePool(layer, "sb/p.png", OsbOrigin.BottomCentre, (p, s, e) =>
                {
                    p.Additive(s);
                    if (track.Index == 0) p.Color(s, 28f / 51, 35f / 51, 13f / 17);
                    else p.Color(s, 4f / 17, 4f / 17, 2f / 3);
                }))
                for (var i = 0; i < onEvent.Length; ++i)
                {
                    var key = createKey(onEvent[i].Note);

                    float time = onEvent[i].Time;
                    float endTime = offEvent[i].Time;

                    if (onEvent[i].Note != offEvent[i].Note)
                    {
                        Log($"Found mismatched notes: {key}, {createKey(offEvent[i].Note)}");
                        endTime = offEvent[Array.FindIndex(
                            offEvent, i - 2, ev => ev.Note == onEvent[i].Note && ev.Time > time
                        )].Time;
                    }

                    time = time * offset + 25;
                    endTime = endTime * offset + 24;

                    var length = endTime - time;
                    if (length <= 0) continue;

                    var noteLength = (int)Math.Round(length * 240f / scrollTime);
                    var noteWidth = (int)(key.Contains("Sharp") ? keySpacing * .5f : keySpacing);

                    var n = pool.Get(time - scrollTime, endTime);
                    if (n.StartTime != double.MaxValue) n.ScaleVec(time - scrollTime, noteWidth, noteLength);
                    n.Move(time - scrollTime, time, positions[key], 0, positions[key], 240);
                    n.ScaleVec(time, endTime, noteWidth, noteLength, noteWidth, 0);

                    var splashes = highlights[key];
                    splashes.Item1.Fade(time, time, 0, 1);
                    splashes.Item2.Fade(time, time, 0, 1);
                    splashes.Item1.Fade(endTime, 0);
                    splashes.Item2.Fade(endTime, 0);
                }
            }
            positions.Clear();

            foreach (var highlight in highlights.Values.Where(hl => hl.Item1.CommandCount <= 1))
            {
                layer.Discard(highlight.Item1);
                layer.Discard(highlight.Item2);
            }
            highlights.Clear();
        }

        static string getKeyFile(string keyType, bool highlight = false)
            => highlight ? $"sb/k/{keyType}l.png" : $"sb/k/{keyType}.png";

        static string createKey(int note) 
            => $"{(NoteName)(note % 12)}{note / 12 - 1}";
    }

    /*
      =========================================================================================
      =========================================================================================
      =============================== <- [ MIDI PARSING ] -> ==================================
      =========================================================================================
      =========================================================================================
    */

    #region MIDI Parsing

    unsafe class MidiFile
    {
        internal int Format { get; private set; }
        internal int TicksPerQuarterNote { get; private set; }
        internal int TracksCount { get; private set; }
        internal MidiTrack[] Tracks { get; private set; }

        internal MidiFile(string path)
        {
            using (var acc = MemoryMappedFile.CreateFromFile(path).CreateViewAccessor().SafeMemoryMappedViewHandle)
            {
                byte* pData = null;
                acc.AcquirePointer(ref pData);
                try
                {
                    ParseHandle(pData);
                }
                finally
                {
                    acc.ReleasePointer();
                }
            }
        }
        internal MidiFile(byte[] data)
        {
            fixed (byte* pData = data) ParseHandle(pData);
        }
        void ParseHandle(byte* pData)
        {
            var position = 0;

            if (!Reader.ReadString(pData, ref position, 4).Contains("MThd")) throw new FormatException("Invalid file header (expected MThd)");
            if (Reader.Read32(pData, ref position) != 6) throw new FormatException("Invalid header length (expected 6)");

            Format = Reader.Read16(pData, ref position);
            TracksCount = Reader.Read16(pData, ref position);
            TicksPerQuarterNote = Reader.Read16(pData, ref position);

            if ((TicksPerQuarterNote & 0x8000) != 0) throw new FormatException("Invalid timing mode (SMPTE timecode not supported)");

            Tracks = new MidiTrack[TracksCount];
            for (var i = 0; i < TracksCount; ++i) Tracks[i] = ParseTrack(i, pData, ref position);
        }

        static bool ParseMetaEvent(byte* data, ref int i, byte metaEventType, out byte data1, out byte data2)
        {
            switch ((MetaEventType)metaEventType)
            {
                case MetaEventType.Tempo:
                    var mspqn = (data[++i] << 16) | (data[++i] << 8) | data[++i];
                    data1 = (byte)(60000000f / mspqn);
                    data2 = 0;
                    ++i;
                    return true;

                case MetaEventType.TimeSignature:
                    data1 = data[++i];
                    data2 = (byte)Math.Pow(2, data[++i]);
                    i += 3;
                    return true;

                case MetaEventType.KeySignature:
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

    unsafe static class Reader
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

    class MidiTrack
    {
        internal int Index;
        internal ICollection<MidiEvent> MidiEvents;
        internal ICollection<TextEvent> TextEvents;

        internal MidiTrack(int index)
        {
            Index = index;
            MidiEvents = new HashSet<MidiEvent>();
            TextEvents = new HashSet<TextEvent>();
        }
    }

    struct MidiEvent
    {
        internal int Time;
        internal byte Type, Arg1, Arg2, Arg3;

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
    struct TextEvent
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

    enum MidiEventType : byte
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
    enum TextEventType : byte
    {
        Text = 0x01,
        TrackName = 0x03,
        Lyric = 0x05
    }
    enum MetaEventType : byte
    {
        Tempo = 0x51,
        TimeSignature = 0x58,
        KeySignature = 0x59
    }
    enum NoteName
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

    #endregion
}
