using OpenTK;
using OpenTK.Graphics;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Collections.Generic;
using System.Linq;
using System;

using Note = Melanchall.DryWetMidi.Interaction.Note;

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

        readonly Dictionary<string, (OsbSprite, OsbSprite)> keyHighlights = new Dictionary<string, (OsbSprite, OsbSprite)>();

        const int noteWidth = 20, pWidth = 58, pHeight = 300, whiteKeySpacing = 12, keyCount = 49;
        const float noteWidthScale = .55f, splashHeight = .2f, splashWidth = .6f, c4Index = keyCount * .5f;

        StoryboardSegment layer;

        [Configurable] public string MIDIPath = "";
        protected override void Generate()
        {
            #region Initialize Constants

            var keyOctave = 3 - (int)Math.Floor(c4Index / 7f);
            layer = GetLayer("");

            const float offset = 155 / 192.2f;
            var cut = (float)(Beatmap.GetTimingPointAt(25).BeatDuration / 16);

            #endregion

            var keys = new List<OsbSprite>();

            #region Initialize Piano

            var keyPositions = new Dictionary<string, float>();
            for (var i = 0; i < keyCount; i++)
            {
                var keyNameIndex = (i - (int)Math.Floor(c4Index)) % 7;
                if (keyNameIndex == 0) keyOctave += 1;
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
                var pScale = (noteWidth / (float)pWidth) * noteWidthScale;

                var p = layer.CreateSprite(keyFile, OsbOrigin.TopCentre, new Vector2(pX, 240));
                p.Scale(-1843, pScale);

                var hl = layer.CreateSprite(getKeyFile(keyType, true), OsbOrigin.TopCentre, new Vector2(pX, 240));
                hl.Scale(-1843, pScale);

                var sp = layer.CreateSprite("sb/l.png", OsbOrigin.BottomCentre, new Vector2(pX, 240));
                sp.ScaleVec(-1843, splashWidth, splashHeight);

                keyHighlights[keyFullName] = (hl, sp);
                keyPositions[keyFullName] = pX;
                keys.Add(p);

                if (keyFile[keyFile.Length - 5] == '0')
                {
                    pX += (int)(whiteKeySpacing * .5f);

                    var pb = layer.CreateSprite("sb/k/bb.png", OsbOrigin.TopCentre, new Vector2(pX, 240));
                    pb.Scale(-1843, pScale);

                    var pbhl = layer.CreateSprite("sb/k/bbhl.png", OsbOrigin.TopCentre, new Vector2(pX, 240));
                    pbhl.Scale(-1843, pScale);

                    sp = layer.CreateSprite("sb/l.png", OsbOrigin.BottomCentre, new Vector2(pX, 240));
                    sp.ScaleVec(-1843, splashWidth * .5f, splashHeight);

                    keyFullName = keyName + "Sharp" + keyOctave.ToString();
                    keyHighlights[keyFullName] = (pbhl, sp);
                    keyPositions[keyFullName] = pX;
                    keys.Add(pb);
                }
            }

            var delay = (int)Math.Round(689f / keys.Count);
            var keyTime = 0;

            keys.ForEach(key =>
            {
                var introStartTime = -1843 + keyTime;
                var introEndTime = introStartTime + 100;
                var outroStartTime = 171756 + keyTime;
                var outroEndTime = outroStartTime + 100;
                key.Fade(introStartTime, introEndTime, 0, 1);
                key.Fade(outroStartTime, outroEndTime, 1, 0);

                keyTime += delay;
            });

            var bgLayer = GetLayer("Background");

            var bg = bgLayer.CreateSprite("bg.jpg", OsbOrigin.Centre, new Vector2(320, 240));
            bg.Scale(-2475, 854f / GetMapsetBitmap("bg.jpg").Width);
            bg.Fade(-1843, -1743 + keyTime, 1, 0);
            bg.Fade(171756, 171856 + keyTime, 0, 1);
            bg.Fade(173444, 174992, 1, 0);

            var blur = bgLayer.CreateSprite("sb/blur.jpg", OsbOrigin.Centre, new Vector2(320, 240));
            blur.Scale(-2475, 854f / GetMapsetBitmap("bg.jpg").Width);
            blur.Fade(-1843, -1743 + keyTime, 0, 1);
            blur.Fade(171756, 171856 + keyTime, 1, 0);

            var line = bgLayer.CreateSprite("sb/px.png", OsbOrigin.CentreLeft, new Vector2(0, 240));
            line.ScaleVec(-2000, -1000, 0, 2, 854, 2);
            line.Fade(-2000, .5f);
            line.MoveX(171756, 171756, -107, 854);
            line.Rotate(171756, 171756, 0, (float)Math.PI);
            line.ScaleVec(171756, 172863, 854, 2, 0, 2);

            #endregion

            #region Create Notes

            var scrollTime = 2500;
            var noteHeight = GetMapsetBitmap("sb/p.png").Height;

            var scrollSpeed = 240f / scrollTime;
            var lengthMultiplier = (1f / noteHeight) * scrollSpeed;

            var file = MidiFile.Read(AssetPath + "/" + "Ayla_-_M2U.mid");
            var chunks = file.GetTrackChunks().ToList();
            
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

                    var n = pool.Get(note.Time - scrollTime, note.EndTime - cut);
                    if (n.StartTime != double.MaxValue) n.ScaleVec(note.Time - scrollTime, noteWidth, noteLength);
                    n.Move(note.Time - scrollTime, note.Time, getNoteXPosition(note), 0, getNoteXPosition(note), 240);
                    n.ScaleVec(note.Time, note.EndTime - cut, noteWidth, noteLength, noteWidth, 0);

                    var highlights = keyHighlights[note.NoteName + note.Octave.ToString()];
                    highlights.Item1.Fade(note.Time, note.Time, 0, 1);
                    highlights.Item1.Fade(note.EndTime - cut, note.EndTime - cut, 1, 0);
                    highlights.Item2.Fade(note.Time, note.Time, 0, 1);
                    highlights.Item2.Fade(note.EndTime - cut, note.EndTime - cut, 1, 0);
                }
            });

            foreach (var highlight in keyHighlights.Values)
            {
                if (highlight.Item1.CommandCost <= 1) layer.Discard(highlight.Item1);
                if (highlight.Item2.CommandCost <= 1) layer.Discard(highlight.Item2);
            }

            var vig = layer.CreateSprite("sb/v.png", OsbOrigin.Centre, new Vector2(320, 240));
            vig.Fade(-2475, 174992, 1, 1);

            #endregion
        }

        static bool isFlatNote(Note note)
            => int.TryParse(getKeyOffset(note.NoteName, 1).ToString(), out int o);

        static string getKeyFile(string keyType, bool highlight = false)
            => highlight ? $"sb/k/{keyType}hl.png" : $"sb/k/{keyType}.png";

        static int getOctaveOffset(int octave, int spacing)
            => (octave - 4) * 7 * spacing;

        static float getKeyOffset(NoteName noteName, float spacing) => new Dictionary<NoteName, float>
        {
            { NoteName.A, 4.5f },
            { NoteName.ASharp, 5 },
            { NoteName.B, 5.5f },
            { NoteName.C, -.5f },
            { NoteName.CSharp, 0 },
            { NoteName.D, .5f },
            { NoteName.DSharp, 1 },
            { NoteName.E, 1.5f },
            { NoteName.F, 2.5f },
            { NoteName.FSharp, 3 },
            { NoteName.G, 3.5f },
            { NoteName.GSharp, 4 }
        }[noteName] * spacing;

        static float getNoteXPosition(Note note)
        {
            var keyOffset = getKeyOffset(note.NoteName, whiteKeySpacing);
            var octaveOffset = getOctaveOffset(note.Octave, whiteKeySpacing);

            return 320 + keyOffset + octaveOffset;
        }
    }
}