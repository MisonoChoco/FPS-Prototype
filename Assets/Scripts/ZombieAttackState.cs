using UnityEngine;
using UnityEngine.AI;

public class ZombieAttackState : StateMachineBehaviour
{
    private Transform player;
    private NavMeshAgent agent;
    private ZombieAudio zombieAudio;

    public float stopAttackingDistance = 2.5f;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        agent = animator.GetComponent<NavMeshAgent>();
        zombieAudio = animator.GetComponent<ZombieAudio>();
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        zombieAudio?.PlayAttack();

        LookAtPlayer();

        float distanceFromPlayer = Vector3.Distance(player.position, animator.transform.position);
        if (distanceFromPlayer > stopAttackingDistance)
            animator.SetBool("isAttacking", false);
    }

    private void LookAtPlayer()
    {
        Vector3 direction = player.position - agent.transform.position;
        agent.transform.rotation = Quaternion.Euler(0,
            Quaternion.LookRotation(direction).eulerAngles.y, 0);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // nothing to stop — PlayOneShot handles itself
    }
}