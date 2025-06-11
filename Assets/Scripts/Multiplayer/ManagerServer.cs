using KinematicCharacterController;
using UnityEngine;

public class ManagerServer : MonoBehaviour
{
    ConnectionManagerServer connectionManager;
    MovementManagerServer movementManager;
    EventManagerServer eventManager;
    void Start()
    {
        connectionManager = GetComponent<ConnectionManagerServer>();
        movementManager = GetComponent<MovementManagerServer>();
        eventManager = GetComponent<EventManagerServer>();
        KinematicCharacterSystem.EnsureCreation();
    }
    public void Tick()
    {
        //connectionManager.Tick();
        eventManager.Tick();
        movementManager.Tick();
    }

    private float tickTimer = 0f;
    private void Update()
    {
        tickTimer += Time.deltaTime;
        while (tickTimer > TickConfig.SERVER_TICK_TIME)
        {
            tickTimer -= TickConfig.SERVER_TICK_TIME;
            Tick();
        }
    }
}
