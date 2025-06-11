using System.Collections.Generic;
using System;
using Unity.Collections;
using UnityEngine;

public class EventManagerClient : MonoBehaviour
{
    private delegate void RPCDelegate(ref DataStreamReader stream);
    Dictionary<byte, RPCDelegate> RPCTable;

    private MovementManagerClient movementManager;
    private void Start()
    {
        RPCTable = new Dictionary<byte, RPCDelegate>();
        RPCTable.Add(GamePackets.RPC_PLAYER_DISCONNECT, PlayerDisconnectRPC);

        movementManager = GetComponent<MovementManagerClient>();
    }
    public void ProcessEventsPacket(DataStreamReader stream)
    {
        while (stream.GetBytesRead() < stream.Length)
        {
            byte RPC_ID = stream.ReadByte();
            RPCTable[RPC_ID](ref stream);
        }
    }

    public void PlayerDisconnectRPC(ref DataStreamReader stream)
    {
        uint playerIdToRemove = stream.ReadUInt();
        Debug.Log($"Event manager client needs to remove {playerIdToRemove}");
        movementManager.RemovePlayer(playerIdToRemove);
    }
}
