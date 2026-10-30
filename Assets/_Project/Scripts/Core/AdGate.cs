using System;
using UnityEngine;

namespace JumpGame
{
    // Decides when a full-screen ad may appear: only at the start of a dive (a natural pause), after a few dives,
    // and never closer than two minutes to the last one or in the first minute and a half of the session.
    public static class AdGate
    {
        const int DivesBetween = 3;
        const float MinGap = 120f, FirstDelay = 90f;

        static float lastAd = float.NegativeInfinity;
        static int dives;

        public static void Run(Action start)
        {
            dives++;
            bool due = dives >= DivesBetween && Time.realtimeSinceStartup > FirstDelay && Time.realtimeSinceStartup - lastAd >= MinGap;
            if (!due)
            {
                start();
                return;
            }

            dives = 0;
            YandexGames.ShowInterstitial(() =>
            {
                lastAd = Time.realtimeSinceStartup;
                start();
            });
        }
    }
}
