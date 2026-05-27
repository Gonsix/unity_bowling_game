using UnityEngine;
using System.Collections.Generic;

public class OrderPin : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    const int NUM_PINS = 10;
    [SerializeField] private Pin pinObject; // Reference to the pin prefab

    int score = 0; // ピンが倒れた数とする

    readonly List<Pin> pins = new List<Pin>(NUM_PINS);


    void Start()
    {
        if (pinObject == null)
        {
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
                float x = row * spacing;

                Vector3 pinPosition = new Vector3(x, 1, z);

                Pin instantiatedPin = Instantiate(
                    pinObject,
                    pinPosition,
                    Quaternion.identity);

                pins.Add(instantiatedPin);
            }
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
        print("score: " + score);

    }
}
