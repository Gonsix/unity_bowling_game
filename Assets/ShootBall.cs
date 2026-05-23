using UnityEngine;

public class ShootBall : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private Rigidbody rb;

    // [SerializeField] private Transform ball;

    [SerializeField] private float laneWidth = 4f;
    [SerializeField] private float shootForce = 3600;
    private bool nageta = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    // 毎フレーム呼び出される
    private void Update()
    {

        if (Input.GetMouseButtonDown(0) && !nageta)
        {

            Vector3 mousePos = Input.mousePosition;

            float normalizedZ = mousePos.x / Screen.width;

            float laneZ = Mathf.Lerp(laneWidth / 2f, -laneWidth / 2f, normalizedZ);

            Vector3 newPosition = transform.position;

            newPosition.z = laneZ;

            transform.position = newPosition;

            Shoot(shootForce);
            nageta = true;

        }

    }

    public void Shoot(float acc)
    {
        rb.AddForce(new Vector3(acc, 0, 0));
    }


}
