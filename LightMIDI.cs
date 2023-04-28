using OpenTK;
using OpenTK.Graphics;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using System.Collections.Generic;
using System;

namespace StorybrewScripts
{
    class LightMIDI : StoryboardObjectGenerator
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

        const float keyCount = 52, whiteKeySpacing = 640f / keyCount;

        [Configurable] public string MIDIPath = "";
        protected override void Generate()
        {
            #region Initialize Constants

            var layer = GetLayer("");
            string getKeyFile(string keyType, bool highlight = false)
                => highlight ? $"sb/k/{keyType}hl.png" : $"sb/k/{keyType}.png";

            var pScale = (float)Math.Round(whiteKeySpacing / 60, 3);

            #endregion

            #region Draw Piano

            var keys = new HashSet<OsbSprite>();
            var keyPositions = new Dictionary<string, int>();
            var keyHighlights = new Dictionary<string, (OsbSprite, OsbSprite)>();

            for (int i = 0, keyOctave = 0; i < keyCount; i++)
            {
                var keyNameIndex = i % 7 - 2;
                if (keyNameIndex == 0) keyOctave++;
                else if (keyNameIndex < 0) keyNameIndex = keyNames.Length + keyNameIndex;

                var keyName = keyNames[keyNameIndex];
                var keyType = keyFiles[keyName];
                var keyFile = getKeyFile(keyType);

                if (i == 0)
                {
                    var chars = keyFile.ToCharArray();
                    chars[chars.Length - 6] = '1';
                    keyFile = new string(chars);
                }
                else if (i == keyCount - 1)
                {
                    var chars = keyFile.ToCharArray();
                    chars[chars.Length - 5] = '1';
                    keyFile = new string(chars);
                }

                var pX = (int)(whiteKeySpacing * i + pScale * (keyCount / 2));

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
                    pX += (int)(whiteKeySpacing * .5f);

                    var pb = layer.CreateSprite("sb/k/bb.png", OsbOrigin.TopCentre, new Vector2(pX, 240));
                    pb.Scale(-1843, pScale);

                    var pbhl = layer.CreateSprite("sb/k/bbhl.png", OsbOrigin.TopCentre, new Vector2(pX, 240));
                    pbhl.Scale(25, pScale);

                    sp = layer.CreateSprite("sb/l.png", OsbOrigin.BottomCentre, new Vector2(pX, 240));
                    sp.ScaleVec(25, .3f, .2f);

                    keyFullName = $"{keyName}Sharp{keyOctave}";
                    keyHighlights[keyFullName] = (pbhl, sp);
                    keyPositions[keyFullName] = pX;
                    keys.Add(pb);
                }
            }

            var delay = (float)Beatmap.GetTimingPointAt(25).BeatDuration * 2 / keys.Count;
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

            #endregion

            CreateNotes(keyPositions, keyHighlights, layer);
        }
        void CreateNotes(
            Dictionary<string, int> positions, Dictionary<string, (OsbSprite, OsbSprite)> highlights, 
            StoryboardSegment layer)
        {
            var scrollTime = 2500;
            var offset = 155 / 192.2f;
            var cut = (float)(Beatmap.GetTimingPointAt(25).BeatDuration / 64);

            var file = new MidiFile(AssetPath + "/" + MIDIPath);
            foreach (var track in file.Tracks)
            {
                var offEvent = new List<MidiEvent>();
                var onEvent = new List<MidiEvent>();

                foreach (var midEvent in track.MidiEvents) switch (midEvent.MidiEventType)
                {
                    case MidiEventType.NoteOff: offEvent.Add(midEvent); continue;
                    case MidiEventType.NoteOn: onEvent.Add(midEvent); continue;
                }

                using (var pool = new SpritePool(layer, "sb/p.png", OsbOrigin.BottomCentre, (p, s, e) =>
                {
                    p.Additive(s);
                    p.Fade(s, .6f);

                    if (track.Index == 0) p.Color(s, new Color4(200, 255, 255, 0));
                    else p.Color(s, new Color4(120, 120, 230, 0));
                }))
                for (var i = 0; i < onEvent.Count; i++)
                {
                    var noteName = (NoteName)(onEvent[i].Note % 12);
                    var octave = onEvent[i].Note / 12 - 1;

                    var time = onEvent[i].Time;
                    var endTime = offEvent[i].Time;

                    if (onEvent[i].Note != offEvent[i].Note) 
                    {
                        Log($"Found mismatched note - {noteName}, {(NoteName)(offEvent[i].Note % 12)}");
                        
                        for (var j = i - 2; j < offEvent.Count; j++) 
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
                    var noteWidth = noteName.ToString().Contains("Sharp") ? whiteKeySpacing * .5f : whiteKeySpacing;

                    var key = $"{noteName}{octave}";

                    var n = pool.Get(time - scrollTime, endTime);
                    if (n.StartTime != double.MaxValue) n.ScaleVec(time - scrollTime, (int)noteWidth, noteLength);
                    n.Move(time - scrollTime, time, positions[key], 0, positions[key], 240);
                    n.ScaleVec(time, endTime, (int)noteWidth, noteLength, (int)noteWidth, 0);

                    var splashes = highlights[key];
                    splashes.Item1.Fade(time, time, 0, 1);
                    splashes.Item2.Fade(time, time, 0, 1);
                    splashes.Item1.Fade(endTime, 0);
                    splashes.Item2.Fade(endTime, 0);
                }
            }

            foreach (var highlight in highlights.Values)
            {
                if (highlight.Item1.CommandCost <= 1) layer.Discard(highlight.Item1);
                if (highlight.Item2.CommandCost <= 1) layer.Discard(highlight.Item2);
            }
        }
    }
}