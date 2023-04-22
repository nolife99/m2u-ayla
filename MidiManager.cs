using OpenTK;
using OpenTK.Graphics;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Collections.Generic;
using System.Collections;
using System;

namespace StorybrewScripts
{
    class MidiManager : StoryboardObjectGenerator
    {
        static readonly string[] keyNames = { "C", "D", "E", "F", "G", "A", "B" };
        static readonly Dictionary<string, string> keyFiles = new Dictionary<string, string>
        {
            {"C", "10"},
            {"D", "00"},
            {"E", "01"},
            {"F", "10"},
            {"G", "00"},
            {"A", "00"},
            {"B", "01"}
        };

        const int noteWidth = 20, pWidth = 58, pHeight = 300, whiteKeySpacing = 12, keyCount = 52;
        const float noteWidthScale = .55f, splashHeight = .2f, splashWidth = .6f, c4Index = keyCount * .5f;

        StoryboardSegment layer;

        [Configurable] public string MIDIPath = "";
        protected override void Generate()
        {
            #region Initialize Constants

            var keyOctave = 3 - (int)Math.Floor(c4Index / 7);
            layer = GetLayer("");

            #endregion

            #region Draw Piano

            var keys = new HashSet<OsbSprite>();
            var keyPositions = new Dictionary<string, float>();
            var keyHighlights = new Dictionary<string, (OsbSprite, OsbSprite)>();

            for (var i = 0; i < keyCount; i++)
            {
                var keyNameIndex = i % 7 - 2;
                if (keyNameIndex == 0) keyOctave++;
                if (keyNameIndex < 0) keyNameIndex = keyNames.Length + keyNameIndex;

                var keyName = keyNames[keyNameIndex];
                var keyFullName = keyName + keyOctave.ToString();
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

                var pX = 320 + (i - c4Index) * whiteKeySpacing;
                var pScale = noteWidth / (float)pWidth * noteWidthScale;

                var p = layer.CreateSprite(keyFile, OsbOrigin.TopCentre, new Vector2(pX, 240));
                p.Scale(-1843, pScale);

                var hl = layer.CreateSprite(getKeyFile(keyType, true), OsbOrigin.TopCentre, new Vector2(pX, 240));
                hl.Scale(25, pScale);

                var sp = layer.CreateSprite("sb/l.png", OsbOrigin.BottomCentre, new Vector2(pX, 240));
                sp.ScaleVec(25, splashWidth, splashHeight);

                keyHighlights[keyFullName] = (hl, sp);
                keyPositions[keyFullName] = pX;
                keys.Add(p);

                Log(keyFullName);

                if (keyFile[keyFile.Length - 5] == '0')
                {
                    pX += whiteKeySpacing * .5f;

                    var pb = layer.CreateSprite("sb/k/bb.png", OsbOrigin.TopCentre, new Vector2(pX, 240));
                    pb.Scale(-1843, pScale);

                    var pbhl = layer.CreateSprite("sb/k/bbhl.png", OsbOrigin.TopCentre, new Vector2(pX, 240));
                    pbhl.Scale(25, pScale);

                    sp = layer.CreateSprite("sb/l.png", OsbOrigin.BottomCentre, new Vector2(pX, 240));
                    sp.ScaleVec(25, splashWidth * .5f, splashHeight);

                    keyFullName = keyName + "Sharp" + keyOctave.ToString();
                    keyHighlights[keyFullName] = (pbhl, sp);
                    keyPositions[keyFullName] = pX;
                    keys.Add(pb);
                }
            }

            var delay = 689f / keys.Count;
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
        void CreateNotes(IDictionary keyPositions, IDictionary keyHighlights, StoryboardSegment layer)
        {
            var positions = keyPositions as Dictionary<string, float>;
            var highlights = keyHighlights as Dictionary<string, (OsbSprite, OsbSprite)>;

            var scrollTime = 2500;
            var noteHeight = GetMapsetBitmap("sb/p.png").Height;
            var lengthMultiplier = (1f / noteHeight) * (240f / scrollTime);

            const float offset = 155 / 192.2f;
            var cut = (float)(Beatmap.GetTimingPointAt(25).BeatDuration / 16);

            var file = MidiFile.Read(AssetPath + "/" + MIDIPath);
            var chunks = file.GetTrackChunks();
            
            chunks.ForEach(track =>
            {
                using (var pool = new SpritePool(layer, "sb/p.png", OsbOrigin.BottomCentre, (p, s, e) =>
                {
                    p.Additive(s);
                    p.Fade(s, .6f);
                    if (chunks.IndexOf(track) == 0) p.Color(s, new Color4(200, 255, 255, 0));
                    else p.Color(s, new Color4(120, 120, 230, 0));
                }))
                foreach (var note in track.GetNotes())
                {
                    note.Time = (long)(note.Time * offset + 25);
                    note.Length = (long)(note.Length * offset + 25);

                    var noteLength = note.Length * lengthMultiplier - .15f;
                    var noteWidth = isFlatNote(note) ? noteWidthScale * .5f : noteWidthScale;

                    var key = note.NoteName + note.Octave.ToString();

                    var n = pool.Get(note.Time - scrollTime, note.EndTime - cut);
                    if (n.StartTime != double.MaxValue) n.ScaleVec(note.Time - scrollTime, noteWidth, noteLength);
                    n.Move(note.Time - scrollTime, note.Time, positions[key], 0, positions[key], 240);
                    n.ScaleVec(note.Time, note.EndTime - cut, noteWidth, noteLength, noteWidth, 0);

                    var splashes = highlights[key];
                    splashes.Item1.Fade(note.Time, note.Time, 0, 1);
                    splashes.Item1.Fade(note.EndTime - cut, note.EndTime - cut, 1, 0);
                    splashes.Item2.Fade(note.Time, note.Time, 0, 1);
                    splashes.Item2.Fade(note.EndTime - cut, note.EndTime - cut, 1, 0);
                }
            });

            foreach (var highlight in highlights.Values)
            {
                if (highlight.Item1.CommandCost <= 1) layer.Discard(highlight.Item1);
                if (highlight.Item2.CommandCost <= 1) layer.Discard(highlight.Item2);
            }
        }

        static bool isFlatNote(Melanchall.DryWetMidi.Interaction.Note note)
            => note.NoteName.ToString().Contains("Sharp");

        static string getKeyFile(string keyType, bool highlight = false)
            => highlight ? $"sb/k/{keyType}hl.png" : $"sb/k/{keyType}.png";
    }
}