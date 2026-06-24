using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ARMarkerReceiver : MonoBehaviour
{
    [Serializable]
    public class MarkerPositionData
    {
        public string timestamp;
        public int marker_id;
        public MarkerDetectionResult detection_result;
    }

    [Serializable]
    public class MarkerDetectionResult
    {
        public bool detected;
        public double camera_distance_m;
        public double lateral_offset_m;
        public MarkerPositionMeters position_m;
    }

    [Serializable]
    public class MarkerPositionMeters
    {
        public double x;
        public double y;
        public double z;
    }

    [SerializeField] private int listenPort = 5007;
    [SerializeField] private bool logMarkerPositionEveryFrame = false;
    [SerializeField] private bool showDebugOverlay = false;

    private static readonly object activePortsLock = new object();
    private static readonly System.Collections.Generic.Dictionary<int, ARMarkerReceiver> activeReceiversByPort =
        new System.Collections.Generic.Dictionary<int, ARMarkerReceiver>();

    private readonly object latestJsonLock = new object();
    private string latestJson;
    private string latestRawJson;
    private string lastParseError;
    private Thread receiveThread;
    private UdpClient udpClient;
    private volatile bool isRunning;
    private int receivedPacketCount;
    private float lastReceivedRealtime = -1f;
    private MarkerPositionData latestData;
    private bool hasData;
    private float latestCenterRelativeLateralMeters;
    private ARMarkerReceiver proxySource;

    public MarkerPositionData LatestData => proxySource != null ? proxySource.LatestData : latestData;
    public bool HasData => proxySource != null ? proxySource.HasData : hasData;
    public float LatestCenterRelativeLateralMeters => proxySource != null ? proxySource.LatestCenterRelativeLateralMeters : latestCenterRelativeLateralMeters;
    public bool IsRunning => isRunning || (proxySource != null && proxySource.IsRunning);
    public int ReceivedPacketCount => proxySource != null ? proxySource.ReceivedPacketCount : receivedPacketCount;
    public string LastParseError => proxySource != null ? proxySource.LastParseError : lastParseError;
    public float SecondsSinceLastPacket => proxySource != null
        ? proxySource.SecondsSinceLastPacket
        : lastReceivedRealtime < 0f ? -1f : Time.realtimeSinceStartup - lastReceivedRealtime;
    public bool IsProxyReceiver => proxySource != null;

    private void OnEnable()
    {
        StartServer();
    }

    private void Update()
    {
        UpdateMarkerPositionData();
        LogMarkerPositionEveryFrame();
    }

    private void UpdateMarkerPositionData()
    {
        string jsonToParse = null;

        lock (latestJsonLock)
        {
            if (!string.IsNullOrEmpty(latestJson))
            {
                jsonToParse = latestJson;
                latestJson = null;
            }
        }

        if (jsonToParse == null)
        {
            return;
        }

        lastReceivedRealtime = Time.realtimeSinceStartup;

        try
        {
            latestData = JsonUtility.FromJson<MarkerPositionData>(jsonToParse);
            lastParseError = null;
            hasData =
                latestData != null &&
                latestData.detection_result != null &&
                latestData.detection_result.detected;

            if (hasData)
            {
                latestCenterRelativeLateralMeters =
                    -(float)latestData.detection_result.lateral_offset_m;
            }
        }
        catch (Exception ex)
        {
            hasData = false;
            lastParseError = ex.Message;
            Debug.LogWarning($"Invalid AR marker UDP JSON: {jsonToParse}\n{ex.Message}");
        }
    }

    private void LogMarkerPositionEveryFrame()
    {
        if (proxySource != null || !logMarkerPositionEveryFrame || !HasData)
        {
            return;
        }

        MarkerDetectionResult detection = LatestData.detection_result;
        MarkerPositionMeters position = detection.position_m;

        string positionText = position == null
            ? "position=(null)"
            : $"position=({position.x:F3}, {position.y:F3}, {position.z:F3})m";

        Debug.Log(
            $"AR marker UDP: timestamp={LatestData.timestamp}, marker_id={LatestData.marker_id}, " +
            $"center_lateral={LatestCenterRelativeLateralMeters:F3}m (left + / right -), " +
            $"raw_lateral={detection.lateral_offset_m:F3}m, distance={detection.camera_distance_m:F3}m, {positionText}",
            this);
    }

    private void OnGUI()
    {
        if (!showDebugOverlay)
        {
            return;
        }

        const int width = 520;
        GUILayout.BeginArea(new Rect(10f, 10f, width, 170f), GUI.skin.box);
        GUILayout.Label($"ARMarkerReceiver UDP:{listenPort} running={IsRunning} packets={ReceivedPacketCount} proxy={IsProxyReceiver}");
        GUILayout.Label(SecondsSinceLastPacket < 0f
            ? "last packet: none"
            : $"last packet: {SecondsSinceLastPacket:F2}s ago");
        GUILayout.Label($"HasData={HasData}");

        if (LatestData != null && LatestData.detection_result != null)
        {
            MarkerPositionMeters position = LatestData.detection_result.position_m;
            string xText = position == null ? "null" : $"{position.x:F4}m";
            string yText = position == null ? "null" : $"{position.y:F4}m";
            string zText = position == null ? "null" : $"{position.z:F4}m";
            GUILayout.Label($"marker_id={LatestData.marker_id} detected={LatestData.detection_result.detected}");
            GUILayout.Label($"position_m.x={xText} y={yText} z={zText}");
            GUILayout.Label($"lateral_offset_m={LatestData.detection_result.lateral_offset_m:F4} center_lateral={LatestCenterRelativeLateralMeters:F4}");
        }

        if (!string.IsNullOrEmpty(lastParseError))
        {
            GUILayout.Label($"parse error: {lastParseError}");
        }

        if (!string.IsNullOrEmpty(latestRawJson))
        {
            string preview = latestRawJson.Length > 120 ? latestRawJson.Substring(0, 120) + "..." : latestRawJson;
            GUILayout.Label($"raw: {preview}");
        }

        GUILayout.EndArea();
    }

    private void OnDisable()
    {
        StopServer();
    }

    private void OnDestroy()
    {
        StopServer();
    }

    private void OnApplicationQuit()
    {
        StopServer();
    }

    public void StartServer()
    {
        if (isRunning)
        {
            return;
        }

        lock (activePortsLock)
        {
            if (activeReceiversByPort.TryGetValue(listenPort, out ARMarkerReceiver activeReceiver) &&
                activeReceiver != null &&
                activeReceiver != this)
            {
                proxySource = activeReceiver;
                Debug.LogWarning($"UDP AR marker receiver on {name} is using existing receiver {activeReceiver.name} for port {listenPort}.", this);
                return;
            }

            activeReceiversByPort[listenPort] = this;
        }

        proxySource = null;
        isRunning = true;
        receiveThread = new Thread(ReceiveLoop)
        {
            IsBackground = true,
            Name = "UDP AR Marker Receiver"
        };
        receiveThread.Start();
    }

    public void StopServer()
    {
        if (!isRunning)
        {
            proxySource = null;
            return;
        }

        isRunning = false;
        udpClient?.Close();
        udpClient = null;
        ReleasePort();

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(200);
        }

        receiveThread = null;
    }

    private void ReceiveLoop()
    {
        try
        {
            udpClient = CreateUdpClient(listenPort);
            var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (isRunning)
            {
                byte[] bytes = udpClient.Receive(ref remoteEndPoint);
                string json = Encoding.UTF8.GetString(bytes);

                lock (latestJsonLock)
                {
                    latestJson = json;
                    latestRawJson = json;
                    receivedPacketCount++;
                }
            }
        }
        catch (SocketException ex)
        {
            isRunning = false;
            udpClient?.Close();
            udpClient = null;
            ReleasePort();

            if (isRunning)
            {
                Debug.LogError($"UDP AR marker receive error on port {listenPort}: {ex.Message}");
            }
            else
            {
                Debug.LogError($"UDP AR marker receiver could not bind port {listenPort}: {ex.Message}", this);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            isRunning = false;
            udpClient?.Close();
            udpClient = null;
            ReleasePort();

            if (isRunning)
            {
                Debug.LogError($"UDP AR marker receiver stopped unexpectedly: {ex.Message}");
            }
            else
            {
                Debug.LogError($"UDP AR marker receiver failed on port {listenPort}: {ex.Message}", this);
            }
        }
    }

    private void ReleasePort()
    {
        lock (activePortsLock)
        {
            if (activeReceiversByPort.TryGetValue(listenPort, out ARMarkerReceiver activeReceiver) &&
                activeReceiver == this)
            {
                activeReceiversByPort.Remove(listenPort);
            }
        }
    }

    private static UdpClient CreateUdpClient(int port)
    {
        UdpClient client = new UdpClient(AddressFamily.InterNetwork);

        try
        {
            client.ExclusiveAddressUse = false;
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            return client;
        }
        catch
        {
            client.Close();
            throw;
        }
    }
}
