using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    [SerializeField] private int HP = 100;
    private Animator animator;
    private NavMeshAgent NavAgent;
    private ZombieAudio zombieAudio;
    public bool isDead;

    private void Start()
    {
        animator = GetComponent<Animator>();
        NavAgent = GetComponent<NavMeshAgent>();
        zombieAudio = GetComponent<ZombieAudio>();
    }

    public void TakeDamage(int dmg)
    {
        HP -= dmg;

        if (HP <= 0)
        {
            isDead = true;
            animator.SetTrigger(Random.Range(0, 2) == 0 ? "DieBack" : "DieForward");
            zombieAudio?.PlayDeath();
            SoundManager.Instance?.PlayKillFeedback();
        }
        else
        {
            animator.SetTrigger("DAMAGE");
            zombieAudio?.PlayHurt();
        }
    }

    // Call these from your AI state machine
    public void OnStartChasing() => zombieAudio?.RequestChase();

    public void OnStopChasing() => zombieAudio?.StopChase();

    public void OnAttack() => zombieAudio?.PlayAttack();

    public void OnStartWalking() => zombieAudio?.RequestWalk();

    public void OnStopWalking() => zombieAudio?.StopWalk();

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 2.5f);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, 15f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 18f);
    }
}