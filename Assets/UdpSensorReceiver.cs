using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UdpSensorReceiver : MonoBehaviour
{
    [Serializable]
    public class Vector3Data
    {
        public double x;
        public double y;
        public double z;
    }

    [Serializable]
    public class OrientationData
    {
        public double pitch;
        public double roll;
        public double yaw;
    }

    [Serializable]
    public class SensorData
    {
        public double time;
        public Vector3Data accel;
        public OrientationData orientation;
        public bool button;
    }

    [SerializeField] private int listenPort = 5003;
    [SerializeField] private bool logReceivedData = false;

    private static readonly object activePortsLock = new object();
    private static readonly System.Collections.Generic.Dictionary<int, UdpSensorReceiver> activeReceiversByPort =
        new System.Collections.Generic.Dictionary<int, UdpSensorReceiver>();

    private readonly object latestJsonLock = new object();
    private string latestJson;
    private Thread receiveThread;
    private UdpClient udpClient;
    private volatile bool isRunning;

    public SensorData LatestData { get; private set; }
    public bool HasData { get; private set; }

    private void OnEnable()
    {
        StartServer();
    }

    private void Update()
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

        try
        {
            LatestData = JsonUtility.FromJson<SensorData>(jsonToParse);
            HasData = LatestData != null;

            if (logReceivedData && HasData)
            {
                Debug.Log(
                    $"UDP sensor: time={LatestData.time}, accel=({LatestData.accel.x:F2}, {LatestData.accel.y:F2}, {LatestData.accel.z:F2}), " +
                    $"orientation=({LatestData.orientation.pitch:F2}, {LatestData.orientation.roll:F2}, {LatestData.orientation.yaw:F2}), button={LatestData.button}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Invalid UDP JSON: {jsonToParse}\n{ex.Message}");
        }
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

        UdpSensorReceiver receiverToStop = null;
        lock (activePortsLock)
        {
            if (activeReceiversByPort.TryGetValue(listenPort, out UdpSensorReceiver activeReceiver) &&
                activeReceiver != null &&
                activeReceiver != this)
            {
                receiverToStop = activeReceiver;
            }
        }

        if (receiverToStop != null)
        {
            Debug.LogWarning(
                $"UDP sensor receiver on {name} is closing existing receiver {receiverToStop.name} for port {listenPort}.",
                this);
            receiverToStop.StopServer();
        }

        lock (activePortsLock)
        {
            if (activeReceiversByPort.TryGetValue(listenPort, out UdpSensorReceiver activeReceiver) &&
                activeReceiver != null &&
                activeReceiver != this)
            {
                Debug.LogWarning(
                    $"UDP sensor receiver on {name} could not take port {listenPort}; existing receiver {activeReceiver.name} is still active.",
                    this);
                return;
            }

            activeReceiversByPort[listenPort] = this;
        }

        isRunning = true;
        receiveThread = new Thread(ReceiveLoop)
        {
            IsBackground = true,
            Name = "UDP Sensor Receiver"
        };
        receiveThread.Start();
    }

    public void StopServer()
    {
        if (!isRunning)
        {
            ReleasePort();
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
                Debug.LogError($"UDP receive error on port {listenPort}: {ex.Message}");
            }
            else
            {
                Debug.LogError($"UDP receiver could not bind port {listenPort}: {ex.Message}", this);
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
                Debug.LogError($"UDP receiver stopped unexpectedly: {ex.Message}");
            }
            else
            {
                Debug.LogError($"UDP receiver failed on port {listenPort}: {ex.Message}", this);
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

    private void ReleasePort()
    {
        lock (activePortsLock)
        {
            if (activeReceiversByPort.TryGetValue(listenPort, out UdpSensorReceiver activeReceiver) &&
                activeReceiver == this)
            {
                activeReceiversByPort.Remove(listenPort);
            }
        }
    }
}
