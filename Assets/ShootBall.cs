using UnityEngine;

public class ShootBall : MonoBehaviour
{
    private Rigidbody rb;

    [Header("UDP Input")]
    [SerializeField] private UdpSensorReceiver udpReceiver;
    [SerializeField] private bool useUdpInput = true;
    [SerializeField] private float udpForceScale = 1200f;
    [SerializeField] private float minimumUdpForce = 800f;
    [SerializeField] private float maximumUdpForce = 6000f;

    [Header("Mouse Fallback")]
    [SerializeField] private bool allowMouseInput = true;
    [SerializeField] private float laneWidth = 4f;
    [SerializeField] private float shootForce = 3600f;

    [Header("Wall Bounce")]
    [SerializeField] private bool suppressUpwardWallBounce = true;

    [Header("Reset")]
    [SerializeField] private KeyCode resetKey = KeyCode.R;

    [Header("Debug Shot")]
    [SerializeField] private KeyCode debugStraightShotKey = KeyCode.S;
    [SerializeField] private float debugStraightShotForce = 9000f;

    private bool nageta = false;
    private bool previousUdpButton = false;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private void Awake()
    {
        EnsureRigidbody();
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    private void Start()
    {
        if (udpReceiver == null)
        {
            udpReceiver = FindFirstObjectByType<UdpSensorReceiver>();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(resetKey))
        {
            ResetBall();
            return;
        }

        if (Input.GetKeyDown(debugStraightShotKey))
        {
            DebugStraightShot();
            return;
        }

        if (nageta)
        {
            UpdatePreviousUdpButton();
            return;
        }

        if (useUdpInput && TryShootFromUdp())
        {
            nageta = true;
            return;
        }

        if (allowMouseInput && Input.GetMouseButtonDown(0))
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

    private bool TryShootFromUdp()
    {
        if (udpReceiver == null || !udpReceiver.HasData || udpReceiver.LatestData == null)
        {
            return false;
        }

        bool currentButton = udpReceiver.LatestData.button;
        bool buttonDown = currentButton && !previousUdpButton;
        previousUdpButton = currentButton;

        if (!buttonDown || udpReceiver.LatestData.accel == null)
        {
            return false;
        }

        Vector3 sensorVector = new Vector3(
            (float)udpReceiver.LatestData.accel.x,
            (float)udpReceiver.LatestData.accel.y,
            (float)udpReceiver.LatestData.accel.z);

        Shoot(sensorVector);
        return true;
    }

    private void UpdatePreviousUdpButton()
    {
        if (udpReceiver != null && udpReceiver.HasData && udpReceiver.LatestData != null)
        {
            previousUdpButton = udpReceiver.LatestData.button;
        }
    }

    public void Shoot(Vector3 sensorVector)
    {
        if (!EnsureRigidbody())
        {
            return;
        }

        Vector3 force = sensorVector * udpForceScale;
        float forceMagnitude = Mathf.Clamp(force.magnitude, minimumUdpForce, maximumUdpForce);

        if (force.sqrMagnitude <= Mathf.Epsilon)
        {
            force = Vector3.right * minimumUdpForce;
        }
        else
        {
            force = force.normalized * forceMagnitude;
        }

        rb.AddForce(force);
    }

    public void Shoot(float acc)
    {
        if (!EnsureRigidbody())
        {
            return;
        }

        rb.AddForce(new Vector3(acc, 0, 0));
    }

    private void DebugStraightShot()
    {
        if (!EnsureRigidbody())
        {
            return;
        }

        rb.AddForce(new Vector3(debugStraightShotForce, 0f, 0f));
        nageta = true;
    }

    private void ResetBall()
    {
        if (!EnsureRigidbody())
        {
            return;
        }

        transform.SetPositionAndRotation(initialPosition, initialRotation);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
        rb.WakeUp();

        nageta = false;
        previousUdpButton = false;
        UpdatePreviousUdpButton();
    }

    private void OnCollisionEnter(Collision collision)
    {
        SuppressUpwardVelocityFromWall(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        SuppressUpwardVelocityFromWall(collision);
    }

    private void SuppressUpwardVelocityFromWall(Collision collision)
    {
        if (!EnsureRigidbody() || !suppressUpwardWallBounce)
        {
            return;
        }

        if (rb.linearVelocity.y <= 0f)
        {
            return;
        }

        if (collision == null || collision.collider == null)
        {
            return;
        }

        GameObject collidedObject = collision.collider.gameObject;
        if (collidedObject == null || !collidedObject.name.Contains("Wall"))
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
    }

    private bool EnsureRigidbody()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            return true;
        }

        Debug.LogWarning("ShootBall needs a Rigidbody on the same GameObject.", this);
        enabled = false;
        return false;
    }
}
