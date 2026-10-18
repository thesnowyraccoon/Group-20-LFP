
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    public int attackDamage = 10;
    public float attackSpeed = 1f;

    private float attackCooldown = 0f;

    BoxCollider attackCollider;
    MeshRenderer attackRenderer;

    void Awake()
    {
        attackCollider = GetComponent<BoxCollider>();
        attackRenderer = GetComponent<MeshRenderer>();

        if (attackCollider != null)
            attackCollider.enabled = false;

        // The attack hitbox can work without a visible mesh.
        if (attackRenderer != null)
            attackRenderer.enabled = false;
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
        if (attackCollider == null) return;
        if (attackSpeed <= 0f) return;

        if (attackCooldown <= 0f)
        {
            attackCollider.enabled = true;

            if (attackRenderer != null)
                attackRenderer.enabled = true;

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

        if (attackCollider != null)
            attackCollider.enabled = false;

        if (attackRenderer != null)
            attackRenderer.enabled = false;
    }
}
