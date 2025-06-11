using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using WebSocketSharp;
using static ConnectionManagerServer;

public class EventManagerServer : MonoBehaviour
{
    NativeList<byte> buffer = new NativeList<byte>(Allocator.Persistent);
    ConnectionManagerServer connectionManager;
    
    private void Start()
    {
        connectionManager = GetComponent<ConnectionManagerServer>();
    }

    public void Tick()
    {
        if (buffer.Length <= 0)
            return;

        foreach (uint key in connectionManager.playerData.Keys)
        {
            if (!connectionManager.playerData[key].networkConnection.IsCreated)
                continue;

            connectionManager.networkDriver.BeginSend(connectionManager.reliablePipe, connectionManager.playerData[key].networkConnection, out var tempWriter);
            tempWriter.WriteByte(GamePackets.EVENT_MANAGER_ID);
            tempWriter.WriteBytes(buffer.AsArray());
            connectionManager.networkDriver.EndSend(tempWriter);
        }
        buffer.Clear();
    }
    public void RemoveConnectionRPC(uint connectionNum)
    {
        var writer = new DataStreamWriter(5, Allocator.Temp);
        writer.WriteByte(GamePackets.RPC_PLAYER_DISCONNECT);
        writer.WriteUInt(connectionNum);
        buffer.AddRange(writer.AsNativeArray());
    }
}
