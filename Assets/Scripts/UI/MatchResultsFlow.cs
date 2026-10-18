using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace VehicleSystem
{
    /// <summary>One line of the leaderboard.</summary>
    [Serializable]
    public class LeaderboardRow
    {
        public GameObject root;
        public Text position, playerName, score, distance, sideBumps, bestChain;
    }

    /// <summary>
    /// Everything after the final whistle, one screen after the other:
    ///   1. "TIME UP"
    ///   2. the winner cutscene: the winner's name and vehicle (everybody sees it; custom, even paid, cutscenes can take over)
    ///   3. the leaderboard: position, player, score, distance, side bumps, best chain
    ///   4. the rewards (coins and cash), with a "double your rewards" rewarded-ad offer, then CLAIM
    ///   5. PLAY AGAIN / MAIN MENU
    /// LocalPlayerRig gives it the MatchManager (SetMatch).
    ///
    /// Custom cutscenes: subscribe to CutsceneRequested(winner, done). While anyone is subscribed the built-in cutscene is skipped:
    /// play your own and call done() when it ends.
    ///
    /// UI source (same idea as the other UI scripts): the objects in your Hierarchy (Tools > Vehicle > Generate Match Results UI, or
    /// the button on this component) are used; if there are none a default is built at runtime. Texts, colours and which panel is
    /// visible are written at runtime; clicks are connected in code. The panels are switched off in the editor.
    /// </summary>
    public class MatchResultsFlow : MonoBehaviour
    {
        public enum Step { Hidden, TimeUp, Cutscene, Leaderboard, Rewards }

        [Header("Source (assigned by LocalPlayerRig)")]
        public MatchManager match;

        [Header("Timing (seconds)")]
        [Tooltip("Pause after the whistle before TIME UP appears.")]
        public float startDelay = 0.8f;
        public float timeUpSeconds = 1.6f;
        public float cutsceneSeconds = 5f;
        [Tooltip("How long the reward numbers take to count up.")]
        public float countUpSeconds = 1.2f;

        [Header("Rewarded ad: double the rewards")]
        public bool offerDoubleReward = true;
        [Tooltip("The double-reward offer is made this many times a day at most.")]
        public int maxDoublesPerDay = 5;
        [Tooltip("In the editor and development builds with no ad SDK yet, a pretend ad is shown, so the flow can be tried.")]
        public bool simulateAdsWithoutSdk = true;
        public float simulatedAdSeconds = 2f;
        public string adPlacement = "double_match_rewards";

        [Header("Winner stage (3D vehicle for the cutscene)")]
        [Tooltip("Created for you if empty.")]
        public VehiclePreviewStage winnerStage;

        [Header("UI (filled in by the editor tool)")]
        public GameObject uiRoot;

        [Header("Cutscene")]
        public GameObject cutscenePanel;
        public Text timeUpText;
        public GameObject winnerGroup;
        public Text winnerLabelText, winnerNameText, winnerVehicleText;
        public RawImage winnerImage;
        [Tooltip("Covers the whole cutscene: a tap skips it.")]
        public Button cutsceneSkipButton;

        [Header("Leaderboard")]
        public GameObject leaderboardPanel;
        public LeaderboardRow[] rows;
        public Button continueButton;

        [Header("Rewards")]
        public GameObject rewardsPanel;
        public Text coinsText, cashText, multiplierText, walletText;
        public Button claimButton;
        public GameObject adPopup;
        public Text adPopupText;
        public Button watchAdButton, noThanksButton;
        public GameObject endButtons;
        public Button playAgainButton, mainMenuButton;
        [Tooltip("Scene loaded by MAIN MENU (it must be in the Build Settings).")]
        public string mainMenuScene = "MainMenu";

        [Header("Default look (editor tool + runtime fallback)")]
        public int fontSize = 40;
        public Color accentColor = new Color(1f, 0.85f, 0.3f, 1f);
        public Color localRowColor = new Color(1f, 0.85f, 0.3f, 1f);
        public Color panelColor = new Color(0f, 0f, 0f, 0.9f);
        public Color buttonColor = new Color(0.22f, 0.22f, 0.3f, 1f);

        public Step CurrentStep { get; private set; } = Step.Hidden;

        /// <summary>Hook for custom cutscenes. Play yours for the winner and call done() when it has ended.</summary>
        public event Action<MatchPlayer, Action> CutsceneRequested;
        /// <summary>The player took the rewards (coins, cash). The profile will add them to the account.</summary>
        public event Action<int, int> RewardsClaimed;

        const string DayKey = "VehicleSystem.AdDoubleDay";
        const string CountKey = "VehicleSystem.AdDoubleCount";

        List<MatchPlayer> ranking = new List<MatchPlayer>();
        MatchResult result;
        DevRewardedAdProvider devAds;
        float delayTimer, stepTimer, countUp;
        bool pending, customCutscene, popupHandled, claimed;
        int multiplier = 1;

        // ------------------------------------------------------------------ setup

        /// <summary>The match to follow (null = nothing). Called by LocalPlayerRig.</summary>
        public void SetMatch(MatchManager newMatch)
        {
            if (match != null)
            {
                match.Finished -= OnFinished;
                match.StateChanged -= OnStateChanged;
            }

            match = newMatch;

            if (match != null)
            {
                match.Finished += OnFinished;
                match.StateChanged += OnStateChanged;
            }
        }

        void Awake()
        {
            if (uiRoot == gameObject) uiRoot = null; // would switch this script off

            if (!UiComplete())
            {
                if (uiRoot != null) Destroy(uiRoot); // half-assigned: start clean
                BuildUI();
            }

            EnsureEventSystem();

            cutsceneSkipButton.onClick.AddListener(EndCutscene);
            continueButton.onClick.AddListener(EnterRewards);
            claimButton.onClick.AddListener(Claim);
            watchAdButton.onClick.AddListener(WatchAd);
            noThanksButton.onClick.AddListener(NoThanks);
            playAgainButton.onClick.AddListener(PlayAgain);
            mainMenuButton.onClick.AddListener(GoToMainMenu);

            var m = match;               // assigned in the inspector
            match = null;
            SetMatch(m);

            HideAll();
        }

        void OnDestroy() => SetMatch(null);

        bool UiComplete() =>
            uiRoot != null && cutscenePanel != null && timeUpText != null && winnerGroup != null && winnerNameText != null &&
            winnerImage != null && cutsceneSkipButton != null && leaderboardPanel != null && rows != null && rows.Length > 0 &&
            continueButton != null && rewardsPanel != null && coinsText != null && cashText != null && claimButton != null &&
            adPopup != null && adPopupText != null && watchAdButton != null && noThanksButton != null && endButtons != null &&
            playAgainButton != null && mainMenuButton != null;

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        void HideAll()
        {
            CurrentStep = Step.Hidden;
            pending = false;
            cutscenePanel.SetActive(false);
            leaderboardPanel.SetActive(false);
            rewardsPanel.SetActive(false);
        }

        // ------------------------------------------------------------------ the flow

        void OnStateChanged(MatchState state)
        {
            if (state != MatchState.Finished) HideAll(); // a new match started
        }

        void OnFinished(MatchResult matchResult)
        {
            result = matchResult;
            ranking = match.GetRanking();
            multiplier = 1;
            claimed = false;
            popupHandled = false;
            delayTimer = startDelay;
            pending = true;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (pending)
            {
                delayTimer -= dt;
                if (delayTimer <= 0f) { pending = false; EnterTimeUp(); }
                return;
            }

            switch (CurrentStep)
            {
                case Step.TimeUp:
                    stepTimer -= dt;
                    if (stepTimer <= 0f) EnterCutscene();
                    break;

                case Step.Cutscene:
                    if (!customCutscene)
                    {
                        stepTimer -= dt;
                        if (stepTimer <= 0f) EndCutscene();
                    }
                    break;

                case Step.Rewards:
                    UpdateRewards(dt);
                    break;
            }
        }

        // ---- 1. time up
        void EnterTimeUp()
        {
            CurrentStep = Step.TimeUp;
            stepTimer = timeUpSeconds;
            cutscenePanel.SetActive(true);
            timeUpText.gameObject.SetActive(true);
            winnerGroup.SetActive(false);
        }

        // ---- 2. winner cutscene
        void EnterCutscene()
        {
            CurrentStep = Step.Cutscene;
            timeUpText.gameObject.SetActive(false);

            MatchPlayer winner = ranking.Count > 0 ? ranking[0] : null;
            if (winner == null) { EnterLeaderboard(); return; }

            if (CutsceneRequested != null)
            {
                // someone else plays the cutscene (for example a bought one)
                customCutscene = true;
                cutscenePanel.SetActive(false);
                CutsceneRequested(winner, EndCutscene);
                return;
            }

            customCutscene = false;
            stepTimer = cutsceneSeconds;
            winnerGroup.SetActive(true);

            winnerLabelText.text = "WINNER";
            winnerNameText.text = winner.name.ToUpperInvariant() + (winner.isLocal ? "  (YOU)" : "");
            winnerVehicleText.text = winner.definition != null ? winner.definition.displayName.ToUpperInvariant() : "";

            if (winner.definition != null)
            {
                if (winnerStage == null)
                {
                    winnerStage = new GameObject("Winner Stage").AddComponent<VehiclePreviewStage>();
                    winnerStage.BuildStage();
                }
                winnerStage.gameObject.SetActive(true);
                winnerStage.Show(winner.definition);
                winnerImage.texture = winnerStage.Texture;
                winnerImage.color = Color.white;
                winnerImage.gameObject.SetActive(true);
            }
            else
            {
                winnerImage.gameObject.SetActive(false);
            }
        }

        void EndCutscene()
        {
            if (CurrentStep != Step.Cutscene) return;
            if (winnerStage != null) winnerStage.gameObject.SetActive(false);
            EnterLeaderboard();
        }

        // ---- 3. leaderboard
        void EnterLeaderboard()
        {
            CurrentStep = Step.Leaderboard;
            cutscenePanel.SetActive(false);
            leaderboardPanel.SetActive(true);

            for (int i = 0; i < rows.Length; i++)
            {
                LeaderboardRow row = rows[i];
                bool has = i < ranking.Count;
                row.root.SetActive(has);
                if (!has) continue;

                MatchPlayer p = ranking[i];
                Color color = p.isLocal ? localRowColor : Color.white;

                Set(row.position, (i + 1).ToString(), color);
                Set(row.playerName, p.name + (p.isLocal ? "  (YOU)" : ""), color);
                Set(row.score, p.points.ToString("N0"), color);
                Set(row.distance, p.distance >= 1000f ? (p.distance / 1000f).ToString("0.0") + " km" : Mathf.RoundToInt(p.distance) + " m", color);
                Set(row.sideBumps, p.npcBumps.ToString(), color);
                Set(row.bestChain, p.maxChain.ToString(), color);
            }
        }

        static void Set(Text text, string value, Color color)
        {
            if (text == null) return;
            text.text = value;
            text.color = color;
        }

        // ---- 4. rewards
        void EnterRewards()
        {
            if (CurrentStep != Step.Leaderboard) return;

            CurrentStep = Step.Rewards;
            leaderboardPanel.SetActive(false);
            rewardsPanel.SetActive(true);

            countUp = 0f;
            multiplier = 1;
            popupHandled = false;
            claimed = false;

            adPopup.SetActive(false);
            claimButton.gameObject.SetActive(false);
            endButtons.SetActive(false);
            if (multiplierText != null) multiplierText.gameObject.SetActive(false);
            if (walletText != null) walletText.gameObject.SetActive(false);
            UpdateRewardTexts(0f);
        }

        void UpdateRewards(float dt)
        {
            if (claimed) return;

            countUp = Mathf.Min(1f, countUp + dt / Mathf.Max(0.01f, countUpSeconds));
            UpdateRewardTexts(1f - (1f - countUp) * (1f - countUp)); // eases out

            if (countUp >= 1f && !popupHandled && !adPopup.activeSelf)
            {
                popupHandled = true;
                if (CanOfferDouble()) ShowAdPopup();
                else claimButton.gameObject.SetActive(true);
            }
        }

        void UpdateRewardTexts(float t)
        {
            coinsText.text = "+" + Mathf.RoundToInt(result.coins * multiplier * t);
            cashText.text = "+" + Mathf.RoundToInt(result.cash * multiplier * t);
        }

        // ---- the "double your rewards" offer
        bool CanOfferDouble()
        {
            if (!offerDoubleReward || result.coins + result.cash <= 0) return false;
            if (DoublesToday() >= maxDoublesPerDay) return false;
            return EnsureAdProvider();
        }

        bool EnsureAdProvider()
        {
            if (RewardedAds.IsReady) return true;

            if (simulateAdsWithoutSdk && (Application.isEditor || Debug.isDebugBuild))
            {
                if (devAds == null) devAds = gameObject.AddComponent<DevRewardedAdProvider>();
                devAds.duration = simulatedAdSeconds;
                RewardedAds.Provider = devAds;
                return true;
            }
            return false;
        }

        void ShowAdPopup()
        {
            adPopup.SetActive(true);
            adPopupText.text = "DOUBLE YOUR REWARDS?\nWatch a short video, or miss out!";
            watchAdButton.gameObject.SetActive(true);
            watchAdButton.interactable = true;
            noThanksButton.interactable = true;
        }

        void WatchAd()
        {
            watchAdButton.interactable = false;
            noThanksButton.interactable = false;
            adPopupText.text = "Playing ad...";
            RewardedAds.Show(adPlacement, OnAdFinished);
        }

        void OnAdFinished(bool watched)
        {
            if (watched)
            {
                multiplier = 2;
                CountDouble();
                countUp = 0f; // count up again to the doubled amount
                if (multiplierText != null)
                {
                    multiplierText.text = "x2";
                    multiplierText.gameObject.SetActive(true);
                }
                adPopup.SetActive(false);
                claimButton.gameObject.SetActive(true);
            }
            else
            {
                adPopupText.text = "No ad is available right now.";
                watchAdButton.gameObject.SetActive(false);
                noThanksButton.interactable = true;
            }
        }

        void NoThanks()
        {
            adPopup.SetActive(false);
            claimButton.gameObject.SetActive(true);
        }

        static string Today => DateTime.Now.ToString("yyyyMMdd");

        static int DoublesToday() => PlayerPrefs.GetString(DayKey, "") == Today ? PlayerPrefs.GetInt(CountKey, 0) : 0;

        static void CountDouble()
        {
            PlayerPrefs.SetInt(CountKey, DoublesToday() + 1);
            PlayerPrefs.SetString(DayKey, Today);
            PlayerPrefs.Save();
        }

        // ---- 5. claim, then what next
        void Claim()
        {
            if (claimed) return;
            claimed = true;

            int coins = result.coins * multiplier;
            int cash = result.cash * multiplier;
            coinsText.text = "+" + coins;
            cashText.text = "+" + cash;

            LocalWallet.Add(coins, cash);
            RewardsClaimed?.Invoke(coins, cash);

            claimButton.gameObject.SetActive(false);
            adPopup.SetActive(false);
            endButtons.SetActive(true);

            if (walletText != null)
            {
                walletText.text = "YOU HAVE   " + LocalWallet.Coins.ToString("N0") + " COINS   " + LocalWallet.Cash.ToString("N0") + " CASH";
                walletText.gameObject.SetActive(true);
            }
        }

        void PlayAgain()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void GoToMainMenu()
        {
            Time.timeScale = 1f;
            if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {
                Debug.LogError($"MatchResultsFlow: scene '{mainMenuScene}' is not in the Build Settings (or the name is wrong).", this);
                return;
            }
            SceneManager.LoadScene(mainMenuScene);
        }

        // ------------------------------------------------------------------ building the UI

        /// <summary>
        /// Creates the default screens under this object and fills the UI fields. Used at runtime when no UI is assigned, and by the
        /// editor tool. Sample texts are filled in so you can style it in the editor.
        /// </summary>
        public void BuildUI()
        {
            var canvasGo = NewUI("Match Results Canvas", transform,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 160; // above the touch buttons (100) and the pause menu (200 is still on top)

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Transform root = canvasGo.transform;
            Vector2 topC = new Vector2(0.5f, 1f), mid = new Vector2(0.5f, 0.5f), botC = new Vector2(0.5f, 0f);

            // =============================== cutscene
            cutscenePanel = Panel("Cutscene Panel", root, new Color(0f, 0f, 0f, 0.88f));
            cutsceneSkipButton = SkipButton(cutscenePanel.transform);

            timeUpText = Label(cutscenePanel.transform, "Time Up", "TIME UP", 160, accentColor, mid, new Vector2(0f, 0f), new Vector2(1400f, 220f));

            winnerGroup = NewUI("Winner Group", cutscenePanel.transform, typeof(RectTransform));
            Stretch((RectTransform)winnerGroup.transform);
            winnerLabelText = Label(winnerGroup.transform, "Winner Label", "WINNER", 80, accentColor, mid, new Vector2(0f, 440f), new Vector2(1000f, 110f));

            var imageGo = NewUI("Winner Vehicle", winnerGroup.transform, typeof(RectTransform), typeof(RawImage));
            Place((RectTransform)imageGo.transform, mid, new Vector2(0f, 20f), new Vector2(853f, 640f));
            winnerImage = imageGo.GetComponent<RawImage>();
            winnerImage.color = new Color(0.13f, 0.14f, 0.19f, 1f);
            winnerImage.raycastTarget = false;

            winnerNameText = Label(winnerGroup.transform, "Winner Name", "PLAYER NAME", 100, Color.white, mid, new Vector2(0f, -385f), new Vector2(1500f, 130f));
            winnerVehicleText = Label(winnerGroup.transform, "Winner Vehicle Name", "VEHICLE NAME", 52, new Color(1f, 1f, 1f, 0.7f), mid, new Vector2(0f, -475f), new Vector2(1200f, 70f));

            // =============================== leaderboard
            leaderboardPanel = Panel("Leaderboard Panel", root, panelColor);
            Label(leaderboardPanel.transform, "Title", "LEADERBOARD", 90, accentColor, topC, new Vector2(0f, -80f), new Vector2(1200f, 120f));

            float[] widths = { 130f, 500f, 230f, 260f, 260f, 220f };
            string[] headers = { "#", "PLAYER", "SCORE", "DISTANCE", "SIDE BUMPS", "BEST CHAIN" };
            float[] centres = new float[widths.Length];
            float x = -800f;
            for (int c = 0; c < widths.Length; c++) { centres[c] = x + widths[c] * 0.5f; x += widths[c]; }

            var header = NewUI("Header Row", leaderboardPanel.transform, typeof(RectTransform));
            Place((RectTransform)header.transform, topC, new Vector2(0f, -210f), new Vector2(1600f, 60f));
            for (int c = 0; c < widths.Length; c++)
                Label(header.transform, "Header " + headers[c], headers[c], fontSize - 8, new Color(1f, 1f, 1f, 0.6f), mid, new Vector2(centres[c], 0f), new Vector2(widths[c], 56f));

            rows = new LeaderboardRow[8];
            for (int i = 0; i < rows.Length; i++)
            {
                var rowGo = NewUI("Row " + (i + 1), leaderboardPanel.transform, typeof(RectTransform), typeof(Image));
                Place((RectTransform)rowGo.transform, topC, new Vector2(0f, -290f - i * 72f), new Vector2(1640f, 64f));
                var bg = rowGo.GetComponent<Image>();
                bg.color = new Color(1f, 1f, 1f, i % 2 == 0 ? 0.08f : 0.03f);
                bg.raycastTarget = false;

                rows[i] = new LeaderboardRow
                {
                    root = rowGo,
                    position = Label(rowGo.transform, "Position", (i + 1).ToString(), fontSize, Color.white, mid, new Vector2(centres[0], 0f), new Vector2(widths[0], 60f)),
                    playerName = Label(rowGo.transform, "Player", i == 0 ? "PLAYER" : "BOT " + i, fontSize, Color.white, mid, new Vector2(centres[1], 0f), new Vector2(widths[1], 60f), TextAnchor.MiddleLeft),
                    score = Label(rowGo.transform, "Score", "1,240", fontSize, Color.white, mid, new Vector2(centres[2], 0f), new Vector2(widths[2], 60f)),
                    distance = Label(rowGo.transform, "Distance", "2.4 km", fontSize, Color.white, mid, new Vector2(centres[3], 0f), new Vector2(widths[3], 60f)),
                    sideBumps = Label(rowGo.transform, "Side Bumps", "12", fontSize, Color.white, mid, new Vector2(centres[4], 0f), new Vector2(widths[4], 60f)),
                    bestChain = Label(rowGo.transform, "Best Chain", "4", fontSize, Color.white, mid, new Vector2(centres[5], 0f), new Vector2(widths[5], 60f))
                };
            }

            continueButton = MakeButton(leaderboardPanel.transform, "CONTINUE", botC, new Vector2(0f, 70f), new Vector2(460f, 110f));

            // =============================== rewards
            rewardsPanel = Panel("Rewards Panel", root, panelColor);
            Label(rewardsPanel.transform, "Title", "REWARDS", 90, accentColor, topC, new Vector2(0f, -100f), new Vector2(1200f, 120f));

            Label(rewardsPanel.transform, "Coins Label", "COINS", fontSize + 8, new Color(1f, 1f, 1f, 0.7f), mid, new Vector2(-320f, 150f), new Vector2(500f, 70f));
            coinsText = Label(rewardsPanel.transform, "Coins", "+124", 130, accentColor, mid, new Vector2(-320f, 40f), new Vector2(600f, 170f));
            Label(rewardsPanel.transform, "Cash Label", "CASH", fontSize + 8, new Color(1f, 1f, 1f, 0.7f), mid, new Vector2(320f, 150f), new Vector2(500f, 70f));
            cashText = Label(rewardsPanel.transform, "Cash", "+31", 130, new Color(0.4f, 0.9f, 0.5f, 1f), mid, new Vector2(320f, 40f), new Vector2(600f, 170f));

            multiplierText = Label(rewardsPanel.transform, "Multiplier", "x2", 90, accentColor, mid, new Vector2(0f, -110f), new Vector2(400f, 120f));
            walletText = Label(rewardsPanel.transform, "Wallet", "YOU HAVE   500 COINS   120 CASH", fontSize, new Color(1f, 1f, 1f, 0.8f), mid, new Vector2(0f, -230f), new Vector2(1500f, 70f));

            claimButton = MakeButton(rewardsPanel.transform, "CLAIM", botC, new Vector2(0f, 90f), new Vector2(520f, 130f));

            endButtons = NewUI("End Buttons", rewardsPanel.transform, typeof(RectTransform));
            Stretch((RectTransform)endButtons.transform);
            playAgainButton = MakeButton(endButtons.transform, "PLAY AGAIN", botC, new Vector2(-270f, 90f), new Vector2(480f, 130f));
            mainMenuButton = MakeButton(endButtons.transform, "MAIN MENU", botC, new Vector2(270f, 90f), new Vector2(480f, 130f));

            adPopup = NewUI("Ad Popup", rewardsPanel.transform, typeof(RectTransform), typeof(Image));
            Place((RectTransform)adPopup.transform, mid, Vector2.zero, new Vector2(1100f, 560f));
            var popupBg = adPopup.GetComponent<Image>();
            popupBg.color = new Color(0.1f, 0.1f, 0.14f, 0.99f);
            adPopupText = Label(adPopup.transform, "Message", "DOUBLE YOUR REWARDS?\nWatch a short video, or miss out!", fontSize + 10, Color.white, mid, new Vector2(0f, 90f), new Vector2(1000f, 280f));
            watchAdButton = MakeButton(adPopup.transform, "WATCH AD", botC, new Vector2(-250f, 60f), new Vector2(450f, 120f));
            noThanksButton = MakeButton(adPopup.transform, "NO THANKS", botC, new Vector2(250f, 60f), new Vector2(450f, 120f));

            // the screens start hidden
            cutscenePanel.SetActive(false);
            leaderboardPanel.SetActive(false);
            rewardsPanel.SetActive(false);
        }

        GameObject Panel(string name, Transform parent, Color color)
        {
            var go = NewUI(name, parent, typeof(RectTransform), typeof(Image));
            Stretch((RectTransform)go.transform);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true; // blocks whatever is underneath
            return go;
        }

        Button SkipButton(Transform parent)
        {
            var go = NewUI("Skip Button", parent, typeof(RectTransform), typeof(Image), typeof(Button));
            Stretch((RectTransform)go.transform);
            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.004f); // invisible but clickable
            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            button.transition = Selectable.Transition.None;
            return button;
        }

        Button MakeButton(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = NewUI(label + " Button", parent, typeof(RectTransform), typeof(Image), typeof(Button));
            Place((RectTransform)go.transform, anchor, position, size, new Vector2(0.5f, 0f));

            var img = go.GetComponent<Image>();
            img.color = Color.white; // the Button tints this

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = buttonColor;
            colors.highlightedColor = buttonColor * 1.4f;
            colors.pressedColor = buttonColor * 1.8f;
            colors.selectedColor = buttonColor * 1.4f;
            button.colors = colors;

            var text = Label(go.transform, "Label", label, fontSize + 6, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Stretch((RectTransform)text.transform);
            return button;
        }

        Text Label(Transform parent, string name, string text, int size, Color color, Vector2 anchor, Vector2 position, Vector2 sizeDelta,
                   TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = NewUI(name, parent, typeof(RectTransform), typeof(Text));
            Place((RectTransform)go.transform, anchor, position, sizeDelta);

            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static GameObject NewUI(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, components);
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) go.layer = ui;
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
