using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ZombiePatrollingState : StateMachineBehaviour
{
    private float timer;
    private Transform player;
    private NavMeshAgent agent;
    private ZombieAudio zombieAudio;

    private List<Transform> waypointList = new List<Transform>();

    public float patrollingTime = 10f;
    public float detectionArea = 18f;
    public float patrolSpeed = 2f;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        agent = animator.GetComponent<NavMeshAgent>();
        zombieAudio = animator.GetComponent<ZombieAudio>();

        agent.speed = patrolSpeed;
        timer = 0;

        waypointList.Clear();
        GameObject waypointCluster = GameObject.FindGameObjectWithTag("Waypoints");
        if (waypointCluster != null)
            foreach (Transform t in waypointCluster.transform)
                waypointList.Add(t);

        if (waypointList.Count > 0)
            agent.SetDestination(waypointList[Random.Range(0, waypointList.Count)].position);

        zombieAudio?.RequestWalk();
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (agent.remainingDistance <= agent.stoppingDistance && waypointList.Count > 0)
            agent.SetDestination(waypointList[Random.Range(0, waypointList.Count)].position);

        timer += Time.deltaTime;
        if (timer > patrollingTime)
            animator.SetBool("isPatrolling", false);

        float dist = Vector3.Distance(player.position, animator.transform.position);
        if (dist < detectionArea)
            animator.SetBool("isChasing", true);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        agent.SetDestination(agent.transform.position);
        zombieAudio?.StopWalk();
    }
}