using UnityEngine;

public class ShootBall : MonoBehaviour
{
    private Rigidbody rb;

    [Header("UDP Input")]
    [SerializeField] private UdpSensorReceiver udpReceiver;
    [SerializeField] private bool useUdpInput = true;
    [SerializeField] private float udpForceScale = 9600f;
    [SerializeField] private float minimumUdpForce = 3200f;
    [SerializeField] private float maximumUdpForce = 24000f;

    [Header("UDP Shot Correction")]
    [SerializeField] private float straightAssist = 0.85f;
    [SerializeField] private float lateralSensitivity = 0.45f;
    [SerializeField] private float verticalSensitivity = 0.12f;
    [SerializeField] private float straightLateralAccelThreshold = 0.8f;
    [SerializeField] private float straightOrientationThreshold = 18f;

    [Header("UDP Calibration")]
    [SerializeField] private bool requireCalibrationOnStart = true;
    [SerializeField] private KeyCode recalibrateKey = KeyCode.C;
    [SerializeField] private float minimumCalibrationAccel = 0.35f;

    [Header("AR Marker Position")]
    [SerializeField] private ARMarkerReceiver arMarkerReceiver;
    [SerializeField] private bool useARMarkerPosition = true;
    [SerializeField, Min(0f)] private float arMarkerZMovementScale = 10f;
    [SerializeField] private bool invertARMarkerXForUnityZ = false;
    [SerializeField] private bool showARMarkerDebugOverlay = false;

    [Header("Mouse Fallback")]
    [SerializeField] private bool allowMouseInput = true;
    [SerializeField] private float laneWidth = 4f;
    [SerializeField] private float shootForce = 3600f;

    [Header("Wall Bounce")]
    [SerializeField] private bool suppressUpwardWallBounce = true;

    [Header("Reset")]
    [SerializeField] private KeyCode resetKey = KeyCode.R;
    [SerializeField] private OrderPin orderPin;

    [Header("Debug Shot")]
    [SerializeField] private KeyCode debugStraightShotKey = KeyCode.S;
    [SerializeField] private float debugStraightShotForce = 9000f;

