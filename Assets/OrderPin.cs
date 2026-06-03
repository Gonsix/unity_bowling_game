using UnityEngine;
using System.Collections.Generic;

public class OrderPin : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    const int NUM_PINS = 10;
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
    private Transform scoreboardTransform;
    private TextMesh scoreboardText;
    private Camera mainCamera;
    private int displayedScore = -1;

    public int Score => score;


    void Start()
    {
        if (pinObject == null)
        {
            if (HasConfiguredSiblingOrderPin())
            {
                enabled = false;
                return;
            }

            Debug.LogError("OrderPin: pinObject is not assigned.", this);
            enabled = false;
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

        if (showScoreboard)
        {
            CreateScoreboard();
            UpdateScoreboardText();
        }
    }

    // Update is called once per frame
    void Update()
    {
        score = 0; // なんで毎回結果がゼロにはならないのか？→ 毎フレームスコアを数え直すため
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

        if (showScoreboard)
        {
            UpdateScoreboardText();
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

        GameObject textObject = new GameObject("Score Text", typeof(TextMesh));
        textObject.transform.SetParent(scoreboardObject.transform, false);
        textObject.transform.localPosition = new Vector3(0f, 0f, frontZ - 0.12f);

        scoreboardText = textObject.GetComponent<TextMesh>();
        scoreboardText.anchor = TextAnchor.MiddleCenter;
        scoreboardText.alignment = TextAlignment.Center;
        scoreboardText.color = scoreboardTextColor;
        scoreboardText.characterSize = 0.62f;
        scoreboardText.fontSize = 96;
        scoreboardText.fontStyle = FontStyle.Bold;
    }

    private void CreateCubePart(string objectName, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
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
    }

    private void UpdateScoreboardText()
    {
        if (scoreboardText == null || score == displayedScore)
        {
            return;
        }

        displayedScore = score;
        scoreboardText.text = $"SCORE\n{score} / {NUM_PINS}";
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
}
