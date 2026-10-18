using System;
using System.Collections;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// What the game needs from a rewarded ad service. Implement it on top of AdMob (or any other SDK) later and set
    /// RewardedAds.Provider once at startup; nothing else in the game has to change.
    /// </summary>
    public interface IRewardedAdProvider
    {
        /// <summary>An ad is loaded and can be shown now.</summary>
        bool IsReady { get; }

        /// <summary>Shows an ad. onFinished(true) only when the player watched it to the end and earned the reward.</summary>
        void Show(string placement, Action<bool> onFinished);
    }

    /// <summary>The one place the game asks for a rewarded ad. With no provider set, ads are simply not offered.</summary>
    public static class RewardedAds
    {
        public static IRewardedAdProvider Provider;

        public static bool IsReady => Provider != null && Provider.IsReady;

        public static void Show(string placement, Action<bool> onFinished)
        {
            if (!IsReady) { onFinished?.Invoke(false); return; }
            Provider.Show(placement, onFinished);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Provider = null;
    }
}
