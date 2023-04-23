using OpenTK;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using System;

namespace StorybrewScripts
{
    class Background : StoryboardObjectGenerator
    {
        protected override void Generate()
        {
            var bgLayer = GetLayer("");

            var bg = bgLayer.CreateSprite("bg.jpg", OsbOrigin.Centre, new Vector2(320, 240));
            bg.Scale(-2475, 854f / GetMapsetBitmap("bg.jpg").Width);
            bg.Fade(-1843, -749, 1, 0);
            bg.Fade(171756, 173057, 0, 1);
            bg.Fade(173444, 174992, 1, 0);

            var blur = bgLayer.CreateSprite("sb/blur.jpg", OsbOrigin.Centre, new Vector2(320, 240));
            blur.Scale(-2475, 854f / GetMapsetBitmap("bg.jpg").Width);
            blur.Fade(-1843, -749, 0, 1);
            blur.Fade(171756, 173057, 1, 0);

            var line = bgLayer.CreateSprite("sb/px.png", OsbOrigin.CentreLeft, new Vector2(0, 240));
            line.ScaleVec(-2000, -1000, 0, 2, 854, 2);
            line.Fade(-2000, .5f);
            line.MoveX(171756, 171756, -107, 747);
            line.Rotate(171756, 171756, 0, (float)Math.PI);
            line.ScaleVec(171756, 172863, 854, 2, 0, 2);
            
            var vig = GetLayer("Overlay").CreateSprite("sb/v.png", OsbOrigin.Centre, new Vector2(320, 240));
            vig.Fade(-2475, 174992, 1, 1);
        }
    }
}