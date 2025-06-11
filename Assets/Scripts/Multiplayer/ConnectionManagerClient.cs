using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using System.Collections.Generic;
using System;
using Unity.Networking.Transport.Utilities;

public class ConnectionManagerClient : MonoBehaviour
{
    public NetworkDriver networkDriver;
    public NetworkConnection clientConnection;

    public NetworkPipeline reliablePipe;
    public NetworkPipeline unreliablePipe;

    public uint playerID = 0;

    private const int PORT = 1027;
    private MovementManagerClient movementManager;
    private EventManagerClient eventManager;

    void Start()
    {
        movementManager = GetComponent<MovementManagerClient>();
        eventManager = GetComponent<EventManagerClient>();
        var settings = new NetworkSettings();
        settings.WithSimulatorStageParameters(
            maxPacketCount: 100,
            mode: ApplyMode.AllPackets,
            packetDelayMs: TickConfig.DELAY,
            packetJitterMs: TickConfig.JITTER,
            packetDropPercentage: TickConfig.DROP);

        if (TickConfig.SIMULATE_LAG)
        {
            networkDriver = NetworkDriver.Create(settings);
            reliablePipe = networkDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage), typeof(SimulatorPipelineStage));
            unreliablePipe = networkDriver.CreatePipeline(typeof(UnreliableSequencedPipelineStage), typeof(SimulatorPipelineStage));
        }
        else
        {
            networkDriver = NetworkDriver.Create();
            reliablePipe = networkDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            unreliablePipe = networkDriver.CreatePipeline(typeof(UnreliableSequencedPipelineStage));
        }
        var endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(PORT);
        clientConnection = networkDriver.Connect(endpoint);
    }

    public void Tick()
    {
        networkDriver.ScheduleUpdate().Complete();
        if (!clientConnection.IsCreated)
            return;

        DataStreamReader stream;
        NetworkPipeline pipeline;
        NetworkEvent.Type cmd;
        while ((cmd = clientConnection.PopEvent(networkDriver, out stream, out pipeline)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                Debug.Log("We are now connected!");
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                byte id = stream.ReadByte();
                //Debug.Log($"ID: {id} got from the server");
                processPacketID(id, stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("Client disconnected");
                clientConnection = default;
                //clientConnection.Disconnect(networkDriver);
            }
        }
    }
    private void processPacketID(byte id, DataStreamReader stream)
    {
        switch (id)
        {
            case GamePackets.HELLO_ID:
                playerID = stream.ReadUInt();
                if (movementManager.playerDict.ContainsKey(playerID))
                {
                    var head = movementManager.playerDict[playerID].head;
                    Camera.main.GetComponent<PlayerCamera>().SetFollowTransform(head);
                }
                Debug.Log($"{playerID} is our new id!");
                break;
            case GamePackets.MOV_MANAGER_ID:
                movementManager.ProcessPlayerStatePacket(stream);
                break;
            case GamePackets.EVENT_MANAGER_ID:
                eventManager.ProcessEventsPacket(stream);
                break;
        }
    }
}