    private bool nageta = false;
    private bool previousUdpButton = false;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool isHoldingBall = false;
    private int holdSampleCount = 0;
    private float holdMaxAccelMagnitude = 0f;
    private float holdMaxForwardAccel = 0f;
    private float holdLateralAccelSum = 0f;
    private float holdVerticalAccelSum = 0f;
    private Vector3 holdStartAccel = Vector3.zero;
    private Vector3 holdAccelSum = Vector3.zero;
    private Vector3 holdPeakAccel = Vector3.zero;
    private float holdStartYaw = 0f;
    private float holdStartRoll = 0f;
    private float holdMaxYawChange = 0f;
    private float holdMaxRollChange = 0f;
    private bool isCalibrated = false;
    private bool isCalibrationHold = false;
    private Vector3 calibratedForwardSensorAxis = Vector3.right;
    private Vector3 calibratedSideSensorAxis = Vector3.forward;
    private Vector3 calibratedUpSensorAxis = Vector3.up;
    private string arMarkerPositionStatus = "not updated yet";
    private float lastARMarkerX;
    private float lastARMarkerTargetZ;
    private float lastARMarkerAppliedZ;

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
            udpReceiver = FindAnyObjectByType<UdpSensorReceiver>();
        }

        if (arMarkerReceiver == null)
        {
            arMarkerReceiver = FindBestARMarkerReceiver();
        }

        if (orderPin == null)
        {
            orderPin = FindConfiguredOrderPin();
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

        if (Input.GetKeyDown(recalibrateKey))
        {
            isCalibrated = false;
            isCalibrationHold = false;
            Debug.Log("UDP calibration reset. Hold the sensor button, swing straight forward, then release.", this);
            return;
        }

        if (nageta)
        {
            UpdatePreviousUdpButton();
            return;
        }

        UpdateBallPositionFromARMarker();

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

    private void UpdateBallPositionFromARMarker()
    {
        if (!useARMarkerPosition)
        {
            arMarkerPositionStatus = "disabled";
            return;
        }

        if (arMarkerReceiver == null)
        {
            arMarkerReceiver = FindBestARMarkerReceiver();
        }

        if (arMarkerReceiver == null)
        {
            arMarkerPositionStatus = "ARMarkerReceiver is null";
            return;
        }

        if (!arMarkerReceiver.HasData)
        {
            ARMarkerReceiver betterReceiver = FindBestARMarkerReceiver();
            if (betterReceiver != null && betterReceiver != arMarkerReceiver && betterReceiver.HasData)
            {
                arMarkerReceiver = betterReceiver;
            }
        }

        if (!arMarkerReceiver.HasData)
        {
            arMarkerPositionStatus = $"receiver has no parsed data, packets={arMarkerReceiver.ReceivedPacketCount}";
            return;
        }

        if (arMarkerReceiver.LatestData == null)
        {
            arMarkerPositionStatus = "LatestData is null";
            return;
        }

        if (arMarkerReceiver.LatestData.detection_result == null)
        {
            arMarkerPositionStatus = "detection_result is null";
            return;
        }

        if (arMarkerReceiver.LatestData.detection_result.position_m == null)
        {
            arMarkerPositionStatus = "position_m is null";
            return;
        }

        float markerX = (float)arMarkerReceiver.LatestData.detection_result.position_m.x;
        float zOffset = markerX * arMarkerZMovementScale;

        if (invertARMarkerXForUnityZ)
        {
            zOffset = -zOffset;
        }

        Vector3 newPosition = transform.position;
        float halfLaneWidth = laneWidth * 0.5f;
        float targetZ = initialPosition.z + zOffset;
        newPosition.z = Mathf.Clamp(
            targetZ,
            initialPosition.z - halfLaneWidth,
            initialPosition.z + halfLaneWidth);

        transform.position = newPosition;
        if (rb != null)
        {
            rb.position = newPosition;
        }

        lastARMarkerX = markerX;
        lastARMarkerTargetZ = targetZ;
        lastARMarkerAppliedZ = newPosition.z;
        arMarkerPositionStatus = "applied";
    }

    private void OnGUI()
    {
        if (!showARMarkerDebugOverlay)
        {
            return;
        }

        const int width = 520;
        GUILayout.BeginArea(new Rect(10f, 185f, width, 150f), GUI.skin.box);
        GUILayout.Label("ShootBall AR marker movement");
        GUILayout.Label($"enabled={useARMarkerPosition} receiver={(arMarkerReceiver == null ? "null" : arMarkerReceiver.name)} status={arMarkerPositionStatus}");
        GUILayout.Label($"ball thrown={nageta} currentZ={transform.position.z:F4} initialZ={initialPosition.z:F4}");
        GUILayout.Label($"position_m.x={lastARMarkerX:F4} scale={arMarkerZMovementScale:F3} invert={invertARMarkerXForUnityZ}");
        GUILayout.Label($"targetZ={lastARMarkerTargetZ:F4} appliedZ={lastARMarkerAppliedZ:F4} laneWidth={laneWidth:F3}");
        if (arMarkerReceiver != null)
        {
            GUILayout.Label($"receiver running={arMarkerReceiver.IsRunning} packets={arMarkerReceiver.ReceivedPacketCount} hasData={arMarkerReceiver.HasData}");
        }
        GUILayout.EndArea();
    }

    private ARMarkerReceiver FindBestARMarkerReceiver()
    {
        ARMarkerReceiver[] receivers = FindObjectsByType<ARMarkerReceiver>();
        ARMarkerReceiver runningReceiver = null;

        foreach (ARMarkerReceiver receiver in receivers)
        {
            if (receiver == null)
            {
                continue;
            }

            if (receiver.HasData)
            {
                return receiver;
            }

            if (runningReceiver == null && receiver.IsRunning)
            {
                runningReceiver = receiver;
            }
        }

        return runningReceiver ?? (receivers.Length > 0 ? receivers[0] : null);
    }

    private bool TryShootFromUdp()
    {
        if (udpReceiver == null || !udpReceiver.HasData || udpReceiver.LatestData == null)
        {
            return false;
        }

        bool currentButton = udpReceiver.LatestData.button;
        bool buttonDown = currentButton && !previousUdpButton;
        bool buttonUp = !currentButton && previousUdpButton;
        previousUdpButton = currentButton;

        if (buttonDown)
        {
            BeginUdpHold();
        }

        if (currentButton)
        {
            RecordUdpHoldSample(udpReceiver.LatestData);
            return false;
        }

        if (!buttonUp || !isHoldingBall)
        {
            return false;
        }

        bool shotFired = ShootCorrectedUdpShot();
        isHoldingBall = false;
        return shotFired;
    }

    private void BeginUdpHold()
    {
        isHoldingBall = true;
        isCalibrationHold = requireCalibrationOnStart && !isCalibrated;
        holdSampleCount = 0;
        holdMaxAccelMagnitude = 0f;
        holdMaxForwardAccel = 0f;
        holdLateralAccelSum = 0f;
        holdVerticalAccelSum = 0f;
        holdStartAccel = GetLatestAccelVector();
        holdAccelSum = Vector3.zero;
        holdPeakAccel = Vector3.zero;
        holdMaxYawChange = 0f;
        holdMaxRollChange = 0f;

        if (udpReceiver.LatestData != null && udpReceiver.LatestData.orientation != null)
        {
            holdStartYaw = (float)udpReceiver.LatestData.orientation.yaw;
            holdStartRoll = (float)udpReceiver.LatestData.orientation.roll;
        }
    }

    private void RecordUdpHoldSample(UdpSensorReceiver.SensorData sensorData)
    {
        if (sensorData == null || sensorData.accel == null)
        {
            return;
        }

        Vector3 rawAccel = new Vector3(
            (float)sensorData.accel.x,
            (float)sensorData.accel.y,
            (float)sensorData.accel.z);
        Vector3 accel = rawAccel - holdStartAccel;

        holdSampleCount++;
        holdAccelSum += accel;

        float accelMagnitude = accel.magnitude;
        if (accelMagnitude > holdMaxAccelMagnitude)
        {
            holdMaxAccelMagnitude = accelMagnitude;
            holdPeakAccel = accel;
        }

        float forwardAccel = Vector3.Dot(accel, calibratedForwardSensorAxis);
        holdMaxForwardAccel = Mathf.Max(holdMaxForwardAccel, forwardAccel);
        holdLateralAccelSum += Vector3.Dot(accel, calibratedSideSensorAxis);
        holdVerticalAccelSum += Vector3.Dot(accel, calibratedUpSensorAxis);

        if (sensorData.orientation != null)
        {
            float yawChange = Mathf.Abs(Mathf.DeltaAngle(holdStartYaw, (float)sensorData.orientation.yaw));
            float rollChange = Mathf.Abs(Mathf.DeltaAngle(holdStartRoll, (float)sensorData.orientation.roll));

            holdMaxYawChange = Mathf.Max(holdMaxYawChange, yawChange);
            holdMaxRollChange = Mathf.Max(holdMaxRollChange, rollChange);
        }
    }

    private bool ShootCorrectedUdpShot()
    {
        if (isCalibrationHold)
        {
            CalibrateUdpForwardAxis();
            return false;
        }

        if (holdSampleCount <= 0)
        {
            Shoot(minimumUdpForce);
            return true;
        }

        float averageLateralAccel = holdLateralAccelSum / holdSampleCount;
        float averageVerticalAccel = holdVerticalAccelSum / holdSampleCount;
        float orientationWobble = Mathf.Max(holdMaxYawChange, holdMaxRollChange);

        float lateralStraightness = 1f - Mathf.Clamp01(Mathf.Abs(averageLateralAccel) / straightLateralAccelThreshold);
        float orientationStraightness = 1f - Mathf.Clamp01(orientationWobble / straightOrientationThreshold);
        float straightness = Mathf.Min(lateralStraightness, orientationStraightness);

        float forwardAccel = Mathf.Max(holdMaxForwardAccel, holdMaxAccelMagnitude * 0.65f);
        float forceMagnitude = Mathf.Clamp(forwardAccel * udpForceScale, minimumUdpForce, maximumUdpForce);
        float assistAmount = straightAssist * straightness;

        Vector3 shotDirection = new Vector3(
            1f,
            averageVerticalAccel * verticalSensitivity,
            averageLateralAccel * lateralSensitivity);

        shotDirection.z = Mathf.Lerp(shotDirection.z, 0f, assistAmount);
        shotDirection.y = Mathf.Clamp(shotDirection.y, -0.15f, 0.2f);
        shotDirection = shotDirection.sqrMagnitude <= Mathf.Epsilon ? Vector3.right : shotDirection.normalized;

        ShootCorrectedForce(shotDirection * forceMagnitude);
        return true;
    }

    private void CalibrateUdpForwardAxis()
    {
        Vector3 calibrationVector = holdPeakAccel.sqrMagnitude > Mathf.Epsilon ? holdPeakAccel : holdAccelSum;

        if (calibrationVector.magnitude < minimumCalibrationAccel)
        {
            Debug.LogWarning("UDP calibration failed: swing was too small. Hold the button and swing straight forward again.", this);
            isCalibrated = false;
            isCalibrationHold = false;
            return;
        }

        calibratedForwardSensorAxis = calibrationVector.normalized;

        Vector3 referenceAxis = Mathf.Abs(Vector3.Dot(calibratedForwardSensorAxis, Vector3.up)) < 0.95f
            ? Vector3.up
            : Vector3.forward;

        calibratedSideSensorAxis = Vector3.Cross(referenceAxis, calibratedForwardSensorAxis).normalized;
        calibratedUpSensorAxis = Vector3.Cross(calibratedForwardSensorAxis, calibratedSideSensorAxis).normalized;
        isCalibrated = true;
        isCalibrationHold = false;

        Debug.Log($"UDP calibrated. forward=({calibratedForwardSensorAxis.x:F2}, {calibratedForwardSensorAxis.y:F2}, {calibratedForwardSensorAxis.z:F2})", this);
    }

    private void UpdatePreviousUdpButton()
    {
        if (udpReceiver != null && udpReceiver.HasData && udpReceiver.LatestData != null)
        {
            previousUdpButton = udpReceiver.LatestData.button;
        }
    }

    private Vector3 GetLatestAccelVector()
    {
        if (udpReceiver == null || udpReceiver.LatestData == null || udpReceiver.LatestData.accel == null)
        {
            return Vector3.zero;
        }

        return new Vector3(
            (float)udpReceiver.LatestData.accel.x,
            (float)udpReceiver.LatestData.accel.y,
            (float)udpReceiver.LatestData.accel.z);
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

    private void ShootCorrectedForce(Vector3 force)
    {
        if (!EnsureRigidbody())
        {
            return;
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
        isHoldingBall = false;
        isCalibrationHold = false;
        holdSampleCount = 0;
        UpdatePreviousUdpButton();

        if (orderPin == null)
        {
            orderPin = FindConfiguredOrderPin();
        }

        if (orderPin != null)
        {
            orderPin.CompleteCurrentShotAndPrepareNextTurn();
        }
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

    private OrderPin FindConfiguredOrderPin()
    {
        OrderPin[] orderPins = FindObjectsByType<OrderPin>();

        foreach (OrderPin candidate in orderPins)
        {
            if (candidate != null && candidate.HasPinObject)
            {
                return candidate;
            }
        }

        return orderPins.Length > 0 ? orderPins[0] : null;
    }
}
