using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    [SerializeField] private int HP = 100;
    [SerializeField] private int Armor = 75;
    public bool isDead;
    private Animator animator;
    private NavMeshAgent NavAgent;
    private ZombieAudio zombieAudio;

    public struct HitOutcome
    {
        public bool WasArmorHit;   // hit armor, armor survived
        public bool ArmorBroke;    // this hit brought armor to 0
        public bool WasKill;       // this hit killed the enemy
    }

    private void Start()
    {
        animator = GetComponent<Animator>();
        NavAgent = GetComponent<NavMeshAgent>();
        zombieAudio = GetComponent<ZombieAudio>();
    }

    public HitOutcome TakeDamage(int dmg)
    {
        var outcome = new HitOutcome();
        if (isDead) return outcome;

        if (Armor > 0)
        {
            Armor -= dmg;
            if (Armor <= 0)
            {
                int overflow = -Armor;
                Armor = 0;
                HP -= overflow;       // leftover damage bleeds into HP this same hit
                outcome.ArmorBroke = true;
            }
            else
            {
                outcome.WasArmorHit = true;
            }
        }
        else
        {
            HP -= dmg;
        }

        if (HP <= 0)
        {
            isDead = true;
            outcome.WasKill = true;
            animator.SetTrigger(Random.Range(0, 2) == 0 ? "DieBack" : "DieForward");
            zombieAudio?.PlayDeath();
            SoundManager.Instance?.PlayKillFeedback();
            KillFeedManager.Instance?.AddKill(gameObject.name);
        }
        else
        {
            animator.SetTrigger("DAMAGE");
            zombieAudio?.PlayHurt();
        }

        return outcome;
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