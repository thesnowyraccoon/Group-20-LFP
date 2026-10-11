using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    public int attackDamage = 10;
    public float attackSpeed = 1f;
    public float attackCooldown = 1f;

    BoxCollider attackCollider;
    bool canAttack = true;

    void Awake()
    {
        attackCollider = GetComponent<BoxCollider>();
        attackCollider.enabled = false;
    }

    public void SetDamage(int amount)
    {
        attackDamage = amount;
    }

    public void SetSpeed(int amount)
    {
        attackSpeed = amount;
    }

    public void DealDamage(GameObject target)
    {
        if (target.TryGetComponent<EnemyHealth>(out var enemy))
        {
            enemy.TakeDamage(attackDamage);
            Debug.Log("Enemy Attacked!");
        }
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!context.started) return;
        if (attackCollider.gameObject == null) return;
        if (!canAttack) return;

        attackCollider.enabled = true;

        StartCoroutine(AttackCooldown());
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision == null) return;
        
        if (collision.gameObject.CompareTag("Enemy"))
        {
            DealDamage(collision.gameObject);
        }
    }

    private IEnumerator AttackCooldown()
    {
        canAttack = false;

        yield return new WaitForSeconds(attackSpeed);

        attackCollider.enabled = false;

        yield return new WaitForSeconds(attackCooldown);

        canAttack = true;
    }
}
