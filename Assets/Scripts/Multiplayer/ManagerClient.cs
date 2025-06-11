using KinematicCharacterController;
using UnityEngine;

public class ManagerClient : MonoBehaviour
{
    MovementManagerClient movementManager;
    ConnectionManagerClient connectionManager;
    private void Start()
    {
        movementManager = GetComponent<MovementManagerClient>();
        connectionManager = GetComponent<ConnectionManagerClient>();
        KinematicCharacterSystem.EnsureCreation();
    }
    public void Tick()
    {
        connectionManager.Tick();
        movementManager.Tick();
        //Vector3 oldPos = movementManager.playerDict[connectionManager.playerID].transform.position;
        KinematicCharacterSystem.GetInstance().Tick(TickConfig.CLIENT_TICK_TIME);
        if ((int)movementManager.tickCounter - 1 >= TickConfig.BUFFER && movementManager.playerDict.ContainsKey(connectionManager.playerID))
            movementManager.predictionList.Add((movementManager.tickCounter - 1, movementManager.playerDict[connectionManager.playerID].transform.position));
        //Vector3 offset = movementManager.playerDict[connectionManager.playerID].transform.position - oldPos;
        //Debug.Log($"CLIENT {movementManager.tickCounter - 1} offset {offset} from {oldPos} to {movementManager.playerDict[connectionManager.playerID].transform.position}");
    }

    private float tickTimer = 0f;
    private void Update()
    {
        tickTimer += Time.deltaTime;
        while (tickTimer > TickConfig.CLIENT_TICK_TIME)
        {
            tickTimer -= TickConfig.CLIENT_TICK_TIME;
            Tick();
        }
    }
}
