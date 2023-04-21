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
        protected override void Generate() => MakeSpectrum(25, 172670, new Color4(120, 120, 255, 0));
        void MakeSpectrum(int startTime, int endTime, Color4 Color)
        {
            var MinimalHeight = .1f;
            var Scale = new Vector2(.1f, 1.4f);
            var LogScale = 450f;
            var Width = 287;
            var Position = new Vector2(320, 377);
            var BarCount = 15;

            var heightKeyframe = new KeyframedValue<float>[BarCount];
            for (var i = 0; i < BarCount; i++) heightKeyframe[i] = new KeyframedValue<float>();

            var timeStep = Beatmap.GetTimingPointAt(startTime).BeatDuration / 8;
            for (double time = startTime; time < endTime + timeStep; time += timeStep)
            {
                var fft = GetFft(time, (int)(BarCount * 1.3), null, OsbEasing.InExpo);
                for (var i = 0; i < BarCount; i++)
                {
                    var height = Math.Pow(Math.Log10(1 + fft[i] * LogScale) * Scale.Y, 1.5);
                    if (height < MinimalHeight) height = MinimalHeight;

                    heightKeyframe[i].Add(time, (float)height);
                }
            }
            var barWidth = Width / BarCount;
            var posX = Position.X - Width / 2;

            for (var i = 0; i < BarCount; i++)
            {
                var positionX = posX + i * barWidth;
                var keyframe = heightKeyframe[i];
                keyframe.Simplify1dKeyframes(.5, h => h);

                var bar = GetLayer("").CreateSprite("sb/p.png", OsbOrigin.Centre, new Vector2(positionX, Position.Y));
                bar.Color(startTime, Color);
                bar.Fade(-2475 + i * (689f / BarCount), startTime, 0, .6);
                bar.Fade(endTime + i * (689f / BarCount), 173444, .6, 0);
                bar.Additive(startTime);

                keyframe.ForEachPair((start, end) 
                    => bar.ScaleVec(start.Time, end.Time, Scale.X, start.Value, Scale.X, end.Value), 
                    MinimalHeight, s => (float)Math.Round(s, 2)
                );
            }
        }
    }
}