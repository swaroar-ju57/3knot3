using UnityEngine;
using UnityEngine.AI;

namespace PatrolEnemy
{
    public class FollowState : IEnemyState
    {
        public void EnterState(EnemyController controller)
        {
            Debug.Log("Entered Follow State");
            controller.Agent.isStopped = false; // Ensure agent movement is enabled when entering follow
        }

        public void UpdateState(EnemyController controller)
        {
            if (controller.CurrentTarget == null)
            {
                controller.ChangeState(EnemyController.EnemyStateType.Idle);
                return;
            }

            float distanceToPlayer = Vector3.Distance(controller.transform.position, controller.CurrentTarget.position);

            // If player moved out of detection range, return to idle
            if (distanceToPlayer > controller.DetectionRange)
            {
                controller.ChangeState(EnemyController.EnemyStateType.Idle);
                return;
            }

            // If player is within attack range, halt movement and switch to attack state
            if (distanceToPlayer <= controller.AttackRange)
            {
                controller.Agent.isStopped = true; // Stop the agent's movement

                if (controller.HasLineOfSight)
                {
                    controller.ChangeState(EnemyController.EnemyStateType.Shoot);
                }
                else if (controller.CurrentGrenades > 0)
                {
                    controller.ChangeState(EnemyController.EnemyStateType.GrenadeThrow);
                }
                // If no line of sight and no grenades switch to shoot state

            else if (!controller.HasLineOfSight && controller.CurrentGrenades <= 0)
                {
                    controller.ChangeState(EnemyController.EnemyStateType.Shoot);
                }
            }
            // If not in attack range, continue following the player
            controller.Agent.SetDestination(controller.CurrentTarget.position);
            controller.Agent.isStopped = false; // Ensure agent moves if outside attack range
        }

        public void ExitState(EnemyController controller)
        {
            Debug.Log("Exited Follow State");
            controller.Agent.isStopped = false; // Ensure agent movement is enabled when exiting follow
        }
    }
}