using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Storyboarding;

namespace StorybrewScripts
{
    class Background : StorybrewCommon.Scripting.StoryboardObjectGenerator
    {
        protected override void Generate()
        {
            var back = GetLayer("");
            var overlay = GetLayer("Overlay");

            var bg = back.CreateSprite("bg.jpg", OsbOrigin.Centre, new CommandPosition(320, 240));
            bg.Scale(-2475, 0.44479166666);
            bg.Fade(-1843, -749, 1, 0);
            bg.Fade(171756, 173057, 0, 1);
            bg.Fade(173444, 174992, 1, 0);

            var blur = back.CreateSprite("sb/blur.jpg", OsbOrigin.Centre, new CommandPosition(320, 240));
            blur.Scale(-2475, 0.44479166666);
            blur.Fade(-1843, -749, 0, 1);
            blur.Fade(171756, 173057, 1, 0);

            var line = overlay.CreateSprite("sb/p.png", OsbOrigin.CentreLeft, new CommandPosition(0, 240));
            line.ScaleVec(-2000, -1000, 0, 2, 854, 2);
            line.Fade(-2000, .75f);
            line.MoveX(171756, 171756, -107, 747);
            line.Rotate(171756, 171756, 0, (float)System.Math.PI);
            line.ScaleVec(171756, 172863, 854, 2, 0, 2);

            var vig = overlay.CreateSprite("sb/v.png", OsbOrigin.Centre, new CommandPosition(320, 240));
            vig.Fade(-2475, 174992, 1, 1);
        }
    }
}