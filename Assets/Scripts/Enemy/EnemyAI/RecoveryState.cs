using UnityEngine;
using UnityEngine.AI;

namespace PatrolEnemy
{
    public class RecoveryState : IEnemyState
    {
        private bool isReturning ;
        private float lastHealthRecoveryTime = 0f;
        private readonly float healthRecoveryInterval = 0.5f;
      
        public void EnterState(EnemyController controller)
        {
            Debug.Log("Entered Recovery State");
            isReturning = false;
            controller.Agent.isStopped = false;
            controller.Agent.ResetPath();
     
        }

       public void UpdateState(EnemyController controller)
{
    if (!isReturning)
    {
        // Set the destination to the initial position
        controller.Agent.SetDestination(controller.InitialPosition);
        isReturning = true;
        controller.IsRecoverReturing = true;
        Debug.Log($"Returning to initial position: {controller.InitialPosition} for recovery.");
        lastHealthRecoveryTime = Time.time; // Initialize recovery timer
    }
    else if (controller.Agent.remainingDistance <= controller.Agent.stoppingDistance && !controller.Agent.pathPending)
    {
        controller.Agent.isStopped = true;
        controller.IsRecoverReturing = false; // Stop at the initial position
        Debug.Log("Reached initial position. Starting recovery.");

        // Recover health over time
        if (Time.time >= lastHealthRecoveryTime + healthRecoveryInterval)
        {
            controller._health.Heal(controller.RecoveryRate*Time.deltaTime);

            // If health is fully recovered, go back to Idle state
            if (controller._health.CurrentHealth >= controller._health.MaxHealth)
            {
                controller.ChangeState(EnemyController.EnemyStateType.Idle);
            }
        }
    }
    else if (isReturning && !controller.Agent.hasPath)
    {
        // Path failed, might need to handle this (e.g., try again, or just recover in place)
        Debug.LogWarning("Path to initial position failed.");
        controller.Agent.isStopped = true;
        controller.IsRecoverReturing = false;
        // Still attempt to recover in place
        if (Time.time >= lastHealthRecoveryTime + healthRecoveryInterval)
        {
            controller._health.Heal(controller.RecoveryRate*Time.deltaTime);
            controller.IsRecoverReturing = false;
            Debug.Log($"Recovering in place (path failed). Current health: {controller._health.CurrentHealth}");
            if (controller._health.CurrentHealth >= controller._health.MaxHealth)
            {
                controller.ChangeState(EnemyController.EnemyStateType.Idle);
            }
        }
    }
}
        public void ExitState(EnemyController controller)
        {
            Debug.Log("Exited Recovery State");
            controller.Agent.isStopped = false;
        }
    }
}