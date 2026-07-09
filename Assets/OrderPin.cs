using UnityEngine;
using System.Collections.Generic;

public class OrderPin : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    const int NUM_PINS = 10;
    const int PLAYER_COUNT = 2;
    const int FRAME_COUNT = 3;
    [SerializeField] private Pin pinObject; // Reference to the pin prefab

    [Header("Scoreboard")]
    [SerializeField] private bool showScoreboard = true;
    [SerializeField] private Vector3 scoreboardPosition = new Vector3(-24f, 7f, 4.2f);
    [SerializeField] private Vector2 scoreboardSize = new Vector2(7f, 3f);
    [SerializeField] private float scoreboardDepth = 0.45f;
    [SerializeField] private Color tvBodyColor = new Color(0.015f, 0.015f, 0.018f, 1f);
    [SerializeField] private Color tvFrameColor = new Color(0.12f, 0.12f, 0.14f, 1f);
    [SerializeField] private Color scoreboardBackgroundColor = new Color(0.03f, 0.09f, 0.12f, 1f);
    [SerializeField] private Color scoreboardTextColor = Color.white;

    [Header("Pin Layout")]
    [SerializeField] private float pinStartX = -24f;

    int score = 0; // ピンが倒れた数とする

    readonly List<Pin> pins = new List<Pin>(NUM_PINS);
    private readonly PlayerScore[] playerScores = new PlayerScore[PLAYER_COUNT];
    private Transform scoreboardTransform;
    private TextMesh titleText;
    private TextMesh turnText;
    private TextMesh[] playerNameTexts;
    private TextMesh[,] frameTexts;
    private TextMesh[] totalTexts;
    private TextMesh pinsText;
    private Renderer[] playerRowRenderers;
    private Transform normalScoreboardContentRoot;
    private Transform winnerScoreboardRoot;
    private Transform winnerCrownRoot;
    private TextMesh winnerPlayerText;
    private TextMesh winnerLabelText;
    private TextMesh winnerScoreText;
    private TextMesh winnerHintText;
    private Renderer winnerBackdropRenderer;
    private Renderer winnerGlowRenderer;
    private Vector3 winnerCrownBaseLocalPosition;
    private readonly Dictionary<TextMesh, Vector3> fittedTextLimits = new Dictionary<TextMesh, Vector3>();
    private Camera mainCamera;
    private string displayedScoreboardState = "";
    private int currentPlayerIndex = 0;
    private bool gameFinished = false;
    private int lastShotPins = 0;
    private bool winnerPresentationActive = false;

    public int Score => score;
    public bool HasPinObject => pinObject != null;
    public int CurrentPlayerIndex => currentPlayerIndex;
    public int CurrentMarkerId => currentPlayerIndex + 1;
    public bool IsGameFinished => gameFinished;
    public string CurrentPlayerLabel => currentPlayerIndex == 0 ? "FIRST" : "SECOND";

    private class PlayerScore
    {
        public readonly FrameScore[] frames = new FrameScore[FRAME_COUNT];
        public int currentFrameIndex = 0;

        public PlayerScore()
        {
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = new FrameScore();
            }
        }
    }

    private class FrameScore
    {
        public readonly int[] rolls = new int[3];
        public int rollCount = 0;

        public bool IsStrike => rollCount > 0 && rolls[0] == NUM_PINS;
        public bool IsSpare => rollCount > 1 && rolls[0] < NUM_PINS && rolls[0] + rolls[1] == NUM_PINS;
    }


    void Start()
    {
        InitializeScores();

        if (pinObject == null)
        {
            if (FindConfiguredSiblingOrderPin() != null)
            {
                enabled = false;
                return;
            }

            Debug.LogError("OrderPin: pinObject is not assigned.", this);
            enabled = false;
            return;
        }

        SpawnPins();

        if (showScoreboard)
        {
            CreateScoreboard();
            UpdateScoreboardText();
        }
    }

    // Update is called once per frame
    void Update()
    {
        RecalculatePinScore();

        if (showScoreboard)
        {
            UpdateScoreboardText();
            AnimateWinnerPresentation();
        }
    }

    private void LateUpdate()
    {
        if (scoreboardTransform == null)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera != null)
        {
            scoreboardTransform.rotation = mainCamera.transform.rotation;
        }
    }

    private void CreateScoreboard()
    {
        mainCamera = Camera.main;

        GameObject scoreboardObject = new GameObject("Lane Scoreboard");
        scoreboardObject.transform.SetParent(transform, false);
        scoreboardObject.transform.position = scoreboardPosition;
        scoreboardTransform = scoreboardObject.transform;

        float bezel = 0.32f;
        float screenWidth = scoreboardSize.x - bezel * 2f;
        float screenHeight = scoreboardSize.y - bezel * 2f;
        float frontZ = -scoreboardDepth * 0.5f - 0.01f;

        CreateCubePart(
            "TV Body",
            scoreboardObject.transform,
            Vector3.zero,
            new Vector3(scoreboardSize.x + 0.45f, scoreboardSize.y + 0.45f, scoreboardDepth),
            tvBodyColor);

        CreateCubePart(
            "Screen",
            scoreboardObject.transform,
            new Vector3(0f, 0f, frontZ - 0.03f),
            new Vector3(screenWidth, screenHeight, 0.04f),
            scoreboardBackgroundColor);

        CreateCubePart(
            "Header Glow",
            scoreboardObject.transform,
            new Vector3(0f, screenHeight * 0.34f, frontZ - 0.07f),
            new Vector3(screenWidth * 0.92f, screenHeight * 0.2f, 0.035f),
            new Color(0.02f, 0.22f, 0.32f, 1f));

        CreateCubePart(
            "Top Frame",
            scoreboardObject.transform,
            new Vector3(0f, screenHeight * 0.5f + bezel * 0.5f, frontZ),
            new Vector3(scoreboardSize.x, bezel, 0.08f),
            tvFrameColor);

        CreateCubePart(
            "Bottom Frame",
            scoreboardObject.transform,
            new Vector3(0f, -screenHeight * 0.5f - bezel * 0.5f, frontZ),
            new Vector3(scoreboardSize.x, bezel, 0.08f),
            tvFrameColor);

        CreateCubePart(
            "Left Frame",
            scoreboardObject.transform,
            new Vector3(-screenWidth * 0.5f - bezel * 0.5f, 0f, frontZ),
            new Vector3(bezel, screenHeight, 0.08f),
            tvFrameColor);

        CreateCubePart(
            "Right Frame",
            scoreboardObject.transform,
            new Vector3(screenWidth * 0.5f + bezel * 0.5f, 0f, frontZ),
            new Vector3(bezel, screenHeight, 0.08f),
            tvFrameColor);

        CreateCubePart(
            "Status Light",
            scoreboardObject.transform,
            new Vector3(scoreboardSize.x * 0.5f - 0.45f, -scoreboardSize.y * 0.5f + 0.17f, frontZ - 0.08f),
            new Vector3(0.16f, 0.16f, 0.08f),
            new Color(0.1f, 0.9f, 0.45f, 1f));

        CreateScoreboardContent(scoreboardObject.transform, screenWidth, screenHeight, frontZ);
    }

    private Renderer CreateCubePart(string objectName, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject partObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        partObject.name = objectName;
        partObject.transform.SetParent(parent, false);
        partObject.transform.localPosition = localPosition;
        partObject.transform.localScale = localScale;

        Collider collider = partObject.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = partObject.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Standard"));
        renderer.material.color = color;
        return renderer;
    }

    private void CreateScoreboardContent(Transform parent, float screenWidth, float screenHeight, float frontZ)
    {
        playerNameTexts = new TextMesh[PLAYER_COUNT];
        frameTexts = new TextMesh[PLAYER_COUNT, FRAME_COUNT];
        totalTexts = new TextMesh[PLAYER_COUNT];
        playerRowRenderers = new Renderer[PLAYER_COUNT];
        normalScoreboardContentRoot = new GameObject("Scoreboard Normal Content").transform;
        normalScoreboardContentRoot.SetParent(parent, false);

        float textZ = frontZ - 0.16f;
        float contentWidth = screenWidth * 0.86f;
        Transform contentParent = normalScoreboardContentRoot;
        float leftX = -contentWidth * 0.5f;
        float topY = screenHeight * 0.37f;
        float headerY = screenHeight * 0.14f;
        float rowOneY = -screenHeight * 0.03f;
        float rowGap = screenHeight * 0.23f;
        float playerX = leftX + contentWidth * 0.09f;
        float frameStartX = leftX + contentWidth * 0.31f;
        float frameGap = contentWidth * 0.17f;
        float frameWidth = contentWidth * 0.12f;
        float totalX = leftX + contentWidth * 0.82f;
        float totalWidth = contentWidth * 0.16f;

        CreateCubePart(
            "Board Tint",
            contentParent,
            new Vector3(0f, 0f, frontZ - 0.075f),
            new Vector3(screenWidth * 0.92f, screenHeight * 0.86f, 0.03f),
            new Color(0.004f, 0.015f, 0.026f, 1f));

        CreateCubePart(
            "Top Neon Rule",
            contentParent,
            new Vector3(0f, screenHeight * 0.44f, frontZ - 0.11f),
            new Vector3(screenWidth * 0.86f, screenHeight * 0.018f, 0.035f),
            new Color(0.14f, 0.64f, 1f, 1f));

        titleText = CreateFittedText(
            "Title",
            contentParent,
            new Vector3(leftX + contentWidth * 0.18f, topY, textZ),
            "BOWLING DUEL",
            new Color(1f, 0.86f, 0.22f, 1f),
            contentWidth * 0.36f,
            screenHeight * 0.1f,
            0.42f);

        turnText = CreateFittedText(
            "Turn",
            contentParent,
            new Vector3(leftX + contentWidth * 0.74f, topY, textZ),
            "",
            scoreboardTextColor,
            contentWidth * 0.48f,
            screenHeight * 0.1f,
            0.32f);

        CreateCubePart(
            "Header Strip",
            contentParent,
            new Vector3(0f, headerY, frontZ - 0.1f),
            new Vector3(contentWidth, screenHeight * 0.075f, 0.035f),
            new Color(0.035f, 0.1f, 0.145f, 1f));

        CreateFittedText("Player Header", contentParent, new Vector3(playerX, headerY, textZ), "PLAYER", new Color(0.46f, 0.84f, 1f, 1f), contentWidth * 0.16f, screenHeight * 0.06f, 0.18f);

        for (int frameIndex = 0; frameIndex < FRAME_COUNT; frameIndex++)
        {
            float frameX = frameStartX + frameIndex * frameGap;
            CreateFittedText($"F{frameIndex + 1} Header", contentParent, new Vector3(frameX, headerY, textZ), $"F{frameIndex + 1}", new Color(0.46f, 0.84f, 1f, 1f), frameWidth, screenHeight * 0.06f, 0.18f);
        }

        CreateFittedText("Total Header", contentParent, new Vector3(totalX, headerY, textZ), "TOTAL", new Color(1f, 0.86f, 0.22f, 1f), totalWidth, screenHeight * 0.06f, 0.18f);

        for (int playerIndex = 0; playerIndex < PLAYER_COUNT; playerIndex++)
        {
            float rowY = rowOneY - playerIndex * rowGap;
            Color rowColor = playerIndex == 0
                ? new Color(0.025f, 0.12f, 0.19f, 1f)
                : new Color(0.16f, 0.05f, 0.13f, 1f);

            playerRowRenderers[playerIndex] = CreateCubePart(
                $"P{playerIndex + 1} Row",
                contentParent,
                new Vector3(0f, rowY, frontZ - 0.095f),
                new Vector3(contentWidth, screenHeight * 0.15f, 0.035f),
                rowColor);

            CreateCubePart(
                $"P{playerIndex + 1} Accent",
                contentParent,
                new Vector3(leftX + contentWidth * 0.015f, rowY, frontZ - 0.13f),
                new Vector3(contentWidth * 0.015f, screenHeight * 0.15f, 0.04f),
                playerIndex == 0 ? new Color(0.2f, 0.75f, 1f, 1f) : new Color(1f, 0.28f, 0.68f, 1f));

            playerNameTexts[playerIndex] = CreateFittedText(
                $"P{playerIndex + 1} Name",
                contentParent,
                new Vector3(playerX, rowY, textZ),
                $"P{playerIndex + 1}",
                Color.white,
                contentWidth * 0.16f,
                screenHeight * 0.11f,
                0.34f);

            for (int frameIndex = 0; frameIndex < FRAME_COUNT; frameIndex++)
            {
                float frameX = frameStartX + frameIndex * frameGap;
                CreateCubePart(
                    $"P{playerIndex + 1} F{frameIndex + 1} Cell",
                    contentParent,
                    new Vector3(frameX, rowY, frontZ - 0.13f),
                    new Vector3(frameWidth, screenHeight * 0.105f, 0.04f),
                    new Color(0.004f, 0.024f, 0.04f, 1f));

                frameTexts[playerIndex, frameIndex] = CreateFittedText(
                    $"P{playerIndex + 1} F{frameIndex + 1}",
                    contentParent,
                    new Vector3(frameX, rowY, textZ),
                    "-",
                    Color.white,
                    frameWidth * 0.86f,
                    screenHeight * 0.09f,
                    0.31f);
            }

            CreateCubePart(
                $"P{playerIndex + 1} Total Cell",
                contentParent,
                new Vector3(totalX, rowY, frontZ - 0.13f),
                new Vector3(totalWidth, screenHeight * 0.105f, 0.04f),
                new Color(0.12f, 0.088f, 0.02f, 1f));

            totalTexts[playerIndex] = CreateFittedText(
                $"P{playerIndex + 1} Total",
                contentParent,
                new Vector3(totalX, rowY, textZ),
                "000",
                new Color(1f, 0.86f, 0.22f, 1f),
                totalWidth * 0.88f,
                screenHeight * 0.09f,
                0.34f);
        }

        CreateCubePart(
            "Pins Status Backdrop",
            contentParent,
            new Vector3(0f, -screenHeight * 0.39f, frontZ - 0.1f),
            new Vector3(contentWidth, screenHeight * 0.09f, 0.035f),
            new Color(0.01f, 0.06f, 0.048f, 1f));

        pinsText = CreateFittedText(
            "Pins",
            contentParent,
            new Vector3(0f, -screenHeight * 0.39f, textZ),
            "",
            new Color(0.65f, 1f, 0.84f, 1f),
            contentWidth * 0.86f,
            screenHeight * 0.07f,
            0.24f);

        CreateWinnerPresentationContent(parent, screenWidth, screenHeight, frontZ);
        SetWinnerPresentationActive(false);
    }

    private void CreateWinnerPresentationContent(Transform parent, float screenWidth, float screenHeight, float frontZ)
    {
        winnerScoreboardRoot = new GameObject("Scoreboard Winner Presentation").transform;
        winnerScoreboardRoot.SetParent(parent, false);

        float textZ = frontZ - 0.18f;
        float contentWidth = screenWidth * 0.86f;

        winnerBackdropRenderer = CreateCubePart(
            "Winner Backdrop",
            winnerScoreboardRoot,
            new Vector3(0f, 0f, frontZ - 0.13f),
            new Vector3(screenWidth * 0.92f, screenHeight * 0.86f, 0.045f),
            new Color(0.012f, 0.02f, 0.055f, 1f));

        winnerGlowRenderer = CreateCubePart(
            "Winner Glow",
            winnerScoreboardRoot,
            new Vector3(0f, 0.03f, frontZ - 0.17f),
            new Vector3(screenWidth * 0.78f, screenHeight * 0.58f, 0.04f),
            new Color(0.14f, 0.42f, 0.72f, 1f));

        CreateCubePart(
            "Winner Gold Rule Top",
            winnerScoreboardRoot,
            new Vector3(0f, screenHeight * 0.36f, frontZ - 0.2f),
            new Vector3(contentWidth, screenHeight * 0.026f, 0.04f),
            new Color(1f, 0.78f, 0.18f, 1f));

        CreateCubePart(
            "Winner Gold Rule Bottom",
            winnerScoreboardRoot,
            new Vector3(0f, -screenHeight * 0.36f, frontZ - 0.2f),
            new Vector3(contentWidth, screenHeight * 0.026f, 0.04f),
            new Color(1f, 0.78f, 0.18f, 1f));

        winnerCrownRoot = new GameObject("Winner Crown").transform;
        winnerCrownRoot.SetParent(winnerScoreboardRoot, false);
        winnerCrownBaseLocalPosition = new Vector3(0f, -screenHeight * 0.02f, textZ - 0.02f);
        winnerCrownRoot.localPosition = winnerCrownBaseLocalPosition;
        CreateCrown(winnerCrownRoot, screenWidth, screenHeight, frontZ);

        winnerPlayerText = CreateFittedText(
            "Winner Player",
            winnerScoreboardRoot,
            new Vector3(0f, -screenHeight * 0.2f, textZ),
            "",
            Color.white,
            contentWidth * 0.72f,
            screenHeight * 0.24f,
            0.82f);

        winnerLabelText = CreateFittedText(
            "Winner Label",
            winnerScoreboardRoot,
            new Vector3(0f, screenHeight * 0.22f, textZ),
            "Winner",
            new Color(1f, 0.82f, 0.24f, 1f),
            contentWidth * 0.7f,
            screenHeight * 0.14f,
            0.44f);

        winnerScoreText = CreateFittedText(
            "Winner Score",
            winnerScoreboardRoot,
            new Vector3(0f, -screenHeight * 0.36f, textZ),
            "",
            new Color(0.7f, 0.94f, 1f, 1f),
            contentWidth * 0.72f,
            screenHeight * 0.08f,
            0.24f);

        winnerHintText = CreateFittedText(
            "Winner Hint",
            winnerScoreboardRoot,
            new Vector3(0f, -screenHeight * 0.44f, textZ),
            "PRESS R",
            new Color(0.65f, 1f, 0.84f, 1f),
            contentWidth * 0.5f,
            screenHeight * 0.06f,
            0.18f);
    }

    private void CreateCrown(Transform parent, float screenWidth, float screenHeight, float frontZ)
    {
        Color gold = new Color(1f, 0.76f, 0.16f, 1f);
        Color brightGold = new Color(1f, 0.92f, 0.3f, 1f);
        Color jewel = new Color(0.25f, 0.8f, 1f, 1f);
        float crownWidth = screenWidth * 0.22f;
        float crownHeight = screenHeight * 0.14f;

        CreateCubePart("Crown Base", parent, Vector3.zero, new Vector3(crownWidth, crownHeight * 0.22f, 0.045f), gold);
        CreateCubePart("Crown Left Peak", parent, new Vector3(-crownWidth * 0.32f, crownHeight * 0.25f, 0f), new Vector3(crownWidth * 0.16f, crownHeight * 0.62f, 0.045f), gold).transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        CreateCubePart("Crown Center Peak", parent, new Vector3(0f, crownHeight * 0.33f, 0f), new Vector3(crownWidth * 0.16f, crownHeight * 0.78f, 0.045f), brightGold);
        CreateCubePart("Crown Right Peak", parent, new Vector3(crownWidth * 0.32f, crownHeight * 0.25f, 0f), new Vector3(crownWidth * 0.16f, crownHeight * 0.62f, 0.045f), gold).transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        CreateCubePart("Crown Center Jewel", parent, new Vector3(0f, crownHeight * 0.02f, -0.018f), new Vector3(crownWidth * 0.08f, crownHeight * 0.13f, 0.035f), jewel);
    }

    private TextMesh CreateFittedText(string objectName, Transform parent, Vector3 localPosition, string text, Color color, float maxWidth, float maxHeight, float maxCharacterSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(TextMesh));
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = localPosition;

        TextMesh textMesh = textObject.GetComponent<TextMesh>();
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = color;
        textMesh.fontSize = 96;
        textMesh.fontStyle = FontStyle.Bold;
        fittedTextLimits[textMesh] = new Vector3(maxWidth, maxHeight, maxCharacterSize);
        SetTextToFit(textMesh, text, maxWidth, maxHeight, maxCharacterSize);
        return textMesh;
    }

    private void UpdateFittedText(TextMesh textMesh, string text)
    {
        if (textMesh == null)
        {
            return;
        }

        if (fittedTextLimits.TryGetValue(textMesh, out Vector3 limits))
        {
            SetTextToFit(textMesh, text, limits.x, limits.y, limits.z);
            return;
        }

        textMesh.text = text;
    }

    private void SetTextToFit(TextMesh textMesh, string text, float maxWidth, float maxHeight, float maxCharacterSize)
    {
        if (textMesh == null)
        {
            return;
        }

        textMesh.text = text;
        textMesh.characterSize = maxCharacterSize;

        Renderer renderer = textMesh.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        for (int i = 0; i < 18; i++)
        {
            Bounds bounds = renderer.localBounds;
            bool fitsWidth = bounds.size.x <= maxWidth || maxWidth <= 0f;
            bool fitsHeight = bounds.size.y <= maxHeight || maxHeight <= 0f;

            if (fitsWidth && fitsHeight)
            {
                break;
            }

            float widthScale = bounds.size.x <= 0f ? 1f : maxWidth / bounds.size.x;
            float heightScale = bounds.size.y <= 0f ? 1f : maxHeight / bounds.size.y;
            float scale = Mathf.Clamp(Mathf.Min(widthScale, heightScale) * 0.92f, 0.55f, 0.98f);
            textMesh.characterSize *= scale;
        }
    }

    private void SetWinnerPresentationActive(bool active)
    {
        bool rootAlreadyMatches =
            winnerScoreboardRoot != null &&
            winnerScoreboardRoot.gameObject.activeSelf == active;

        if (winnerPresentationActive == active && rootAlreadyMatches)
        {
            return;
        }

        winnerPresentationActive = active;

        if (normalScoreboardContentRoot != null)
        {
            normalScoreboardContentRoot.gameObject.SetActive(!active);
        }

        if (winnerScoreboardRoot != null)
        {
            winnerScoreboardRoot.gameObject.SetActive(active);
            winnerScoreboardRoot.localScale = active ? Vector3.one * 0.92f : Vector3.one;
        }
    }

    private void UpdateWinnerPresentationText()
    {
        int firstPlayerScore = CalculatePlayerScore(playerScores[0]);
        int secondPlayerScore = CalculatePlayerScore(playerScores[1]);
        int winnerPlayerIndex = GetWinnerPlayerIndex();

        if (winnerPlayerIndex < 0)
        {
            UpdateFittedText(winnerPlayerText, "DRAW");
            UpdateFittedText(winnerLabelText, "GAME SET");
            UpdateFittedText(winnerScoreText, $"P1 {firstPlayerScore:000}  -  P2 {secondPlayerScore:000}");
            winnerPlayerText.color = Color.white;

            if (winnerCrownRoot != null)
            {
                winnerCrownRoot.gameObject.SetActive(false);
            }

            return;
        }

        UpdateFittedText(winnerPlayerText, $"P{winnerPlayerIndex + 1}");
        UpdateFittedText(winnerLabelText, "Winner");
        UpdateFittedText(winnerScoreText, $"P1 {firstPlayerScore:000}  -  P2 {secondPlayerScore:000}");
        winnerPlayerText.color = winnerPlayerIndex == 0
            ? new Color(0.66f, 0.9f, 1f, 1f)
            : new Color(1f, 0.64f, 0.9f, 1f);

        if (winnerCrownRoot != null)
        {
            winnerCrownRoot.gameObject.SetActive(true);
        }
    }

    private void AnimateWinnerPresentation()
    {
        if (!winnerPresentationActive || winnerScoreboardRoot == null)
        {
            return;
        }

        float t = Time.time;
        float pulse = 1f + Mathf.Sin(t * 4.2f) * 0.035f;
        winnerScoreboardRoot.localScale = Vector3.one * pulse;

        if (winnerCrownRoot != null && winnerCrownRoot.gameObject.activeSelf)
        {
            float bob = Mathf.Sin(t * 5.4f) * 0.035f;
            winnerCrownRoot.localPosition = winnerCrownBaseLocalPosition + new Vector3(0f, bob, 0f);
            winnerCrownRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3.1f) * 4f);
        }

        if (winnerGlowRenderer != null)
        {
            float glow = 0.55f + Mathf.Sin(t * 3.7f) * 0.18f;
            winnerGlowRenderer.material.color = Color.Lerp(
                new Color(0.08f, 0.22f, 0.42f, 1f),
                new Color(0.24f, 0.58f, 0.95f, 1f),
                glow);
        }

        if (winnerBackdropRenderer != null)
        {
            float shimmer = 0.5f + Mathf.Sin(t * 2.2f) * 0.08f;
            winnerBackdropRenderer.material.color = Color.Lerp(
                new Color(0.008f, 0.014f, 0.04f, 1f),
                new Color(0.025f, 0.028f, 0.075f, 1f),
                shimmer);
        }
    }

    private void UpdateScoreboardText()
    {
        if (turnText == null || pinsText == null)
        {
            return;
        }

        SetWinnerPresentationActive(gameFinished);

        string state = BuildScoreboardState();

        if (state == displayedScoreboardState)
        {
            return;
        }

        displayedScoreboardState = state;
        bool isPlayerOneTurn = !gameFinished && currentPlayerIndex == 0;

        if (gameFinished)
        {
            UpdateWinnerPresentationText();
            return;
        }

        UpdateFittedText(titleText, $"{CurrentPlayerLabel} SCORE");
        string turnLabel = $"{CurrentPlayerLabel} TURN  M{CurrentMarkerId}  F{GetCurrentFrameIndex() + 1}-{GetCurrentThrowNumber()}";
        UpdateFittedText(turnText, turnLabel);
        turnText.color = isPlayerOneTurn
            ? new Color(0.66f, 0.9f, 1f, 1f)
            : new Color(1f, 0.64f, 0.9f, 1f);

        for (int playerIndex = 0; playerIndex < PLAYER_COUNT; playerIndex++)
        {
            PlayerScore player = playerScores[playerIndex];
            bool isCurrentPlayer = !gameFinished && playerIndex == currentPlayerIndex;

            if (playerRowRenderers[playerIndex] != null)
            {
                playerRowRenderers[playerIndex].material.color = isCurrentPlayer
                    ? (playerIndex == 0 ? new Color(0.04f, 0.23f, 0.38f, 1f) : new Color(0.32f, 0.08f, 0.23f, 1f))
                    : (playerIndex == 0 ? new Color(0.03f, 0.12f, 0.2f, 0.95f) : new Color(0.16f, 0.055f, 0.13f, 0.95f));
            }

            string playerLabel = playerIndex == 0 ? "FIRST M1" : "SECOND M2";
            UpdateFittedText(playerNameTexts[playerIndex], isCurrentPlayer ? $"{playerLabel}>" : playerLabel);
            playerNameTexts[playerIndex].color = isCurrentPlayer
                ? new Color(1f, 0.92f, 0.35f, 1f)
                : Color.white;

            for (int frameIndex = 0; frameIndex < FRAME_COUNT; frameIndex++)
            {
                UpdateFittedText(frameTexts[playerIndex, frameIndex], FormatFrame(player.frames[frameIndex], frameIndex));
            }

            UpdateFittedText(totalTexts[playerIndex], CalculatePlayerScore(player).ToString("000"));
        }

        UpdateFittedText(pinsText, $"HIT {lastShotPins}     DOWN {score}     LEFT {pins.Count}");
    }

    private string BuildScoreboardState()
    {
        string state = gameFinished
            ? "GAME SET"
            : $"TURN  P{currentPlayerIndex + 1}  F{GetCurrentFrameIndex() + 1}  THROW {GetCurrentThrowNumber()}";

        for (int playerIndex = 0; playerIndex < PLAYER_COUNT; playerIndex++)
        {
            PlayerScore player = playerScores[playerIndex];
            state += $"|P{playerIndex + 1}:{CalculatePlayerScore(player)}";
            for (int frameIndex = 0; frameIndex < FRAME_COUNT; frameIndex++)
            {
                state += $":{FormatFrame(player.frames[frameIndex], frameIndex)}";
            }
        }

        return $"{state}|pins:{score}|left:{pins.Count}|last:{lastShotPins}";
    }

    private string FormatFrame(FrameScore frame, int frameIndex)
    {
        if (frame.rollCount == 0)
        {
            return "-";
        }

        if (frameIndex < FRAME_COUNT - 1)
        {
            if (frame.IsStrike)
            {
                return " X ";
            }

            string first = FormatRoll(frame.rolls[0]);
            string second = frame.rollCount > 1 ? FormatSecondRoll(frame.rolls[0], frame.rolls[1]) : " ";
            return $"{first} {second}";
        }

        string text = "";
        for (int i = 0; i < 3; i++)
        {
            text += i < frame.rollCount ? FormatFinalFrameRoll(frame, i) : " ";
        }

        return text;
    }

    private string FormatRoll(int pins)
    {
        return pins == NUM_PINS ? "X" : pins.ToString();
    }

    private string FormatSecondRoll(int firstRoll, int secondRoll)
    {
        if (firstRoll + secondRoll == NUM_PINS)
        {
            return "/";
        }

        return FormatRoll(secondRoll);
    }

    private string FormatFinalFrameRoll(FrameScore frame, int rollIndex)
    {
        int pins = frame.rolls[rollIndex];

        if (pins == NUM_PINS)
        {
            return "X";
        }

        if (rollIndex == 1 && frame.rolls[0] + pins == NUM_PINS)
        {
            return "/";
        }

        if (rollIndex == 2 && frame.rolls[1] < NUM_PINS && frame.rolls[1] + pins == NUM_PINS)
        {
            return "/";
        }

        return pins.ToString();
    }

    private int CalculatePlayerScore(PlayerScore player)
    {
        List<int> allRolls = new List<int>();
        for (int frameIndex = 0; frameIndex < FRAME_COUNT; frameIndex++)
        {
            FrameScore frame = player.frames[frameIndex];
            for (int rollIndex = 0; rollIndex < frame.rollCount; rollIndex++)
            {
                allRolls.Add(frame.rolls[rollIndex]);
            }
        }

        int total = 0;
        int rollCursor = 0;

        for (int frameIndex = 0; frameIndex < FRAME_COUNT; frameIndex++)
        {
            FrameScore frame = player.frames[frameIndex];
            if (frame.rollCount == 0)
            {
                break;
            }

            if (frameIndex == FRAME_COUNT - 1)
            {
                for (int i = 0; i < frame.rollCount; i++)
                {
                    total += frame.rolls[i];
                }
                break;
            }

            if (frame.IsStrike)
            {
                total += NUM_PINS;
                total += GetRollOrZero(allRolls, rollCursor + 1);
                total += GetRollOrZero(allRolls, rollCursor + 2);
                rollCursor += 1;
                continue;
            }

            if (frame.rollCount == 1)
            {
                total += frame.rolls[0];
                break;
            }

            int framePins = frame.rolls[0] + frame.rolls[1];
            total += framePins;

            if (framePins == NUM_PINS)
            {
                total += GetRollOrZero(allRolls, rollCursor + 2);
            }

            rollCursor += 2;
        }

        return total;
    }

    private int GetRollOrZero(List<int> rolls, int index)
    {
        return index >= 0 && index < rolls.Count ? rolls[index] : 0;
    }

    private string GetWinnerDisplayLabel()
    {
        int winnerPlayerIndex = GetWinnerPlayerIndex();

        if (winnerPlayerIndex < 0)
        {
            return "DRAW GAME";
        }

        return winnerPlayerIndex == 0 ? "FIRST Winner" : "SECOND Winner";
    }

    private int GetWinnerPlayerIndex()
    {
        int firstPlayerScore = CalculatePlayerScore(playerScores[0]);
        int secondPlayerScore = CalculatePlayerScore(playerScores[1]);

        if (firstPlayerScore == secondPlayerScore)
        {
            return -1;
        }

        return firstPlayerScore > secondPlayerScore ? 0 : 1;
    }

    private int GetCurrentFrameIndex()
    {
        return playerScores[currentPlayerIndex].currentFrameIndex;
    }

    private int GetCurrentThrowNumber()
    {
        PlayerScore player = playerScores[currentPlayerIndex];
        int frameIndex = Mathf.Clamp(player.currentFrameIndex, 0, FRAME_COUNT - 1);
        return player.frames[frameIndex].rollCount + 1;
    }

    private void InitializeScores()
    {
        for (int i = 0; i < playerScores.Length; i++)
        {
            playerScores[i] = new PlayerScore();
        }

        currentPlayerIndex = 0;
        gameFinished = false;
        lastShotPins = 0;
    }

    private bool HasConfiguredSiblingOrderPin()
    {
        OrderPin[] orderPins = GetComponents<OrderPin>();

        foreach (OrderPin orderPin in orderPins)
        {
            if (orderPin != this && orderPin.pinObject != null)
            {
                return true;
            }
        }

        return false;
    }

    public void ResetPinsAndScore()
    {
        if (pinObject == null)
        {
            OrderPin configuredOrderPin = FindConfiguredSiblingOrderPin();

            if (configuredOrderPin != null)
            {
                configuredOrderPin.ResetPinsAndScore();
                return;
            }

            Debug.LogError("OrderPin: Cannot reset pins because pinObject is not assigned.", this);
            return;
        }

        ClearPins();
        InitializeScores();
        score = 0;
        SpawnPins();
        UpdateScoreboardText();
    }

    public void CompleteCurrentShotAndPrepareNextTurn()
    {
        if (pinObject == null)
        {
            OrderPin configuredOrderPin = FindConfiguredSiblingOrderPin();

            if (configuredOrderPin != null)
            {
                configuredOrderPin.CompleteCurrentShotAndPrepareNextTurn();
                return;
            }

            Debug.LogError("OrderPin: Cannot complete shot because pinObject is not assigned.", this);
            return;
        }

        if (gameFinished)
        {
            ResetGame();
            return;
        }

        RecalculatePinScore();

        PlayerScore player = playerScores[currentPlayerIndex];
        FrameScore frame = player.frames[player.currentFrameIndex];
        int pinsThisRoll = Mathf.Clamp(score, 0, NUM_PINS);
        lastShotPins = pinsThisRoll;
        frame.rolls[frame.rollCount] = pinsThisRoll;
        frame.rollCount++;

        bool shouldResetPins = IsCurrentFrameComplete(player, frame);

        if (shouldResetPins)
        {
            AdvanceTurn();
            ResetPinsOnly();
        }
        else if (ShouldResetPinsForBonusRoll(frame))
        {
            ResetPinsOnly();
        }
        else
        {
            RemoveFallenPins();
        }

        UpdateScoreboardText();
    }

    public void ResetGame()
    {
        InitializeScores();
        ResetPinsOnly();
        UpdateScoreboardText();
    }

    private bool IsCurrentFrameComplete(PlayerScore player, FrameScore frame)
    {
        bool finalFrame = player.currentFrameIndex == FRAME_COUNT - 1;

        if (!finalFrame)
        {
            return frame.IsStrike || frame.rollCount >= 2;
        }

        if (frame.rollCount < 2)
        {
            return false;
        }

        bool hasBonusRoll = frame.IsStrike || frame.IsSpare;
        return !hasBonusRoll || frame.rollCount >= 3;
    }

    private bool ShouldResetPinsForBonusRoll(FrameScore frame)
    {
        if (frame.rollCount == 1 && frame.rolls[0] == NUM_PINS)
        {
            return true;
        }

        if (frame.rollCount == 2 && frame.rolls[1] == NUM_PINS)
        {
            return true;
        }

        if (frame.rollCount == 2 && frame.IsSpare)
        {
            return true;
        }

        return false;
    }

    private void AdvanceTurn()
    {
        PlayerScore player = playerScores[currentPlayerIndex];
        player.currentFrameIndex++;

        if (currentPlayerIndex == PLAYER_COUNT - 1)
        {
            currentPlayerIndex = 0;
        }
        else
        {
            currentPlayerIndex++;
        }

        gameFinished = true;
        for (int i = 0; i < PLAYER_COUNT; i++)
        {
            if (playerScores[i].currentFrameIndex < FRAME_COUNT)
            {
                gameFinished = false;
                break;
            }
        }

        if (!gameFinished && playerScores[currentPlayerIndex].currentFrameIndex >= FRAME_COUNT)
        {
            currentPlayerIndex = FindNextActivePlayerIndex();
        }
    }

    private int FindNextActivePlayerIndex()
    {
        for (int offset = 0; offset < PLAYER_COUNT; offset++)
        {
            int playerIndex = (currentPlayerIndex + offset) % PLAYER_COUNT;
            if (playerScores[playerIndex].currentFrameIndex < FRAME_COUNT)
            {
                return playerIndex;
            }
        }

        return 0;
    }

    private void ResetPinsOnly()
    {
        ClearPins();
        score = 0;
        SpawnPins();
    }

    private void RemoveFallenPins()
    {
        for (int i = pins.Count - 1; i >= 0; i--)
        {
            Pin pin = pins[i];
            if (pin == null)
            {
                pins.RemoveAt(i);
                continue;
            }

            if (!pin.IsStanding())
            {
                Destroy(pin.gameObject);
                pins.RemoveAt(i);
            }
        }

        score = 0;
    }

    private void RecalculatePinScore()
    {
        score = 0; // 毎フレーム、いま倒れているピン数を数え直す
        foreach (Pin pin in pins)
        {
            if (pin == null)
            {
                continue;
            }

            if (!pin.IsStanding())
            {
                score++;
            }
        }
    }

    private void SpawnPins()
    {
        if (pinObject == null)
        {
            Debug.LogError("OrderPin: Cannot spawn pins because pinObject is not assigned.", this);
            return;
        }

        pins.Clear();

        float object_size = 0.9f;
        float spacing = object_size * 1.2f;

        // 三角形の段数
        int rows = 4;

        for (int row = 0; row < rows; row++)
        {
            // その段に置くピン数
            int pinsInRow = row + 1;

            for (int col = 0; col < pinsInRow; col++)
            {
                // 横方向
                float z = (col - row * 0.5f) * spacing;

                // 奥方向
                float x = pinStartX + row * spacing;

                Vector3 pinPosition = new Vector3(x, 1, z);

                Pin instantiatedPin = Instantiate(
                    pinObject,
                    pinPosition,
                    Quaternion.identity);

                pins.Add(instantiatedPin);
            }
        }
    }

    private void ClearPins()
    {
        foreach (Pin pin in pins)
        {
            if (pin != null)
            {
                Destroy(pin.gameObject);
            }
        }

        pins.Clear();
    }

    private OrderPin FindConfiguredSiblingOrderPin()
    {
        OrderPin[] orderPins = GetComponents<OrderPin>();

        foreach (OrderPin orderPin in orderPins)
        {
            if (orderPin != this && orderPin.pinObject != null)
            {
                return orderPin;
            }
        }

        return null;
    }
}
