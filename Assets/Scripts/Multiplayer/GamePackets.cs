using System.Runtime.InteropServices;
using System;
using UnityEngine;
using System.Collections.Generic;

public static class GamePackets
{
    //Client packets (all odd)
    public const byte INPUT_ID = 1;

    //server packets (all even)
    public const byte HELLO_ID = 2;
    public const byte MOV_MANAGER_ID = 4;
    public const byte EVENT_MANAGER_ID = 6;


    //RPC IDs 
    public const byte RPC_PLAYER_DISCONNECT = 1;

    public struct MoveInput
    {
        public byte moveByte;
        public uint playerID;
        public float camY;
        public uint packetID;
        public MoveInput (byte _moveByte, uint _playerID, float _camY, uint _packetID)
        {
            moveByte = _moveByte;
            playerID = _playerID;
            camY = _camY;
            packetID = _packetID;
        }
    }
}

public static class TickConfig
{
    //Testing params
    public const bool SIMULATE_LAG = true;
    public const int DELAY = 250;
    public const int JITTER = DELAY / 3;
    public const int DROP = 5;

    public const float SERVER_TICK_RATE = 50;
    public const float SERVER_TICK_TIME = 1 / SERVER_TICK_RATE;
    public const int BUFFER = 2; //how many ticks to buffer

    public const float CLIENT_TICK_RATE = 50;
    public const float CLIENT_TICK_TIME = 1 / CLIENT_TICK_RATE;
    public const int INPUT_MAX = 25;
}
