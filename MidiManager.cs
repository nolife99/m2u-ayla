using OpenTK;
using OpenTK.Graphics;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Collections.Generic;

namespace StorybrewScripts
{
    class MidiManager : StoryboardObjectGenerator
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

        const int noteWidth = 20, pWidth = 58, pHeight = 300, whiteKeySpacing = 12;
        const float noteWidthScale = .55f, splashHeight = .2f, splashWidth = .6f;

        [Configurable] public string MIDIPath = "";
        protected override void Generate()
        {
            #region Initialize Constants

            var layer = GetLayer("");

            // Method to match key ID to sprite
            string getKeyFile(string keyType, bool highlight = false)
                => highlight ? $"sb/k/{keyType}hl.png" : $"sb/k/{keyType}.png";

            #endregion

            #region Draw Piano

            var keys = new HashSet<OsbSprite>(); // In case of duplicates
            var keyPositions = new Dictionary<string, float>(); // Matches a note's ID to a x-coordinate
            var keyHighlights = new Dictionary<string, (OsbSprite, OsbSprite)>(); // Matches a note's ID to 2 highlights

            // 52 white keys on a piano
            for (int i = 0, keyOctave = 0; i < 52; i++)
            {
                var keyNameIndex = i % 7 - 2; // Start at A#-0 so offset by 2
                if (keyNameIndex == 0) keyOctave++; // Increase the octave for every note reset
                else if (keyNameIndex < 0) keyNameIndex = keyNames.Length + keyNameIndex; // If index is negative, access the array backwards

                var keyName = keyNames[keyNameIndex]; // Get index's corresponding note
                var keyType = keyFiles[keyName]; // Get corresponding key ID to note
                var keyFile = getKeyFile(keyType); // Get corresponding sprite to key ID

                // At the start and end of each loop, make sure the piano sprites are correct
                if (i == 0)
                {
                    var chars = keyFile.ToCharArray();
                    chars[chars.Length - 6] = '1';
                    keyFile = new string(chars);
                }
                else if (i == 51)
                {
                    var chars = keyFile.ToCharArray();
                    chars[chars.Length - 5] = '1';
                    keyFile = new string(chars);
                }

                var pX = 320 + (i - 26f) * whiteKeySpacing; // Position the keyboard at the center
                var pScale = noteWidth / (float)pWidth * noteWidthScale; // Scale the keys according to the piano's width

                var p = layer.CreateSprite(keyFile, OsbOrigin.TopCentre, new Vector2(pX, 240)); // Key sprite
                p.Scale(-1843, pScale);

                var hl = layer.CreateSprite(getKeyFile(keyType, true), OsbOrigin.TopCentre, new Vector2(pX, 240)); // Highlight sprite
                hl.Scale(25, pScale);

                var sp = layer.CreateSprite("sb/l.png", OsbOrigin.BottomCentre, new Vector2(pX, 240)); // Splash sprite
                sp.ScaleVec(25, splashWidth, splashHeight);

                var keyFullName = $"{keyName}{keyOctave}"; // Construct an ID for the piano key
                keyHighlights[keyFullName] = (hl, sp); // Assign the highlight and splash to the ID
                keyPositions[keyFullName] = pX; // Assign the piano key's x-coordinate to the ID
                keys.Add(p); // Add the key sprite to the HashSet

                // Create black keys
                if (keyFile[keyFile.Length - 5] == '0')
                {
                    pX += whiteKeySpacing * .5f; // Flat notes are half the width of regular notes

                    var pb = layer.CreateSprite("sb/k/bb.png", OsbOrigin.TopCentre, new Vector2(pX, 240)); // Key sprite
                    pb.Scale(-1843, pScale);

                    var pbhl = layer.CreateSprite("sb/k/bbhl.png", OsbOrigin.TopCentre, new Vector2(pX, 240)); // Highlight sprite
                    pbhl.Scale(25, pScale);

                    sp = layer.CreateSprite("sb/l.png", OsbOrigin.BottomCentre, new Vector2(pX, 240)); // Splash sprite
                    sp.ScaleVec(25, splashWidth * .5f, splashHeight);

                    keyFullName = $"{keyName}Sharp{keyOctave}"; // Reconstruct an ID for the piano key
                    keyHighlights[keyFullName] = (pbhl, sp); // Assign the highlight and splash to the ID
                    keyPositions[keyFullName] = pX; // Assign the piano key's x-coordinate to the ID
                    keys.Add(pb); // Add the flat note sprite to the HashSet
                }
            }

            // Fade in the key sprites with a delay
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

            // Generate the falling piano notes
            CreateNotes(keyPositions, keyHighlights, layer);
        }
        void CreateNotes(
            Dictionary<string, float> positions, Dictionary<string, (OsbSprite, OsbSprite)> highlights, 
            StoryboardSegment layer)
        {
            // Constants
            var scrollTime = 2500;
            var noteHeight = GetMapsetBitmap("sb/p.png").Height;
            var lengthMultiplier = (1f / noteHeight) * (240f / scrollTime);

            // Offset the MIDI accordingly to match the beatmap's time (play around with it?)
            const float offset = 155 / 192.2f;
            var cut = (float)(Beatmap.GetTimingPointAt(25).BeatDuration / 16); // Shorten note time by little

            // Generate the notes in a nested loop for each track
            var chunks = MidiFile.Read(AssetPath + "/" + MIDIPath).GetTrackChunks();
            chunks.ForEach(track =>
            {
                using (var pool = new SpritePool(layer, "sb/p.png", OsbOrigin.BottomCentre, (p, s, e) =>
                {
                    p.Additive(s);
                    p.Fade(s, .6f);

                    // Color the notes according to the current track's index
                    if (chunks.IndexOf(track) == 0) p.Color(s, new Color4(200, 255, 255, 0));
                    else p.Color(s, new Color4(120, 120, 230, 0));
                }))
                track.GetNotes().ForEach(note =>
                {
                    // Offset the note's time and length
                    note.Time = (int)(note.Time * offset + 25);
                    note.Length = (int)(note.Length * offset + 25);

                    // Edit the note size
                    var noteLength = note.Length * lengthMultiplier - .15f;
                    var noteWidth = note.NoteName.ToString().Contains("Sharp") ? 
                        noteWidthScale * .5f : noteWidthScale;

                    // Construct an ID for the current note as a dictionary key
                    var key = $"{note.NoteName}{note.Octave}";

                    // Create note sprite (position matched with ID)
                    var n = pool.Get(note.Time - scrollTime, note.EndTime - cut);
                    if (n.StartTime != double.MaxValue) n.ScaleVec(note.Time - scrollTime, noteWidth, noteLength);
                    n.Move(note.Time - scrollTime, note.Time, positions[key], 0, positions[key], 240);
                    n.ScaleVec(note.Time, note.EndTime - cut, noteWidth, noteLength, noteWidth, 0);

                    // Activate the key ID's corresponding highlights
                    var splashes = highlights[key];
                    splashes.Item1.Fade(note.Time, note.Time, 0, 1);
                    splashes.Item1.Fade(note.EndTime - cut, note.EndTime - cut, 1, 0);
                    splashes.Item2.Fade(note.Time, note.Time, 0, 1);
                    splashes.Item2.Fade(note.EndTime - cut, note.EndTime - cut, 1, 0);
                });
            });

            // Remove any unused highlights
            foreach (var highlight in highlights.Values)
            {
                if (highlight.Item1.CommandCost <= 1) layer.Discard(highlight.Item1);
                if (highlight.Item2.CommandCost <= 1) layer.Discard(highlight.Item2);
            }
        }
    }
}