using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Animations;
using System.Numerics;
using System;

namespace StorybrewScripts
{
    class Spectrum : StoryboardObjectGenerator
    {
        protected override void Generate() => MakeSpectrum(25, 172670);
        void MakeSpectrum(int startTime, int endTime)
        {
            const int width = 290, barCount = 15;

            var heightKeyframe = new KeyframedValue<float>[barCount];
            for (var i = 0; i < barCount; ++i) heightKeyframe[i] = new KeyframedValue<float>();

            var timeStep = Beatmap.GetTimingPointAt(startTime).BeatDuration / 8;
            for (double time = startTime; time < endTime + timeStep; time += timeStep)
            {
                var fft = GetFft(time, (int)(barCount * 1.5f), null, OsbEasing.InExpo);
                for (var i = 0; i < barCount; ++i)
                {
                    var height = Math.Pow(Math.Log10(1 + fft[i] * 450) * 5, 2);
                    if (height < 1) height = 1;

                    heightKeyframe[i].Add(time, (float)height);
                }
            }

            var startX = 320 - width * .5f;
            for (var i = 0; i < barCount; ++i)
            {
                var bar = GetLayer("").CreateSprite("sb/p.png", OsbOrigin.Centre, new Vector2((int)startX, 380));
                bar.Color(startTime, .57f, .78f, 1);
                bar.Fade(-2475 + i * (2000f / barCount), startTime, 0, .6);
                bar.Fade(endTime + i * (689f / barCount), 173444, .6, 0);
                bar.Additive(startTime);

                heightKeyframe[i].Simplify1dKeyframes(6, h => h);
                heightKeyframe[i].ForEachPair((s, e) => bar.ScaleVec(s.Time, e.Time, 2, s.Value, 2, e.Value), 
                    1, s => (int)s);

                startX += width / (barCount - 1f);
            }
        }
    }
}