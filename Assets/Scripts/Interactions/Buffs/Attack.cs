using UnityEngine;

public class Attack : MonoBehaviour, IInteractable
{
    PlayerAttack player;

    public int damage = 10;
    
    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponentInChildren<PlayerAttack>();
    }

    public void Interact(Collider col)
    {
        if (player != null)
        {
            Debug.Log("Picked up Attack");
            
            player.SetDamage(damage);

            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("Player not found");
        }
    }
}
