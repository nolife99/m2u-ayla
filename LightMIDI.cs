using BrewLib.Util;
using StorybrewCommon.Storyboarding;
using System;
using System.Collections.Generic;
using System.Linq;
using StorybrewCommon.Storyboarding.Util;
using System.IO;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace StorybrewScripts
{
    class LightMIDI : StorybrewCommon.Scripting.StoryboardObjectGenerator
    {
        const int keyCount = 52;
        const float pWidth = 700, keySpacing = pWidth / keyCount;

        [Configurable] public string MIDIPath = "";
        protected override void Generate()
        {
            #region Initialize Constants

            var layer = GetLayer("");
            var keyRect = BitmapHelper.FindTransparencyBounds(GetMapsetBitmap(getKeyFile("00")));
            var pScale = MathF.Round(keySpacing / (keyRect.Width - keyCount / 9f), 3);

            #endregion

            #region Draw Piano

            char[] keyNames = ['C', 'D', 'E', 'F', 'G', 'A', 'B'];
            List<OsbSprite> keys = [];
            Dictionary<string, float> keyPositions = [];
            Dictionary<string, (OsbSprite, OsbSprite)> keyHighlights = [];
            Dictionary<char, string> keyFiles = new()
            {
                {'C', "10"},
                {'D', "00"},
                {'E', "01"},
                {'F', "10"},
                {'G', "00"},
                {'A', "00"},
                {'B', "01"}
            };

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

                var pX = MathF.Round(320 - pWidth / 2 + i * keySpacing + keyRect.Width * pScale / 2, 1);

                var p = layer.CreateSprite(keyFile, OsbOrigin.TopCentre, new(pX, 240));
                p.Scale(-1843, pScale);

                var hl = layer.CreateSprite(getKeyFile(keyType, true), OsbOrigin.TopCentre, new(pX, 240));
                hl.Scale(25, pScale);

                var sp = layer.CreateSprite("sb/l.png", OsbOrigin.BottomCentre, new(pX, 240));
                sp.ScaleVec(25, MathF.Round(pScale * 2.5f, 2), pScale);

                var keyFullName = $"{keyName}{keyOctave}";
                keyHighlights[keyFullName] = (hl, sp);
                keyPositions[keyFullName] = pX;
                keys.Add(p);

                if (keyFile[^5] == '0')
                {
                    pX += MathF.Round(keySpacing / 2, 1);

                    p = layer.CreateSprite("sb/k/bb.png", OsbOrigin.TopCentre, new(pX, 240));
                    p.Scale(-1843, pScale);

                    hl = layer.CreateSprite("sb/k/bbl.png", OsbOrigin.TopCentre, new(pX, 240));
                    hl.Scale(25, pScale);

                    sp = layer.CreateSprite("sb/l.png", OsbOrigin.BottomCentre, new(pX, 240));
                    sp.ScaleVec(25, MathF.Round(pScale * 1.25f, 2), pScale);

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

            #endregion

            CreateNotes(keyPositions, keyHighlights, layer);
        }
        void CreateNotes(Dictionary<string, float> positions, Dictionary<string, (OsbSprite, OsbSprite)> highlights, StoryboardLayer layer)
        {
            const int scrollTime = 2300;

            AddDependency(AssetPath + "/" + MIDIPath);
            var file = MidiFile.Read(AssetPath + "/" + MIDIPath);
            var map = file.GetTempoMap();

            var trackIndex = 0;
            foreach (var track in file.Chunks.OfType<TrackChunk>())
            {
                var notes = track.Events.GetNotes();
                if (notes.Count == 0) continue;

                using OsbSpritePool pool = new(layer, "sb/p.png", OsbOrigin.BottomCentre, (p, s, e) =>
                {
                    p.Additive(s);
                    if (trackIndex == 1) p.Color(s, 28f / 51, 35f / 51, 13f / 17);
                    else p.Color(s, 4f / 17, 4f / 17, 2f / 3);
                });
                foreach (var note in notes)
                {
                    var time = TimeConverter.ConvertTo<MetricTimeSpan>(note.Time, map).TotalMilliseconds + 25;
                    var endTime = TimeConverter.ConvertTo<MetricTimeSpan>(note.EndTime, map).TotalMilliseconds + 25;

                    var length = endTime - time;
                    if (length <= 0) continue;

                    var key = note.NoteName.ToString() + note.Octave;
                    var noteWidth = (int)(key.Contains('S') ? keySpacing / 2 : keySpacing);
                    var noteLength = (int)(length * 240 / scrollTime);

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

                ++trackIndex;
            }
            foreach (var highlight in highlights.Values) if (highlight.Item1.CommandCount < 2)
            {
                layer.Discard(highlight.Item1);
                layer.Discard(highlight.Item2);
            }
        }

        static string getKeyFile(string keyType, bool highlight = false) => highlight ? $"sb/k/{keyType}l.png" : $"sb/k/{keyType}.png";
    }
}