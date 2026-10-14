using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    public int attackDamage = 10;
    public float attackSpeed = 1f;
    
    private float attackCooldown = 0f;
    
    // bool canAttack = true;
    
    BoxCollider attackCollider;
    MeshRenderer renderer;

    void Awake()
    {
        attackCollider = GetComponent<BoxCollider>();
        attackCollider.enabled = false;

        renderer = GetComponent<MeshRenderer>();
        renderer.enabled = false;
    }

    void Update()
    {
        attackCooldown -= Time.deltaTime;
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
        // if (!canAttack) return;

        // attackCollider.enabled = true;
        // renderer.enabled = true;

        // StartCoroutine(AttackCooldown());

        if (attackCooldown <= 0f)
        {
            attackCollider.enabled = true;
            renderer.enabled = true;

            StartCoroutine(AttackCooldown());

            attackCooldown = 1f / attackSpeed;
        }
    }

    private void OnTriggerEnter(Collider trigger)
    {
        if (trigger == null) return;
        
        if (trigger.gameObject.CompareTag("Enemy"))
        {
            DealDamage(trigger.gameObject);
        }
    }

    private IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(0.5f);

        attackCollider.enabled = false;
        renderer.enabled = false;
    }
}
