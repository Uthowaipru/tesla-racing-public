using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimeToon
{
    /// <summary>
    /// Created by AnimeQuality (you never add this yourself). Re-applies the scene parts after each scene load, keeps the
    /// frame cap, and in Auto mode watches the frame rate: below about 24 fps for two 5-second windows it asks
    /// AnimeQuality to drop one tier. Paused, backgrounded and loading frames are ignored.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class AnimeQualityRunner : MonoBehaviour
    {
        const float WindowSeconds = 5f;
        const float StepDownBelowFps = 24f;
        const float SettleSeconds = 6f;     // after a scene load or a tier change
        const float CooldownSeconds = 12f;

        float windowTime;
        int windowFrames;
        int badWindows;
        float settleUntil;
        float cooldownUntil;
        float nextTick;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            AnimeQuality.Changed += OnChanged;
            settleUntil = Time.unscaledTime + SettleSeconds;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            AnimeQuality.Changed -= OnChanged;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            AnimeQuality.ApplySceneBits();
            Restart();
        }

        void OnChanged()
        {
            Restart();
        }

        void Restart()
        {
            settleUntil = Time.unscaledTime + SettleSeconds;
            windowTime = 0f;
            windowFrames = 0;
            badWindows = 0;
        }

        void Update()
        {
            if (Time.unscaledTime >= nextTick)
            {
                nextTick = Time.unscaledTime + 1.5f;
                AnimeQuality.Tick();
            }

            if (!AnimeQuality.AdaptiveActive) return;

            float dt = Time.unscaledDeltaTime;
            bool counts = Time.timeScale > 0.5f && Application.isFocused && dt < 0.5f;
            if (!counts || Time.unscaledTime < settleUntil || Time.unscaledTime < cooldownUntil)
            {
                windowTime = 0f;
                windowFrames = 0;
                return;
            }

            windowTime += dt;
            windowFrames++;
            if (windowTime < WindowSeconds) return;

            float fps = windowFrames / windowTime;
            windowTime = 0f;
            windowFrames = 0;

            if (fps >= StepDownBelowFps) { badWindows = 0; return; }

            badWindows++;
            if (badWindows >= 2)
            {
                badWindows = 0;
                cooldownUntil = Time.unscaledTime + CooldownSeconds;
                AnimeQuality.StepDown();
            }
        }
    }
}
