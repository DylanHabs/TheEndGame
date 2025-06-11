using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using Unity.Collections;
using System.Runtime.InteropServices;
using System;
using UnityEngine.InputSystem;

public class ConnectionManagerServer : MonoBehaviour
{
    public NetworkDriver networkDriver;
    //public NativeList<NetworkConnection> connections;
    public NetworkPipeline reliablePipe;
    public NetworkPipeline unreliablePipe;
    private const int PORT = 1027;

    public Dictionary<uint, PlayerData> playerData;
    public GameObject playerPrefab;
    private uint playerIDCounter = 1;
    private MovementManagerServer movementManager;
    private EventManagerServer eventManager;

    private void Start()
    {
        movementManager = GetComponent<MovementManagerServer>();
        eventManager = GetComponent<EventManagerServer>();

        var settings = new NetworkSettings();
        settings.WithNetworkConfigParameters(
            connectTimeoutMS: 1000,
            maxConnectAttempts: 10,
            disconnectTimeoutMS: 10000);
        networkDriver = NetworkDriver.Create(settings);

        playerData = new Dictionary<uint, PlayerData>();
        reliablePipe = networkDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
        unreliablePipe = networkDriver.CreatePipeline(typeof(UnreliableSequencedPipelineStage));

        var endpoint = NetworkEndpoint.AnyIpv4.WithPort(PORT);
        if (networkDriver.Bind(endpoint) != 0)
        {
            Debug.Log($"failed to start sever on port {PORT}");
        }
        networkDriver.Listen();
    }

    private void Update()
    {
        networkDriver.ScheduleUpdate().Complete();

        //accept new connections
        NetworkConnection c;
        while ((c = networkDriver.Accept()) != default)
        {
            //create new player
            GameObject newplayerObject = Instantiate(playerPrefab, new Vector3(0, 10, 0), new Quaternion());
            PlayerData newPlayer = new PlayerData(c, newplayerObject);
            playerData[playerIDCounter] = (newPlayer);
            Debug.Log("Added new connection");

            networkDriver.BeginSend(reliablePipe, c, out var writer);
            writer.WriteByte(GamePackets.HELLO_ID);
            writer.WriteUInt(playerIDCounter);
            playerIDCounter++;
            networkDriver.EndSend(writer);
        }

        //loop and check all connections
        List<uint> keysToRemove = new List<uint>();
        foreach (uint key in playerData.Keys)
        {
            DataStreamReader stream;
            NetworkEvent.Type cmd;

            while ((cmd = networkDriver.PopEventForConnection(playerData[key].networkConnection, out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    byte id = stream.ReadByte();
                    processPacketID(id, stream, key);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client has left :(");
                    keysToRemove.Add(key);
                    break;
                }
            }
        }
        foreach (uint key in keysToRemove)
            RemoveConnection(key);
    }
    private void processPacketID(byte id, DataStreamReader stream, uint connection)
    {
        switch (id)
        {
            case GamePackets.INPUT_ID:
                movementManager.ProcessInputData(ref stream, connection);
                break;
        }
    }
    public void RemoveConnection(uint i)
    {
        Destroy(playerData[i].playerObject);
        playerData.Remove(i);
        eventManager.RemoveConnectionRPC(i);
    }
    private void OnDestroy()
    {
        if (networkDriver.IsCreated)
        {
            networkDriver.Dispose();
            //foreach (uint key in playerData.Keys)
            //{
            //    RemoveConnection(key);
            //}
        }
    }

    public class PlayerData
    {
        public NetworkConnection networkConnection;
        public GameObject playerObject;
        public Player player;
        public uint expectedInput;
        public PlayerData(NetworkConnection _networkConnection, GameObject _playerObject)
        {
            networkConnection = _networkConnection;
            playerObject = _playerObject;
            player = playerObject.GetComponent<Player>();
            expectedInput = 0;
        }
    }
}
