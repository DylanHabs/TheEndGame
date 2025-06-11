using UnityEngine;

public class GravityArea : MonoBehaviour
{
    public LayerMask playerLayer;
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("FOUUND something");
        if (collision.gameObject.layer == playerLayer.value)
        {
            Debug.Log("FOUUND PLAYER");
        }
    }
}
