using KinematicCharacterController;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class MovementManagerClient : MonoBehaviour
{
    public GameObject playerPrefab;
    public Dictionary<uint, Player> playerDict;

    //bit 0 = W(forward)
    //bit 1 = S(backward)
    //bit 2 = A(left)
    //bit 3 = D(right)
    //bit 4 = Jump
    InputAction moveAction;
    InputAction jumpAction;
    public List<(uint, Vector3)> predictionList;
    public List<Player.PlayerInput> inputList;
    public uint tickCounter = TickConfig.BUFFER;
    uint newestPacketID = 0;

    ConnectionManagerClient connectionManager;
    private void Start()
    {
        inputList = new List<Player.PlayerInput>();
        predictionList = new List<(uint, Vector3)>();
        playerDict = new Dictionary<uint, Player>();
        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");
        connectionManager = GetComponent<ConnectionManagerClient>();
    }
    public void Tick()
    {
        //sample moving
        Vector2 moveValue = moveAction.ReadValue<Vector2>();
        moveValue.x = Mathf.Round(moveValue.x);
        moveValue.y = Mathf.Round(moveValue.y);

        //send a packet if we can
        if (connectionManager.networkDriver.BeginSend(connectionManager.unreliablePipe, connectionManager.clientConnection, out var writer) == 0)
        {
            //Add to input list
            inputList.Add(new Player.PlayerInput(moveValue, jumpAction.IsPressed(), Camera.main.transform.rotation.eulerAngles.y, tickCounter));

            writer.WriteByte(GamePackets.INPUT_ID); //Packet Id
            //Write every input in our queue
            for (int i = inputList.Count - 1; i > Mathf.Max(0, inputList.Count - TickConfig.INPUT_MAX - 1); i--)
            {
                Player.PlayerInput input = inputList[i];
                byte moveFlags = GetMoveByte(input);
                writer.WriteByte(moveFlags);            //move data
                writer.WriteFloat(input.camY);          //cam rot
                writer.WriteUInt(input.id);             //Tick ID
            }
            connectionManager.networkDriver.EndSend(writer);

            if (playerDict.ContainsKey(connectionManager.playerID)) //simulate player packet
            {
                playerDict[connectionManager.playerID].MovePlayer(moveValue, jumpAction.IsPressed(), Camera.main.transform.rotation.eulerAngles.y);
            }

            //Debug.Log($"CLIENT{connectionManager.playerID} SENT {tickCounter} + {inputList.Count} other ticks");
            tickCounter += 1;
        }
    }
    private byte GetMoveByte(Player.PlayerInput playerInput)
    {
        byte moveFlags = 0;
        Vector2 moveValue = playerInput.moveValue;

        if (moveValue.x > 0.5f) //left
            moveFlags |= 1 << 2;
        else if (moveValue.x < -0.5f) //right
            moveFlags |= 1 << 3;

        if (moveValue.y > 0.5f) //forward
            moveFlags |= 1 << 0;
        else if (moveValue.y < -0.5f) //back
            moveFlags |= 1 << 1;

        //sample jump
        if (playerInput.jump)
            moveFlags |= 1 << 4;

        return moveFlags;
    }
    public void ProcessPlayerStatePacket(DataStreamReader stream)
    {
        uint packetID = stream.ReadUInt();
        if (packetID <= newestPacketID) //we have already processed packets that are newer
            return;
        newestPacketID = packetID;
        while (stream.GetBytesRead() < stream.Length)
        {
            uint playerID = stream.ReadUInt(); //read player id

            Vector3 playerPos = Vector3.zero;
            playerPos.x = stream.ReadFloat(); //read xPos
            playerPos.y = stream.ReadFloat(); //read yPos
            playerPos.z = stream.ReadFloat(); //read zPos

            float playerRotY = stream.ReadFloat();

            Vector3 vel = Vector3.zero;
            vel.x = stream.ReadFloat(); //read xVel
            vel.y = stream.ReadFloat(); //read yVel
            vel.z = stream.ReadFloat(); //read zVel

            //float ungroundTime = stream.ReadFloat();
            byte timeSinceJump = stream.ReadByte();

            byte flags = stream.ReadByte();
            bool mustUnground = IsBitSet(flags, 0);
            bool foundGround = IsBitSet(flags, 1);
            bool foundGround2 = IsBitSet(flags, 2);
            bool stableGround = IsBitSet(flags, 3);
            bool snappingPrevented = IsBitSet(flags, 4);

            Player playerToMove;
            //check if player exists and if not create a new one
            if (playerDict.ContainsKey(playerID))
            {
                playerToMove = playerDict[playerID];
            }
            else
            {
                GameObject newplayerObject = Instantiate(playerPrefab, new Vector3(0, 10, 0), new Quaternion());
                playerToMove = newplayerObject.GetComponent<Player>();
                playerDict.Add(playerID, playerToMove);
                if (connectionManager.playerID == playerID)
                {
                    Camera.main.GetComponent<PlayerCamera>().SetFollowTransform(playerToMove.head);
                }
            }
            //set them with new data
            KinematicCharacterMotorState state = playerToMove.motor.GetState();
            state.Position = playerPos;
            state.Rotation = Quaternion.Euler(0, playerRotY, 0);
            state.BaseVelocity = vel;
            //state.MustUngroundTime = ungroundTime;
            state.MustUnground = mustUnground;
            state.LastMovementIterationFoundAnyGround = foundGround;
            state.GroundingStatus.FoundAnyGround = foundGround2;
            state.GroundingStatus.IsStableOnGround = stableGround;
            state.GroundingStatus.SnappingPrevented = snappingPrevented;

            if (connectionManager.playerID == playerID) //its us
            {
                playerToMove.serverPos.position = playerPos;
                inputList = inputList.Where(input => input.id > packetID).ToList(); //remove old inputs
                predictionList = predictionList.Where(input => input.Item1 >= packetID).ToList(); //remove old predictions
                if (predictionList.Count == 0 || packetID < TickConfig.BUFFER)
                    return;
                Vector3 offset = playerPos - predictionList[0].Item2;
                //Debug.Log($"SERVER {packetID} offset {offset} from {oldPos} to {playerPos}");
                if (offset.magnitude > 0.1f)
                {
                    //Debug.Log($"We were wrong! {predictionList[0].Item1}");
                    playerToMove.timeSinceJumpDown = timeSinceJump;
                    playerToMove.motor.ApplyState(state);

                    if (playerID == connectionManager.playerID)
                        playerToMove.SimulateInputsFrom(inputList);
                }
            }
            else
            {
                playerToMove.timeSinceJumpDown = timeSinceJump;
                playerToMove.motor.ApplyState(state);
            }
        }
    }
    public void RemovePlayer(uint id)
    {
        if (playerDict.ContainsKey(id))
        {
            Destroy(playerDict[id].transform.gameObject);
            playerDict.Remove(id);
        }
    }
    private bool IsBitSet(byte b, int pos)
    {
        return (b & (1 << pos)) != 0;
    }

}
