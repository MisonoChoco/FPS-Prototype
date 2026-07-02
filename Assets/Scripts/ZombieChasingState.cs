using UnityEngine;
using UnityEngine.AI;

public class ZombieChasingState : StateMachineBehaviour
{
    private NavMeshAgent agent;
    private Transform player;
    private ZombieAudio zombieAudio;

    public float chaseSpeed = 6f;
    public float stopChasingDistance = 21f;
    public float attackingDistance = 2.5f;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        agent = animator.GetComponent<NavMeshAgent>();
        zombieAudio = animator.GetComponent<ZombieAudio>();

        agent.speed = chaseSpeed;
        zombieAudio?.RequestChase();
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        agent.SetDestination(player.position);
        animator.transform.LookAt(player);

        float dist = Vector3.Distance(player.position, animator.transform.position);

        if (dist > stopChasingDistance)
            animator.SetBool("isChasing", false);

        if (dist < attackingDistance)
            animator.SetBool("isAttacking", true);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        agent.SetDestination(animator.transform.position);
        zombieAudio?.StopChase();
    }
}