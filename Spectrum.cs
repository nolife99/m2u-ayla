using OpenTK;
using OpenTK.Graphics;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Animations;
using System;

namespace StorybrewScripts
{
    class Spectrum : StoryboardObjectGenerator
    {
        const float minHeight = .1f;
        const int width = 290, barCount = 15;

        protected override void Generate() => MakeSpectrum(25, 172670, new Color4(120, 120, 255, 0));
        void MakeSpectrum(int startTime, int endTime, Color4 Color)
        {
            var heightKeyframe = new KeyframedValue<float>[barCount];
            for (var i = 0; i < barCount; i++) heightKeyframe[i] = new KeyframedValue<float>();

            var timeStep = Beatmap.GetTimingPointAt(startTime).BeatDuration / 8;
            for (double time = startTime; time < endTime + timeStep; time += timeStep)
            {
                var fft = GetFft(time, (int)(barCount * 1.3), null, OsbEasing.InExpo);
                for (var i = 0; i < barCount; i++)
                {
                    var height = Math.Pow(Math.Log10(1 + fft[i] * 450) * 1.4f, 1.5);
                    if (height < minHeight) height = minHeight;

                    heightKeyframe[i].Add(time, (float)height);
                }
            }

            for (var i = 0; i < barCount; i++)
            {
                var bar = GetLayer("").CreateSprite("sb/p.png", OsbOrigin.Centre, 
                    new Vector2((332 - width / 2) + i * (width / barCount), 380));
                bar.Color(startTime, Color);
                bar.Fade(-2475 + i * (689f / barCount), startTime, 0, .6);
                bar.Fade(endTime + i * (689f / barCount), 173444, .6, 0);
                bar.Additive(startTime);

                heightKeyframe[i].Simplify1dKeyframes(.47, h => h);
                heightKeyframe[i].ForEachPair((s, e) 
                    => bar.ScaleVec(s.Time, e.Time, .1f, s.Value, .1f, e.Value), 
                    minHeight, s => (float)Math.Round(s, 2)
                );
            }
        }
    }
}