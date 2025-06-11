using KinematicCharacterController;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class MovementManagerServer : MonoBehaviour
{
    //each player has a sorted dictionary of movments where they key is the input packet id
    private Dictionary<uint, SortedDictionary<uint, GamePackets.MoveInput>> InputsBuffers;
    //private Dictionary<uint, GamePackets.MoveInput> movements;
    private ConnectionManagerServer connectionManager;
    private void Start()
    {
        //movements = new Dictionary<uint, GamePackets.MoveInput>();
        InputsBuffers = new Dictionary<uint, SortedDictionary<uint, GamePackets.MoveInput>>();
        connectionManager = GetComponent<ConnectionManagerServer>();
    }
    public void Tick()
    {
        UpdatePlayers();
        //Vector3 oldPos = connectionManager.playerData[1].playerObject.transform.position;
        KinematicCharacterSystem.GetInstance().Tick(TickConfig.SERVER_TICK_TIME);
        //Vector3 offset = connectionManager.playerData[1].playerObject.transform.position - oldPos;
        //Debug.Log($"SERVER {connectionManager.playerData[1].expectedInput - 1} offset {offset} from {oldPos} to {connectionManager.playerData[1].playerObject.transform.position}");
        SendStateToPlayers();
    }
    private void UpdatePlayers()
    {
        List<uint> keysToRemove = new List<uint>();
        foreach (KeyValuePair<uint, SortedDictionary<uint, GamePackets.MoveInput>> kvp in InputsBuffers)
        {
            //handle disconnected players
            if (!connectionManager.playerData.ContainsKey(kvp.Key))
            {
                keysToRemove.Add(kvp.Key);
                Debug.Log("REMOVING PLAYER");
                continue;
            }

            uint playerID = kvp.Key;
            SortedDictionary<uint, GamePackets.MoveInput> playerBuffer = kvp.Value;
            uint expectedInputID = connectionManager.playerData[playerID].expectedInput;
            bool packetLost = false;
            if (expectedInputID >= TickConfig.BUFFER)
            {
                //Procces expected input
                GamePackets.MoveInput data;
                if (!playerBuffer.TryGetValue(expectedInputID, out data)) 
                {
                    //We don't have the packet we expect so use whatever was last
                    Debug.Log($"{playerID} Missing packet {expectedInputID}");
                    playerBuffer[expectedInputID] = playerBuffer[expectedInputID - 1];
                    data = playerBuffer[expectedInputID];
                    packetLost = true;
                }

                Vector2 moveValue = Vector2.zero;
                if (IsBitSet(data.moveByte, 0)) //bit 0 = W(forward)
                    moveValue.y = 1;
                else if (IsBitSet(data.moveByte, 1)) //bit 1 = S(backward)
                    moveValue.y = -1;

                if (IsBitSet(data.moveByte, 2)) //bit 2 = A(left)
                    moveValue.x = 1;
                else if (IsBitSet(data.moveByte, 3)) //bit 3 = D(right)
                    moveValue.x = -1;
                bool jumped = IsBitSet(data.moveByte, 4); //bit 4 = Jump

                connectionManager.playerData[data.playerID].player.MovePlayer(moveValue, jumped, data.camY); //Move the player
            }
            //Trim the input buffer
            if (expectedInputID > 1)
                playerBuffer.Remove(expectedInputID - 1);

            //Debug.Log($"Player buffer size {playerBuffer.Count}");
            //We now expect the next input
            if (!packetLost)
                connectionManager.playerData[playerID].expectedInput += 1;
        }
        foreach (uint key in keysToRemove)
            InputsBuffers.Remove(key);
    }
    private void SendStateToPlayers()
    {
        //Now send the state of all players back to the clients 
        //create main packet payload
        int bytesNeeded = (connectionManager.playerData.Count * 34); //we need 33 bytes per player
        DataStreamWriter tempWriter = new DataStreamWriter(bytesNeeded, Allocator.Temp);
        foreach (uint key in connectionManager.playerData.Keys)
        {
            KinematicCharacterMotorState state = connectionManager.playerData[key].player.motor.GetState();
            tempWriter.WriteUInt(key); //write player id

            tempWriter.WriteFloat(state.Position.x); //write xPos
            tempWriter.WriteFloat(state.Position.y); //write yPos
            tempWriter.WriteFloat(state.Position.z); //write zPos

            tempWriter.WriteFloat(state.Rotation.eulerAngles.y); //write yRot

            tempWriter.WriteFloat(state.BaseVelocity.x); //write xVel
            tempWriter.WriteFloat(state.BaseVelocity.y); //write yVel
            tempWriter.WriteFloat(state.BaseVelocity.z); //write zVel

            //tempWriter.WriteFloat(state.MustUngroundTime);
            tempWriter.WriteByte(connectionManager.playerData[key].player.timeSinceJumpDown);

            byte flags = 0;
            flags |= (byte)((state.MustUnground ? 1 : 0) << 0);
            flags |= (byte)((state.LastMovementIterationFoundAnyGround ? 1 : 0) << 1);
            flags |= (byte)((state.GroundingStatus.FoundAnyGround ? 1 : 0) << 2);
            flags |= (byte)((state.GroundingStatus.IsStableOnGround ? 1 : 0) << 3);
            flags |= (byte)((state.GroundingStatus.SnappingPrevented ? 1 : 0) << 4);
            tempWriter.WriteByte(flags);
        }
        //send main packet payload with extra id and timestamp per-client
        foreach (uint key in connectionManager.playerData.Keys)
        {
            if (!connectionManager.playerData[key].networkConnection.IsCreated)
                continue;

            connectionManager.networkDriver.BeginSend(connectionManager.unreliablePipe, connectionManager.playerData[key].networkConnection, out var writer);
            writer.WriteByte(GamePackets.MOV_MANAGER_ID); //Packet Id
            if (connectionManager.playerData[key].expectedInput == 0)
                writer.WriteUInt(0);
            else
                writer.WriteUInt(connectionManager.playerData[key].expectedInput - 1); //Write the timestamp
            writer.WriteBytes(tempWriter.AsNativeArray());
            connectionManager.networkDriver.EndSend(writer);
        }
    }
    public void ProcessInputData(ref DataStreamReader stream, uint connectionID)
    {
        while (stream.GetBytesRead() < stream.Length)
        {
            byte moveByte = stream.ReadByte();
            float camY = stream.ReadFloat();
            uint packetID = stream.ReadUInt();
            AddMove(moveByte, connectionID, camY, packetID);
        }
    }
    public void AddMove(byte moveByte, uint playerID, float camY, uint _packetID)
    {
        if (!InputsBuffers.ContainsKey(playerID))
        {
            InputsBuffers.Add(playerID, new SortedDictionary<uint, GamePackets.MoveInput>());
            InputsBuffers[playerID][TickConfig.BUFFER - 1] = new GamePackets.MoveInput(0, playerID, 0, TickConfig.BUFFER - 1);
        }

        if (connectionManager.playerData[playerID].expectedInput <= _packetID)
        {
            InputsBuffers[playerID][_packetID] = new GamePackets.MoveInput(moveByte, playerID, camY, _packetID);
            //Debug.Log($"SERVER ADDED INPUT {_packetID}");
        }
    }
    private bool IsBitSet(byte b, int pos)
    {
        return (b & (1 << pos)) != 0;
    }
}
