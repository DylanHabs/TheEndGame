using KinematicCharacterController;
using UnityEngine;

public class PlayerBody : MonoBehaviour
{
    public float dampenSpeed = 8.0f;

    Animator animator;
    Transform followTrans;
    Player player;

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        followTrans = transform.parent;
        player = followTrans.GetComponent<Player>();
        transform.parent = transform.parent.parent;
    }
    private void Update()
    {
        //Handle interpolation
        Dampen();
        //Interp();
        //transform.position = followTrans.position;
        //transform.rotation = followTrans.rotation;

        Vector3 playerSpeed = player.motor.Velocity;
        playerSpeed.y = 0f;
        animator.SetFloat("Speed", playerSpeed.magnitude / Player.MAX_SPEED);
        animator.SetBool("Grounded", player.IsGrounded());
    }
    private void Dampen()
    {
        transform.rotation = followTrans.rotation;
        Vector3 delta = followTrans.position - transform.position;
        if (delta.magnitude > 4.0f)
        {
            transform.position = followTrans.position;
            Debug.Log("TELEPORT BODY");
        }
        else
        {
            float rate = Mathf.Max(dampenSpeed, delta.magnitude * dampenSpeed);
            transform.position += delta * rate * Time.fixedDeltaTime;
        }
    }
}
