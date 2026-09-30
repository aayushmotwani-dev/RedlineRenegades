using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Redline
{
    public enum RacePhase { Title, Countdown, Racing, Paused, Finished, Defeat }

    [DisallowMultipleComponent]
    public sealed class RedlineGame : MonoBehaviour
    {
        public static RedlineGame I { get; private set; }
        public RacePhase Phase { get; private set; } = RacePhase.Title;
        public BikeMotor Player { get; private set; }
        public Camera GameCamera { get; private set; }
        public float RaceTime { get; private set; }
        public int Score { get; private set; }
        public int PlayerPosition { get; private set; } = 1;
        public int Checkpoint { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;

        readonly List<BikeMotor> racers = new List<BikeMotor>();
        readonly List<BikeMotor> finishers = new List<BikeMotor>();
        Texture2D pixel;
        Font displayFont;
        GUIStyle titleStyle;
        GUIStyle headerStyle;
        GUIStyle bodyStyle;
        GUIStyle smallStyle;
        GUIStyle numberStyle;
        float styleScale = -1f;
        float statusUntil;
        string countdown = string.Empty;
        string defeatReason = string.Empty;
        bool qaSmoke;
        bool qaVisual;
        string qaDirectory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureGame()
        {
            if (FindAnyObjectByType<RedlineGame>() == null)
                new GameObject("Redline Renegades Game").AddComponent<RedlineGame>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Application.targetFrameRate = 120;
            Time.fixedDeltaTime = 1f / 60f;
            Time.timeScale = 1f;
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            BuildGame();
            ConfigureQualityAssurance();
        }

        void BuildGame()
        {
            WorldBuilder world = new GameObject("Redline World").AddComponent<WorldBuilder>();
            world.Build();

            Player = BikeMotor.Create("RIDER 07", new Color(0.79f, 0.13f, 0.055f), true, 18f, -0.75f);
            racers.Add(Player);
            racers.Add(BikeMotor.Create("VEX", new Color(0.08f, 0.22f, 0.28f), false, 21f, 1.2f));
            racers.Add(BikeMotor.Create("MAKO", new Color(0.88f, 0.51f, 0.08f), false, 14f, 2.5f));
            racers.Add(BikeMotor.Create("ROOK", new Color(0.19f, 0.39f, 0.2f), false, 11f, -2.6f));
            racers.Add(BikeMotor.Create("SABLE", new Color(0.33f, 0.18f, 0.42f), false, 7f, 1.15f));
            racers.Add(BikeMotor.Create("BOLT", new Color(0.64f, 0.52f, 0.19f), false, 4f, -1.1f));

            for (int i = 0; i < 12; i++)
            {
                float start = 165f + i * 132f;
                float lane = i % 2 == 0 ? -2.65f : 2.65f;
                TrafficVehicle.Create(i, start, lane, 17f + (i % 4) * 1.7f);
            }

            GameCamera = ChaseCamera.Create(Player);
            RedlineAudio.StartAmbience();
            StatusMessage = "MOUNTAIN PASS 07  ·  1.8 KM";
        }

        void ConfigureQualityAssurance()
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (argument.Equals("-redlineSmoke", StringComparison.OrdinalIgnoreCase)) qaSmoke = true;
                else if (argument.Equals("-redlineVisual", StringComparison.OrdinalIgnoreCase)) qaVisual = true;
                else if (argument.StartsWith("-redlineQaDir=", StringComparison.OrdinalIgnoreCase))
                    qaDirectory = argument.Substring("-redlineQaDir=".Length).Trim('"');
            }

            if (!qaSmoke && !qaVisual) return;
            if (string.IsNullOrWhiteSpace(qaDirectory)) qaDirectory = Path.Combine(Application.persistentDataPath, "RedlineQA");
            Directory.CreateDirectory(qaDirectory);
            Player.EnableAutomation(0.35f);
            StartCoroutine(QualityAssuranceRoutine());
        }

        IEnumerator QualityAssuranceRoutine()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            if (qaVisual) ScreenCapture.CaptureScreenshot(Path.Combine(qaDirectory, "01-title.png"));
            yield return new WaitForSecondsRealtime(0.6f);

            StartCoroutine(CountdownRoutine());
            yield return new WaitForSecondsRealtime(0.2f);
            if (qaVisual) ScreenCapture.CaptureScreenshot(Path.Combine(qaDirectory, "01-grid.png"));
            while (Phase != RacePhase.Racing) yield return null;
            float startProgress = Player.Progress;
            RiderCombat combat = Player.GetComponent<RiderCombat>();
            bool combatStarted = combat != null && combat.BeginAttack(1);
            float raceStart = Time.unscaledTime;
            bool raceCaptured = false;
            bool showcaseAttackQueued = false;
            float showcaseAttackTime = 0f;

            while (Time.unscaledTime - raceStart < 10.5f && Phase == RacePhase.Racing)
            {
                float waveLane = Mathf.Sin((Time.unscaledTime - raceStart) * 0.42f) * 1.15f;
                Player.SetAIControls(1f, 0f, 0f, waveLane, Player.Boost > 0.72f && Time.unscaledTime - raceStart > 4f);
                float raceAge = Time.unscaledTime - raceStart;
                if (qaVisual && !showcaseAttackQueued && raceAge > 4.4f && !Player.IsWrecked && Player.SpeedKph > 70f)
                {
                    showcaseAttackQueued = combat.BeginAttack(1);
                    if (showcaseAttackQueued) showcaseAttackTime = Time.unscaledTime;
                }
                if (qaVisual && !raceCaptured && showcaseAttackQueued && Time.unscaledTime - showcaseAttackTime > 0.21f)
                {
                    raceCaptured = true;
                    ScreenCapture.CaptureScreenshot(Path.Combine(qaDirectory, "02-race.png"));
                }
                yield return null;
            }

            if (qaVisual && GameCamera != null && Player != null)
            {
                ChaseCamera chase = GameCamera.GetComponent<ChaseCamera>();
                if (chase != null) chase.enabled = false;
                float previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                Vector3 wheelView = Player.transform.position + Player.transform.right * 4.6f + Player.transform.up * 1.15f;
                GameCamera.transform.SetPositionAndRotation(wheelView,
                    Quaternion.LookRotation(Player.transform.position + Player.transform.up * 0.92f - wheelView, Player.transform.up));
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(qaDirectory, "02-wheel-close.png"));
                yield return new WaitForEndOfFrame();
                Time.timeScale = previousTimeScale;
                if (chase != null) chase.enabled = true;
            }

            bool finite = IsFinite(Player.transform.position) && IsFinite(Player.SpeedKph) && IsFinite(Player.Progress);
            bool progressed = Player.Progress - startProgress > 45f;
            bool routeValid = RoadSpline.I != null && RoadSpline.I.Length > 1700f;
            bool fieldValid = racers.Count == 6 && BikeMotor.All.Count == 6;
            bool speedValid = Player.SpeedKph > 30f;
            float wheelAxleAlignment = Player.VisualRoot != null
                ? Vector3.Dot(Player.FrontWheelAxleWorld.normalized, Player.VisualRoot.right.normalized)
                : 0f;
            bool wheelRotationValid = Player.WheelTravelDegrees > 1000f && wheelAxleAlignment > 0.97f;
            bool passed = finite && progressed && routeValid && fieldValid && speedValid && combatStarted && wheelRotationValid;
            string report =
                "REDLINE RENEGADES AUTOMATED QA\n" +
                "result=" + (passed ? "PASS" : "FAIL") + "\n" +
                "route_m=" + (RoadSpline.I != null ? RoadSpline.I.Length.ToString("F1") : "missing") + "\n" +
                "player_progress_delta_m=" + (Player.Progress - startProgress).ToString("F1") + "\n" +
                "player_speed_kph=" + Player.SpeedKph.ToString("F1") + "\n" +
                "wheel_rotation_degrees=" + Player.WheelTravelDegrees.ToString("F1") + "\n" +
                "wheel_axle_alignment=" + wheelAxleAlignment.ToString("F3") + "\n" +
                "wheel_rotation_valid=" + wheelRotationValid + "\n" +
                "racer_count=" + racers.Count + "\n" +
                "combat_started=" + combatStarted + "\n" +
                "finite_physics=" + finite + "\n" +
                "phase=" + Phase + "\n";
            File.WriteAllText(Path.Combine(qaDirectory, "qa-report.txt"), report);
            Debug.Log((passed ? "REDLINE_SMOKE_OK" : "REDLINE_SMOKE_FAILED") + "\n" + report);

            if (qaVisual && Phase == RacePhase.Racing)
            {
                TogglePause();
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(qaDirectory, "03-pause.png"));
            }
            yield return new WaitForSecondsRealtime(1.2f);
            Application.Quit(passed ? 0 : 2);
        }

        static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        void Update()
        {
            if (Phase == RacePhase.Title && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                StartCoroutine(CountdownRoutine());

            if ((Phase == RacePhase.Racing || Phase == RacePhase.Paused) && Input.GetKeyDown(KeyCode.Escape))
                TogglePause();

            if ((Phase == RacePhase.Finished || Phase == RacePhase.Defeat) && Input.GetKeyDown(KeyCode.R))
                Restart();

            if (Phase == RacePhase.Racing)
            {
                RaceTime += Time.deltaTime;
                PlayerPosition = CalculatePosition(Player);
                UpdateCheckpoints();
                UpdateFinishers();
            }
        }

        IEnumerator CountdownRoutine()
        {
            if (Phase != RacePhase.Title) yield break;
            Phase = RacePhase.Countdown;
            for (int number = 3; number >= 1; number--)
            {
                countdown = number.ToString();
                RedlineAudio.Play(RedlineSound.Countdown, Player.transform.position);
                yield return new WaitForSecondsRealtime(0.76f);
            }
            countdown = "GO";
            RedlineAudio.Play(RedlineSound.Boost, Player.transform.position);
            Phase = RacePhase.Racing;
            ShowStatus("THE RIDGE IS OPEN", 2.2f);
            yield return new WaitForSecondsRealtime(0.7f);
            countdown = string.Empty;
        }

        void UpdateCheckpoints()
        {
            int expected = Checkpoint + 1;
            float threshold = expected * (RoadSpline.I.Length / 4f);
            if (expected <= 3 && Player.Progress >= threshold)
            {
                Checkpoint = expected;
                AddScore(500, "CHECKPOINT " + Checkpoint + " CLEARED");
                Player.AddBoost(0.2f);
            }
        }

        void UpdateFinishers()
        {
            foreach (BikeMotor racer in racers)
            {
                if (racer == null || racer.Finished || racer.Progress < RoadSpline.I.Length - 24f) continue;
                racer.Finished = true;
                finishers.Add(racer);
                if (racer == Player)
                {
                    PlayerPosition = finishers.Count;
                    Score += Mathf.Max(0, 7000 - (PlayerPosition - 1) * 1100) + Mathf.RoundToInt(Player.Health * 12f + Player.Integrity * 8f);
                    Phase = RacePhase.Finished;
                    RedlineAudio.Play(RedlineSound.Finish, Player.transform.position);
                    ShowStatus("SUMMIT LINE CROSSED", 99f);
                }
            }
        }

        int CalculatePosition(BikeMotor target)
        {
            int position = 1;
            foreach (BikeMotor racer in racers)
            {
                if (racer == null || racer == target) continue;
                if (racer.Progress > target.Progress + 0.4f) position++;
            }
            return Mathf.Clamp(position, 1, racers.Count);
        }

        public void AddScore(int amount, string reason)
        {
            Score += Mathf.Max(0, amount);
            ShowStatus(reason + "  +" + amount, 1.5f);
        }

        public void ShowStatus(string text, float seconds)
        {
            StatusMessage = text;
            statusUntil = Time.unscaledTime + seconds;
        }

        public void LoseRace(string reason)
        {
            if (Phase == RacePhase.Defeat || Phase == RacePhase.Finished) return;
            defeatReason = reason;
            Phase = RacePhase.Defeat;
            Time.timeScale = 1f;
        }

        void TogglePause()
        {
            bool pause = Phase == RacePhase.Racing;
            Phase = pause ? RacePhase.Paused : RacePhase.Racing;
            Time.timeScale = pause ? 0f : 1f;
        }

        void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void OnGUI()
        {
            float ui = Mathf.Clamp(Screen.height / 720f, 0.74f, 1.35f);
            EnsureStyles(ui);
            float w = Screen.width;
            float h = Screen.height;

            if (Phase == RacePhase.Title) DrawTitle(w, h, ui);
            else DrawHud(w, h, ui);

            if (Phase == RacePhase.Countdown && !string.IsNullOrEmpty(countdown))
            {
                Fade(new Rect(0f, 0f, w, h), new Color(0.03f, 0.018f, 0.01f, 0.26f));
                Label(new Rect(0f, h * 0.32f, w, 150f * ui), countdown, titleStyle);
            }

            if (Phase == RacePhase.Paused) DrawPause(w, h, ui);
            else if (Phase == RacePhase.Finished || Phase == RacePhase.Defeat) DrawResult(w, h, ui);
        }

        void DrawTitle(float w, float h, float ui)
        {
            Fade(new Rect(0f, 0f, w, h), new Color(0.08f, 0.035f, 0.012f, 0.32f));
            float margin = Mathf.Max(20f, 34f * ui);
            float panelWidth = Mathf.Min(w - margin * 2f, 720f * ui);
            float panelHeight = Mathf.Min(h - margin * 2f, 590f * ui);
            Rect panel = new Rect(margin, (h - panelHeight) * 0.5f, panelWidth, panelHeight);
            Panel(panel, new Color(0.055f, 0.042f, 0.032f, 0.95f), new Color(0.83f, 0.14f, 0.055f));

            Label(new Rect(panel.x + 30f * ui, panel.y + 22f * ui, panel.width - 60f * ui, 25f * ui), "ORIGINAL 3D ARCADE COMBAT RACER  ·  BUILD 01", smallStyle);
            Label(new Rect(panel.x + 30f * ui, panel.y + 58f * ui, panel.width - 60f * ui, 82f * ui), "REDLINE", titleStyle);
            Label(new Rect(panel.x + 30f * ui, panel.y + 127f * ui, panel.width - 60f * ui, 52f * ui), "RENEGADES", titleStyle);
            Label(new Rect(panel.x + 30f * ui, panel.y + 186f * ui, panel.width - 60f * ui, 30f * ui), "MOUNTAIN COMBAT TRIAL", headerStyle);

            Rect briefing = new Rect(panel.x + 38f * ui, panel.y + 239f * ui, panel.width - 76f * ui, 78f * ui);
            Fade(briefing, new Color(0.11f, 0.075f, 0.045f, 0.92f));
            Label(new Rect(briefing.x + 14f * ui, briefing.y + 9f * ui, briefing.width - 28f * ui, 25f * ui), "OBJECTIVE", smallStyle);
            Label(new Rect(briefing.x + 14f * ui, briefing.y + 34f * ui, briefing.width - 28f * ui, 32f * ui), "OUTRIDE FIVE RIVALS. FIGHT FOR SPACE. REACH THE SUMMIT.", bodyStyle);

            Label(new Rect(panel.x + 35f * ui, panel.y + 345f * ui, panel.width - 70f * ui, 28f * ui), "W / S    THROTTLE & BRAKE        A / D    STEER", bodyStyle);
            Label(new Rect(panel.x + 35f * ui, panel.y + 382f * ui, panel.width - 70f * ui, 28f * ui), "Q / E    STRIKE LEFT & RIGHT        SPACE    BOOST", bodyStyle);
            Label(new Rect(panel.x + 35f * ui, panel.y + 419f * ui, panel.width - 70f * ui, 28f * ui), "R    RECOVER TO ROAD        ESC    PAUSE", bodyStyle);

            Rect action = new Rect(panel.x + 64f * ui, panel.y + 485f * ui, panel.width - 128f * ui, 58f * ui);
            Panel(action, new Color(0.19f, 0.075f, 0.028f, 0.96f), new Color(0.95f, 0.52f, 0.12f));
            Label(action, "PRESS ENTER TO START", headerStyle);
        }

        void DrawHud(float w, float h, float ui)
        {
            float margin = Mathf.Max(12f, 18f * ui);
            float progress = RoadSpline.I != null ? Player.Progress / RoadSpline.I.Length : 0f;
            Rect progressBack = new Rect(w * 0.22f, margin, w * 0.56f, 8f * ui);
            Bar(progressBack, progress, new Color(0.92f, 0.31f, 0.07f));

            Rect place = new Rect(margin, margin, 176f * ui, 102f * ui);
            Panel(place, new Color(0.045f, 0.04f, 0.034f, 0.93f), new Color(0.9f, 0.24f, 0.06f));
            Label(new Rect(place.x + 14f * ui, place.y + 8f * ui, place.width - 28f * ui, 22f * ui), "RACE POSITION", smallStyle);
            Label(new Rect(place.x + 10f * ui, place.y + 29f * ui, place.width - 20f * ui, 59f * ui), PlayerPosition + " / " + racers.Count, numberStyle);

            Rect speedPanel = new Rect(w - margin - 210f * ui, margin, 210f * ui, 102f * ui);
            Panel(speedPanel, new Color(0.045f, 0.04f, 0.034f, 0.93f), new Color(0.94f, 0.55f, 0.13f));
            Label(new Rect(speedPanel.x + 12f * ui, speedPanel.y + 6f * ui, speedPanel.width - 24f * ui, 57f * ui), Mathf.RoundToInt(Player.SpeedKph).ToString(), numberStyle);
            Label(new Rect(speedPanel.x + 12f * ui, speedPanel.y + 67f * ui, speedPanel.width - 24f * ui, 22f * ui), "KM/H     SCORE " + Score.ToString("N0"), smallStyle);

            Rect health = new Rect(margin, h - margin - 112f * ui, 310f * ui, 112f * ui);
            Panel(health, new Color(0.045f, 0.04f, 0.034f, 0.94f), new Color(0.9f, 0.24f, 0.06f));
            Label(new Rect(health.x + 15f * ui, health.y + 8f * ui, 100f * ui, 22f * ui), "RIDER", smallStyle);
            Bar(new Rect(health.x + 105f * ui, health.y + 13f * ui, health.width - 122f * ui, 12f * ui), Player.Health / 100f, new Color(0.85f, 0.18f, 0.08f));
            Label(new Rect(health.x + 15f * ui, health.y + 43f * ui, 100f * ui, 22f * ui), "BIKE", smallStyle);
            Bar(new Rect(health.x + 105f * ui, health.y + 48f * ui, health.width - 122f * ui, 12f * ui), Player.Integrity / 100f, new Color(0.92f, 0.57f, 0.13f));
            Label(new Rect(health.x + 15f * ui, health.y + 78f * ui, health.width - 30f * ui, 22f * ui), "R  RECOVER TO THE LAST SAFE LINE", smallStyle);

            Rect boost = new Rect(w - margin - 310f * ui, h - margin - 86f * ui, 310f * ui, 86f * ui);
            Panel(boost, new Color(0.045f, 0.04f, 0.034f, 0.94f), new Color(0.94f, 0.55f, 0.13f));
            Label(new Rect(boost.x + 15f * ui, boost.y + 9f * ui, boost.width - 30f * ui, 22f * ui), "OVERTAKE BOOST", smallStyle);
            Bar(new Rect(boost.x + 15f * ui, boost.y + 39f * ui, boost.width - 30f * ui, 14f * ui), Player.Boost, new Color(0.96f, 0.45f, 0.08f));
            Label(new Rect(boost.x + 15f * ui, boost.y + 59f * ui, boost.width - 30f * ui, 20f * ui), "SPACE TO BURN     Q / E TO STRIKE", smallStyle);

            float remaining = RoadSpline.I != null ? Mathf.Max(0f, RoadSpline.I.Length - Player.Progress) : 0f;
            Rect route = new Rect(w * 0.5f - 160f * ui, margin + 18f * ui, 320f * ui, 56f * ui);
            Panel(route, new Color(0.045f, 0.04f, 0.034f, 0.93f), new Color(0.9f, 0.24f, 0.06f));
            Label(route, Mathf.RoundToInt(remaining) + " M TO SUMMIT     ·     " + FormatTime(RaceTime), headerStyle);

            if (Time.unscaledTime < statusUntil && !string.IsNullOrEmpty(StatusMessage))
            {
                Rect status = new Rect(w * 0.5f - 235f * ui, margin + 84f * ui, 470f * ui, 44f * ui);
                Panel(status, new Color(0.12f, 0.055f, 0.025f, 0.94f), new Color(0.94f, 0.55f, 0.13f));
                Label(status, StatusMessage, headerStyle);
            }
        }

        void DrawPause(float w, float h, float ui)
        {
            Fade(new Rect(0f, 0f, w, h), new Color(0.025f, 0.018f, 0.012f, 0.78f));
            Rect panel = new Rect(w * 0.5f - 290f * ui, h * 0.5f - 122f * ui, 580f * ui, 244f * ui);
            Panel(panel, new Color(0.055f, 0.042f, 0.032f, 0.98f), new Color(0.94f, 0.55f, 0.13f));
            Label(new Rect(panel.x, panel.y + 36f * ui, panel.width, 72f * ui), "RACE PAUSED", titleStyle);
            Label(new Rect(panel.x, panel.y + 131f * ui, panel.width, 30f * ui), "PRESS ESC TO RETURN TO THE RIDGE", bodyStyle);
        }

        void DrawResult(float w, float h, float ui)
        {
            Fade(new Rect(0f, 0f, w, h), new Color(0.025f, 0.018f, 0.012f, 0.82f));
            Rect panel = new Rect(w * 0.5f - 365f * ui, h * 0.5f - 205f * ui, 730f * ui, 410f * ui);
            Color accent = Phase == RacePhase.Finished ? new Color(0.95f, 0.55f, 0.13f) : new Color(0.82f, 0.12f, 0.05f);
            Panel(panel, new Color(0.055f, 0.042f, 0.032f, 0.98f), accent);
            Label(new Rect(panel.x, panel.y + 35f * ui, panel.width, 75f * ui), Phase == RacePhase.Finished ? "SUMMIT REACHED" : "RACE OVER", titleStyle);
            string resultLine = Phase == RacePhase.Finished ? Ordinal(PlayerPosition) + " PLACE  ·  " + FormatTime(RaceTime) : defeatReason;
            Label(new Rect(panel.x + 30f * ui, panel.y + 130f * ui, panel.width - 60f * ui, 42f * ui), resultLine, headerStyle);
            Label(new Rect(panel.x + 30f * ui, panel.y + 194f * ui, panel.width - 60f * ui, 35f * ui), "SCORE  " + Score.ToString("N0"), numberStyle);
            Label(new Rect(panel.x + 30f * ui, panel.y + 253f * ui, panel.width - 60f * ui, 31f * ui),
                "RIDER " + Mathf.RoundToInt(Player.Health) + "%     ·     BIKE " + Mathf.RoundToInt(Player.Integrity) + "%", bodyStyle);
            Label(new Rect(panel.x + 30f * ui, panel.y + 325f * ui, panel.width - 60f * ui, 32f * ui), "PRESS R TO RACE AGAIN", headerStyle);
        }

        void EnsureStyles(float ui)
        {
            if (titleStyle != null && Mathf.Abs(styleScale - ui) < 0.01f) return;
            styleScale = ui;
            if (displayFont == null) displayFont = Resources.Load<Font>("Fonts/Oxanium");
            titleStyle = Style(TextAnchor.MiddleCenter, Mathf.Max(38, Mathf.RoundToInt(58f * ui)), FontStyle.Bold, new Color(0.98f, 0.55f, 0.14f), displayFont);
            headerStyle = Style(TextAnchor.MiddleCenter, Mathf.Max(16, Mathf.RoundToInt(21f * ui)), FontStyle.Bold, new Color(0.96f, 0.86f, 0.68f), displayFont);
            bodyStyle = Style(TextAnchor.MiddleCenter, Mathf.Max(13, Mathf.RoundToInt(16f * ui)), FontStyle.Normal, new Color(0.9f, 0.86f, 0.78f), null);
            smallStyle = Style(TextAnchor.MiddleLeft, Mathf.Max(12, Mathf.RoundToInt(13f * ui)), FontStyle.Bold, new Color(0.87f, 0.81f, 0.7f), null);
            numberStyle = Style(TextAnchor.MiddleCenter, Mathf.Max(28, Mathf.RoundToInt(43f * ui)), FontStyle.Bold, Color.white, displayFont);
        }

        static GUIStyle Style(TextAnchor anchor, int size, FontStyle fontStyle, Color color, Font font)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = anchor, fontSize = size, fontStyle = fontStyle, clipping = TextClipping.Clip };
            style.normal.textColor = color;
            if (font != null) style.font = font;
            return style;
        }

        void Panel(Rect rect, Color fill, Color accent)
        {
            Fade(rect, fill);
            Border(rect, new Color(0.56f, 0.46f, 0.34f, 0.7f), 1f);
            Fade(new Rect(rect.x, rect.y, Mathf.Max(3f, rect.width * 0.007f), rect.height), accent);
            Fade(new Rect(rect.x, rect.y, rect.width, 2f), accent);
        }

        void Bar(Rect rect, float value, Color fill)
        {
            Fade(rect, new Color(0.14f, 0.12f, 0.1f, 0.98f));
            Rect amount = rect;
            amount.width *= Mathf.Clamp01(value);
            Fade(amount, fill);
            Border(rect, new Color(0.55f, 0.48f, 0.38f, 0.62f), 1f);
        }

        void Border(Rect rect, Color color, float width)
        {
            Fade(new Rect(rect.x, rect.y, rect.width, width), color);
            Fade(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            Fade(new Rect(rect.x, rect.y, width, rect.height), color);
            Fade(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        void Label(Rect rect, string text, GUIStyle style) => GUI.Label(rect, text, style);

        void Fade(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = old;
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        static string Ordinal(int position)
        {
            if (position == 1) return "1ST";
            if (position == 2) return "2ND";
            if (position == 3) return "3RD";
            return position + "TH";
        }
    }
}
