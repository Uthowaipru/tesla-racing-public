using System;
using System.Collections;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// A pretend ad for the editor and development builds, so the "double your rewards" flow can be tried before an ad SDK exists:
    /// it waits a couple of seconds and then reports that the ad was watched.
    /// </summary>
    public class DevRewardedAdProvider : MonoBehaviour, IRewardedAdProvider
    {
        public float duration = 2f;

        public bool IsReady => true;

        public void Show(string placement, Action<bool> onFinished) => StartCoroutine(Play(onFinished));

        IEnumerator Play(Action<bool> onFinished)
        {
            yield return new WaitForSecondsRealtime(duration);
            onFinished?.Invoke(true);
        }
    }
}
