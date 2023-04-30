using OpenTK;
using StorybrewCommon.Storyboarding;
using System;
using System.Collections.Generic;
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

        const float keyCount = 52, keySpacing = 640f / keyCount;

        [Configurable] public string MIDIPath = "";
        protected override void Generate()
        {
            #region Initialize Constants

            var layer = GetLayer("");
            string getKeyFile(string keyType, bool highlight = false)
                => highlight ? $"sb/k/{keyType}l.png" : $"sb/k/{keyType}.png";

            var pScale = (float)Math.Round(keySpacing / 60, 3);

            #endregion

            #region Draw Piano

            var keys = new HashSet<OsbSprite>(88);
            var keyPositions = new Dictionary<string, int>();
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

                var pX = (int)(keySpacing * i + pScale * (keyCount / 2));

                var p = layer.CreateSprite(keyFile, OsbOrigin.TopCentre, new Vector2(pX, 240));
                p.Scale(-1843, pScale);

                var hl = layer.CreateSprite(getKeyFile(keyType, true), OsbOrigin.TopCentre, new Vector2(pX, 240));
                hl.Scale(25, pScale);

                var sp = layer.CreateSprite("sb/l.png", OsbOrigin.BottomCentre, new Vector2(pX, 240));
                sp.ScaleVec(25, .6f, .2f);

                var keyFullName = $"{keyName}{keyOctave}";
                keyHighlights[keyFullName] = (hl, sp);
                keyPositions[keyFullName] = pX;
                keys.Add(p);

                if (keyFile[keyFile.Length - 5] == '0')
                {
                    pX += (int)(keySpacing * .5f);

                    var pb = layer.CreateSprite("sb/k/bb.png", OsbOrigin.TopCentre, new Vector2(pX, 240));
                    pb.Scale(-1843, pScale);

                    var pbhl = layer.CreateSprite("sb/k/bbl.png", OsbOrigin.TopCentre, new Vector2(pX, 240));
                    pbhl.Scale(25, pScale);

                    sp = layer.CreateSprite("sb/l.png", OsbOrigin.BottomCentre, new Vector2(pX, 240));
                    sp.ScaleVec(25, .3f, .2f);

                    keyFullName = $"{keyName}Sharp{keyOctave}";
                    keyHighlights[keyFullName] = (pbhl, sp);
                    keyPositions[keyFullName] = pX;
                    keys.Add(pb);
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
            Dictionary<string, int> positions, Dictionary<string, (OsbSprite, OsbSprite)> highlights, 
            StoryboardSegment layer)
        {
            const int scrollTime = 2500;
            Vector3 c1 = new Vector3(28f / 51, 35f / 51, 13f / 17),
                c2 = new Vector3(4f / 17, 4f / 17, 2f / 3);

            AddDependency(AssetPath + "/" + MIDIPath);
            var file = new MidiFile(AssetPath + "/" + MIDIPath);
            foreach (var track in file.Tracks)
            {
                var offEvent = new List<MidiEvent>();
                var onEvent = new List<MidiEvent>();
                var offset = (float)Beatmap.TimingPoints.First().BeatDuration / file.TicksPerQuarterNote;

                foreach (var midEvent in track.MidiEvents) switch (midEvent.MidiEventType)
                {
                    case MidiEventType.NoteOff: offEvent.Add(midEvent); continue;
                    case MidiEventType.NoteOn: onEvent.Add(midEvent); continue;
                }

                using (var pool = new SpritePool(layer, "sb/p.png", OsbOrigin.BottomCentre, (p, s, e) =>
                {
                    p.Additive(s);
                    if (track.Index == 0) p.Color(s, c1.X, c1.Y, c1.Z);
                    else p.Color(s, c2.X, c2.Y, c2.Z);
                }))
                for (var i = 0; i < onEvent.Count; ++i)
                {
                    var noteName = (NoteName)(onEvent[i].Note % 12);
                    var octave = onEvent[i].Note / 12 - 1;

                    var time = onEvent[i].Time;
                    var endTime = offEvent[i].Time;

                    if (onEvent[i].Note != offEvent[i].Note) 
                    {
                        Log($"Found mismatched note - {noteName}, {(NoteName)(offEvent[i].Note % 12)}");
                        
                        for (var j = i - 2; j < offEvent.Count; ++j) 
                        if (onEvent[i].Note == offEvent[j].Note && offEvent[j].Time > time) 
                        {
                            endTime = offEvent[j].Time;
                            break;
                        }
                    }

                    time = (int)(time * offset + 25);
                    endTime = (int)(endTime * offset + 20);

                    var length = endTime - time;
                    if (length <= 0) continue;

                    var noteLength = (int)Math.Round(length * 240f / scrollTime);
                    var noteWidth = (int)(noteName.ToString().Contains("Sharp") ? keySpacing * .5f : keySpacing);

                    var key = $"{noteName}{octave}";

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

            foreach (var highlight in highlights.Values)
            {
                if (highlight.Item1.CommandCost <= 1) layer.Discard(highlight.Item1);
                if (highlight.Item2.CommandCost <= 1) layer.Discard(highlight.Item2);
            }
        }
    }
}