using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace VehicleSystem
{
    /// <summary>
    /// Everything the player sees of the match: time left, points and position (live), the 3-2-1-GO countdown, "+20 SIDE BUMP"
    /// pop-ups, and the results screen when time is up (score, position, coins and cash earned, PLAY AGAIN / MAIN MENU).
    /// LocalPlayerRig gives it the MatchManager (SetMatch).
    ///
    /// UI source (same idea as the other UI scripts):
    ///   - If "Ui Root" is assigned (Tools > Vehicle > Generate Match HUD UI, or the button on this component) the objects in
    ///     your Hierarchy are used: move, resize and restyle them freely.
    ///   - If it is empty, a default one is built at runtime.
    /// Only texts, colours' transparency, the pop-up movement and which parts are visible are written at runtime. Button
    /// clicks are connected in code.
    /// </summary>
    public class MatchHud : MonoBehaviour
    {
        [Header("Source (assigned by LocalPlayerRig)")]
        public MatchManager match;

        [Header("Live HUD (filled in by the editor tool)")]
        [Tooltip("Parent object of everything (its Canvas).")]
        public GameObject uiRoot;
        [Tooltip("The panel with time, points and position. Hidden before the match.")]
        public GameObject hudPanel;
        public Text timeText;
        public Text pointsText;
        public Text positionText;
        [Tooltip("Score pop-ups, used in turn. Keep a few so quick events do not overwrite each other.")]
        public Text[] popups;
        [Tooltip("The big 3-2-1-GO in the middle.")]
        public Text centerText;

        [Header("Results screen")]
        public GameObject resultsPanel;
        public Text resultsTitle;
        public Text resultsBody;
        public Button playAgainButton;
        public Button mainMenuButton;
        [Tooltip("Scene loaded by MAIN MENU (it must be in the Build Settings).")]
        public string mainMenuScene = "MainMenu";

        [Header("Texts")]
        public string goText = "GO!";
        public string timeUpTitle = "TIME UP";

        [Header("Pop-ups")]
        public float popupTime = 1.4f;
        [Tooltip("Pixels a pop-up rises while it fades.")]
        public float popupRise = 60f;
        public Color gainColor = new Color(1f, 0.85f, 0.3f, 1f);
        public Color lossColor = new Color(1f, 0.3f, 0.25f, 1f);

        [Header("Default look (editor tool + runtime fallback)")]
        public int fontSize = 40;
        public Color panelColor = new Color(0f, 0f, 0f, 0.45f);
        public Color buttonColor = new Color(0.22f, 0.22f, 0.3f, 1f);

        float goTimer, urgentFlash;
        float[] popupTimers;
        Vector2[] popupBase;
        int nextPopup;

        /// <summary>The match to show (null hides everything). Called by LocalPlayerRig.</summary>
        public void SetMatch(MatchManager newMatch)
        {
            Unsubscribe();
            match = newMatch;
            if (match != null)
            {
                match.StateChanged += OnStateChanged;
                match.PointsAwarded += OnPoints;
                match.Finished += OnFinished;
            }
            Refresh();
        }

        void Unsubscribe()
        {
            if (match == null) return;
            match.StateChanged -= OnStateChanged;
            match.PointsAwarded -= OnPoints;
            match.Finished -= OnFinished;
        }

        void Awake()
        {
            if (uiRoot == gameObject) uiRoot = null; // would switch this script off

            if (uiRoot == null || hudPanel == null || timeText == null || pointsText == null || positionText == null ||
                centerText == null || resultsPanel == null || resultsBody == null || playAgainButton == null || mainMenuButton == null)
            {
                if (uiRoot != null) Destroy(uiRoot); // half-assigned: start clean
                BuildUI();
            }

            popupTimers = new float[popups != null ? popups.Length : 0];
            popupBase = new Vector2[popupTimers.Length];
            for (int i = 0; i < popupTimers.Length; i++)
            {
                popupBase[i] = ((RectTransform)popups[i].transform).anchoredPosition;
                popups[i].gameObject.SetActive(false);
            }

            EnsureEventSystem();
            playAgainButton.onClick.AddListener(PlayAgain);
            mainMenuButton.onClick.AddListener(GoToMainMenu);

            var m = match;               // assigned in the inspector
            match = null;
            SetMatch(m);
        }

        void OnDestroy() => Unsubscribe();

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

        // ------------------------------------------------------------------ events

        void OnStateChanged(MatchState state)
        {
            if (state == MatchState.Running) goTimer = 0.8f;
            Refresh();
        }

        void OnFinished(MatchResult result) => Refresh();

        void OnPoints(MatchPlayer player, int points, ScoreReason reason)
        {
            if (player == null || !player.isLocal || popups == null || popups.Length == 0) return;

            int i = nextPopup;
            nextPopup = (nextPopup + 1) % popups.Length;

            Text popup = popups[i];
            popup.text = (points > 0 ? "+" : "") + points + "   " + Label(reason);
            popup.color = points >= 0 ? gainColor : lossColor;
            popup.gameObject.SetActive(true);
            popupTimers[i] = popupTime;
        }

        static string Label(ScoreReason reason)
        {
            switch (reason)
            {
                case ScoreReason.Distance: return "DISTANCE";
                case ScoreReason.NpcBump: return "SIDE BUMP";
                case ScoreReason.Chain: return "CHAIN";
                case ScoreReason.RivalRam: return "RAM";
                case ScoreReason.Overtake: return "OVERTAKE";
                case ScoreReason.Crossing: return "CROSSING";
                case ScoreReason.Destroyed: return "DESTROYED";
                case ScoreReason.WrongWay: return "WRONG WAY";
                default: return "";
            }
        }

        // ------------------------------------------------------------------ showing

        void Refresh()
        {
            bool active = match != null;
            MatchState state = active ? match.State : MatchState.Waiting;

            hudPanel.SetActive(active && state != MatchState.Waiting);
            centerText.gameObject.SetActive(active && state == MatchState.Countdown);
            resultsPanel.SetActive(active && state == MatchState.Finished);

            if (active && state == MatchState.Finished) FillResults();
        }

        void FillResults()
        {
            MatchResult r = match.Result;
            resultsTitle.text = timeUpTitle;
            resultsBody.text =
                "SCORE   " + r.points.ToString("N0") + "\n" +
                "POSITION   " + Ordinal(r.rank) + " of " + r.playerCount + "\n" +
                "DISTANCE   " + (r.distance / 1000f).ToString("0.0") + " km\n" +
                "SIDE BUMPS   " + r.npcBumps + "     BEST CHAIN   " + r.maxChain + "\n" +
                "COINS   +" + r.coins + "     CASH   +" + r.cash;
        }

        void Update()
        {
            if (match == null) return;

            if (match.State == MatchState.Countdown)
            {
                centerText.text = Mathf.Max(1, Mathf.CeilToInt(match.CountdownLeft)).ToString();
            }
            else if (goTimer > 0f)
            {
                goTimer -= Time.unscaledDeltaTime;
                centerText.text = goText;
                centerText.gameObject.SetActive(goTimer > 0f);
            }

            if (hudPanel.activeSelf)
            {
                int seconds = Mathf.CeilToInt(match.TimeLeft);
                timeText.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
                timeText.color = match.State == MatchState.Running && seconds <= 10 && Mathf.FloorToInt(Time.unscaledTime * 3f) % 2 == 0
                    ? lossColor : Color.white; // blinks red in the last 10 seconds

                MatchPlayer local = match.LocalPlayer;
                if (local != null)
                {
                    pointsText.text = local.points.ToString("N0");
                    positionText.text = Ordinal(match.GetRank(local)) + " / " + match.Players.Count;
                }
            }

            UpdatePopups();
        }

        void UpdatePopups()
        {
            for (int i = 0; i < popupTimers.Length; i++)
            {
                if (popupTimers[i] <= 0f) continue;

                popupTimers[i] -= Time.unscaledDeltaTime;
                Text popup = popups[i];

                if (popupTimers[i] <= 0f) { popup.gameObject.SetActive(false); continue; }

                float progress = 1f - popupTimers[i] / popupTime;
                ((RectTransform)popup.transform).anchoredPosition = popupBase[i] + Vector2.up * (popupRise * progress);

                Color c = popup.color;
                c.a = Mathf.Clamp01(popupTimers[i] / 0.4f); // fades during the last 0.4 s
                popup.color = c;
            }
        }

        static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }

        // ------------------------------------------------------------------ buttons

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
                Debug.LogError($"MatchHud: scene '{mainMenuScene}' is not in the Build Settings (or the name is wrong).", this);
                return;
            }
            SceneManager.LoadScene(mainMenuScene);
        }

        // ------------------------------------------------------------------ building the UI

        /// <summary>
        /// Creates the default HUD under this object and fills the UI fields. Used at runtime when no UI is assigned, and by the
        /// editor tool. Sample text is filled in so you can style it in the editor.
        /// </summary>
        public void BuildUI()
        {
            var canvasGo = NewUI("Match Canvas", transform,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 94;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Transform root = canvasGo.transform;
            Vector2 topRight = new Vector2(1f, 1f);
            Vector2 mid = new Vector2(0.5f, 0.5f);

            // ---- live panel (top right, under the RESET / BACK buttons)
            hudPanel = NewUI("Match Panel", root, typeof(RectTransform), typeof(Image));
            Place((RectTransform)hudPanel.transform, topRight, topRight, topRight, new Vector2(-40f, -210f), new Vector2(330f, 200f));
            var panelImg = hudPanel.GetComponent<Image>();
            panelImg.color = panelColor;
            panelImg.raycastTarget = false;

            timeText = MakeText(hudPanel.transform, "Time", "03:00", FontStyle.Bold, Color.white, fontSize + 24, TextAnchor.MiddleCenter);
            Place((RectTransform)timeText.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(310f, 80f));

            pointsText = MakeText(hudPanel.transform, "Points", "1,240", FontStyle.Bold, gainColor, fontSize + 14, TextAnchor.MiddleCenter);
            Place((RectTransform)pointsText.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(310f, 64f));

            positionText = MakeText(hudPanel.transform, "Position", "1st / 4", FontStyle.Bold, new Color(1f, 1f, 1f, 0.85f), fontSize, TextAnchor.MiddleCenter);
            Place((RectTransform)positionText.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(310f, 46f));

            // ---- pop-ups (middle, a little above the vehicle)
            popups = new Text[4];
            for (int i = 0; i < popups.Length; i++)
            {
                popups[i] = MakeText(root, "Popup " + (i + 1), "+20   SIDE BUMP", FontStyle.Bold, gainColor, fontSize + 8, TextAnchor.MiddleCenter);
                Place((RectTransform)popups[i].transform, mid, mid, mid, new Vector2(0f, 150f - i * 6f), new Vector2(700f, 70f));
            }

            // ---- countdown
            centerText = MakeText(root, "Countdown", "3", FontStyle.Bold, Color.white, 180, TextAnchor.MiddleCenter);
            Place((RectTransform)centerText.transform, mid, mid, mid, new Vector2(0f, 120f), new Vector2(600f, 240f));

            // ---- results (its own canvas on top, so it also covers the touch buttons)
            resultsPanel = NewUI("Results Panel", root, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
            var resultsCanvas = resultsPanel.GetComponent<Canvas>();
            resultsCanvas.overrideSorting = true;
            resultsCanvas.sortingOrder = 150;
            Stretch((RectTransform)resultsPanel.transform);
            var dim = resultsPanel.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.7f);
            dim.raycastTarget = true; // blocks anything under it

            var card = NewUI("Results Card", resultsPanel.transform, typeof(RectTransform), typeof(Image));
            Place((RectTransform)card.transform, mid, mid, mid, new Vector2(0f, 0f), new Vector2(940f, 720f));
            card.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 0.98f);

            resultsTitle = MakeText(card.transform, "Title", timeUpTitle, FontStyle.Bold, gainColor, fontSize + 36, TextAnchor.MiddleCenter);
            Place((RectTransform)resultsTitle.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(880f, 110f));

            resultsBody = MakeText(card.transform, "Body", "SCORE   1,240\nPOSITION   1st of 1\nDISTANCE   2.4 km\nSIDE BUMPS   12     BEST CHAIN   4\nCOINS   +124     CASH   +31",
                                   FontStyle.Bold, Color.white, fontSize, TextAnchor.UpperCenter);
            Place((RectTransform)resultsBody.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(880f, 380f));
            resultsBody.lineSpacing = 1.35f;

            playAgainButton = MakeButton(card.transform, "PLAY AGAIN", new Vector2(-230f, 70f), new Vector2(400f, 110f));
            mainMenuButton = MakeButton(card.transform, "MAIN MENU", new Vector2(230f, 70f), new Vector2(400f, 110f));
        }

        Button MakeButton(Transform parent, string label, Vector2 position, Vector2 size)
        {
            var go = NewUI(label + " Button", parent, typeof(RectTransform), typeof(Image), typeof(Button));
            Vector2 bottomCentre = new Vector2(0.5f, 0f);
            Place((RectTransform)go.transform, bottomCentre, bottomCentre, new Vector2(0.5f, 0f), position, size);

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

            var text = MakeText(go.transform, "Label", label, FontStyle.Bold, Color.white, fontSize, TextAnchor.MiddleCenter);
            Stretch((RectTransform)text.transform);
            return button;
        }

        static Text MakeText(Transform parent, string name, string text, FontStyle style, Color color, int size, TextAnchor align)
        {
            var go = NewUI(name, parent, typeof(RectTransform), typeof(Text));
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
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
