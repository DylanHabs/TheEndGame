using KinematicCharacterController;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Player : MonoBehaviour, ICharacterController
{
    public struct PlayerInput
    {
        public Vector2 moveValue;
        public bool jump;
        public float camY;
        public uint id;

        public PlayerInput(Vector2 _moveValue, bool _jump, float _camY, uint _id)
        {
            moveValue = _moveValue;
            jump = _jump;
            camY = _camY;
            id = _id;
        }
    }
    public const float MAX_SPEED = 4.5f;
    public const float ACCEL_GROUND = 45f;
    public const float FRICTION = 5f;
    public const float JUMP_SPEED = 18f;
    public const float MAX_JUMPTIME = 2.0f;
    public const float FALLSPEED = 18f;
    public const float ROTSPEED = 7f;

    public Transform serverPos;
    public Transform head;
    public KinematicCharacterMotor motor;

    //Input state vars
    public float camY;
    public Vector3 moveValue = Vector3.zero;
    public byte timeSinceJumpDown = 0;
    private bool jumpRequest = false;

    void Awake()
    {
        motor = GetComponent<KinematicCharacterMotor>();
        motor.CharacterController = this;
    }
    void Start()
    {
        serverPos.parent = transform.parent;
        KinematicCharacterSystem.Settings.AutoSimulation = false;
        KinematicCharacterSystem.Settings.Interpolate = false;
    }
    public void SimulateInputsFrom(List<Player.PlayerInput> inputList)
    {
        Vector3 oldPos = transform.position;
        Debug.Log($"Input list client is {inputList.Count} long");
        //Loop through all inputs
        for (int i = 0; i < inputList.Count; i++)
        {
            //Resimulate input
            MovePlayer(inputList[i].moveValue, inputList[i].jump, inputList[i].camY);
            //KinematicCharacterSystem.GetInstance().Tick(TickConfig.CLIENT_TICK_TIME);
            KinematicCharacterSystem.Simulate(TickConfig.CLIENT_TICK_TIME, new List<KinematicCharacterMotor>{motor}, new List<PhysicsMover>());
        }
        float dif = (oldPos - transform.position).magnitude;
        if (dif > 0.1f)
            Debug.Log($"Got dif of {dif}");
    }
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        currentRotation = RotateToDir(currentRotation, deltaTime);
    }
    private Quaternion RotateToDir(Quaternion currentRot, float deltaTime)
    {
        head.rotation = Quaternion.Euler(0, camY, 0);
        Vector3 wishDir = Vector3.zero;
        wishDir += Mathf.Round(moveValue.y) * head.forward;
        wishDir += Mathf.Round(moveValue.x) * head.right;
        wishDir.Normalize();
        if (wishDir != Vector3.zero)
        {
            Vector3 dir = Vector3.RotateTowards(motor.CharacterForward, wishDir, ROTSPEED * deltaTime, 0f);
            currentRot = Quaternion.LookRotation(dir, motor.CharacterUp);
        }

        return currentRot;
    }
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        head.rotation = Quaternion.Euler(0, camY, 0);
        Vector3 wishDir = Vector3.zero;
        wishDir += Mathf.Round(moveValue.y) * head.forward;
        wishDir += Mathf.Round(moveValue.x) * head.right;
        wishDir.Normalize();

        float velMagnitude = currentVelocity.magnitude;

        if (motor.GroundingStatus.IsStableOnGround)
        {
            Vector3 effectiveGroundNormal = motor.GroundingStatus.GroundNormal;
            //Reorient velocity on slope
            currentVelocity = motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * velMagnitude;
            Vector3 inputRight = Vector3.Cross(wishDir, motor.CharacterUp);
            Vector3 reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized;// * wishDir.magnitude;

            if (timeSinceJumpDown == 0)
                currentVelocity += Accelerate(currentVelocity, reorientedInput, ACCEL_GROUND, MAX_SPEED, deltaTime);
            float speed = currentVelocity.magnitude;
            if (speed != 0.0f)
            {
                float drop = speed * FRICTION * deltaTime;
                currentVelocity *= MathF.Max(speed - drop, 0) / speed;
            }
            TryJump(ref currentVelocity, deltaTime);
        }
        else
        {
            currentVelocity.y -= FALLSPEED * deltaTime;
        }
    }
    private void TryJump(ref Vector3 currentVelocity, float deltaTime)
    {
        if (jumpRequest)
        {
            timeSinceJumpDown += (byte)(MathF.Min(deltaTime / MAX_JUMPTIME * 255f, 255f));
        }
        float jumpAlpha = timeSinceJumpDown / 255f;
        if (jumpAlpha >= 1 || (!jumpRequest && jumpAlpha > 0))
        {
            timeSinceJumpDown = 0;

            jumpAlpha = Math.Min(0.4f + jumpAlpha, 1.0f);
            jumpAlpha = Mathf.Round(jumpAlpha * 10) / 10;

            Vector3 jumpDirection = motor.CharacterUp;
            if (moveValue.magnitude > 0)
                jumpDirection += motor.CharacterForward;
            //jumpDirection.Normalize();
            motor.ForceUnground();
            currentVelocity += (jumpDirection * JUMP_SPEED * jumpAlpha) - Vector3.Project(currentVelocity, motor.CharacterUp);
        }
        if (!jumpRequest)
            timeSinceJumpDown = 0;
    }
    private Vector3 Accelerate(Vector3 prevVel, Vector3 dir, float accelScale, float maxSpeed, float delta)
    {
        float proj_velocity = Vector3.Dot(prevVel, dir);
        float accel_vel = accelScale * delta;
        if (proj_velocity + accel_vel > maxSpeed)
            accel_vel = maxSpeed - proj_velocity;
        return dir * accel_vel;
    }
    public void MovePlayer(Vector2 _moveValue, bool jump, float _camY)
    {
        moveValue = _moveValue;
        jumpRequest = jump;
        camY = _camY;
    }
    public bool IsGrounded()
    {
        if (motor == null)
            return false;
        else
            return motor.GroundingStatus.IsStableOnGround;
    }
    //LESS USEFUL METHODS (NON CORE) \/ \/ \/
    public void AfterCharacterUpdate(float deltaTime)
    {
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        return true;
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void PostGroundingUpdate(float deltaTime)
    {
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
    }
}
