using UnityEngine;

public class ShootBall : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    // 毎フレーム呼び出される
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            Shoot(1200);
        }
    }

    public void Shoot(float acc)
    {
        rb.AddForce(new Vector3(acc, 0, 0));
    }


}
