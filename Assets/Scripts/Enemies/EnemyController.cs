using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    public float lookRadius = 10f;

    public int damage = 5;
    public float attackSpeed = 1f;
    
    private float attackCooldown = 0f;

    Transform target;
    NavMeshAgent agent;

    void Start()
    {
        target = PlayerManager.instance.player.transform;
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        attackCooldown -= Time.deltaTime;

        TargetPlayer();
    }

    void TargetPlayer()
    {
        float distance = Vector3.Distance(target.position, transform.position);

        if (distance <= lookRadius)
        {
            agent.SetDestination(target.position);

            if (distance <= agent.stoppingDistance)
            {
                PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();

                if (playerHealth != null)
                {
                    if (attackCooldown <= 0)
                    {
                        Debug.Log("Attacking player: " + playerHealth.GetHealth());

                        playerHealth.TakeDamage(damage);

                        attackCooldown = 1f / attackSpeed;
                    }
                }

                FaceTarget();
            }
        }
    }

    void FaceTarget()
    {
        Vector3 direction = (target.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, lookRadius);
    }
}
