using UnityEngine;
using HealthSystem;
using SingletonManagers;
using Unity.Mathematics;
using Random = UnityEngine.Random;

namespace MachineGunner
{
    public class MachineGunnerController : MonoBehaviour
    {
        #region Public Variables

        [Header("Ranges")]
        [SerializeField] private float alertRange;

        [SerializeField]private float SuppressiveRange = 15f;
        [SerializeField]private float _shootRange = 7f;

        private Health _health;

        [Header("Shooting Configuration")]
        [SerializeField]private GameObject bulletPrefab;
        [SerializeField] private Transform _firePoint;
        [SerializeField]private float fireRate = 0.1f;
        [SerializeField]private float burstDuration = 2f; // Duration of a single burst in ShootState
        [SerializeField]private float suppressiveBurstDuration = 3f;
        [SerializeField]private float overheatThreshold = 10f;
        [SerializeField]private float coolingRate = 2f;
        [SerializeField]private float reloadTime = 3f;
        [SerializeField]private int magazineSize = 30;
        [Range(0f, 90f)]
        [SerializeField]private float suppressiveFireSpreadAngle = 15f; // Angle for bullet spread in suppressive fire


        [Header("Detection")]
        public string playerTag { get; private set; } = "Player";
        public LayerMask lineOfSightMask{ get; private set; }

        [Header("Gizmo Colors")] 
        private readonly Color alertGizmoColor = Color.yellow;
        private Color suppressiveGizmoColor = Color.blue;
        private Color shootGizmoColor = Color.red;
        private Color lineOfSightColor = Color.green;
        private Color noLineOfSightColor = Color.magenta;
        private Color suppressiveArcColor = new Color(1f, 0.5f, 0f, 0.5f); // Orange with alpha

        [Header("Idle Rotation")]
        public float idleRotationSpeed { get; private set; }= 10f;
        [Range(0f, 180f)]
        [SerializeField]private float idleRotationAngle = 60f; // Total angle of rotation
        [SerializeField]private float idleRotationOffset = 0f; // Starting offset for the rotation

        [Header("Shoot State")]
        [Range(0f, 90f)]
        [SerializeField]private float burstSweepAngle = 30f; // Total sweep angle during burst
        [SerializeField]private float burstSweepSpeed = 60f; // Degrees per second of sweep

        #endregion

        #region Private Variables

        private IMachineGunnerState _currentState;
        private GameObject _player;
        private float _timeSinceLastShot = 0f;
        private float _currentHeat = 0f;
        private bool _isOverheated = false;
        private float _reloadStartTime;
        private int _currentAmmo;

        private Vector3 _lastKnownPlayerPosition;

        #endregion

        #region Properties

        public IMachineGunnerState CurrentState => _currentState;
        public GameObject Player => _player;
        public float TimeSinceLastShot => _timeSinceLastShot;
        public float CurrentHeat => _currentHeat;
        public bool IsOverheated => _isOverheated;
        public int CurrentAmmo => _currentAmmo;
        public float SuppressiveFireSpreadAngle => suppressiveFireSpreadAngle;
        public Transform firePoint=>_firePoint;
        public float BurstDuration =>  burstDuration ;
        public float BurstSweepAngle => burstSweepAngle;
        public float BurstSweepSpeed => burstSweepSpeed;
        public float SuppressiveBurstDuration => suppressiveBurstDuration;
        public float IdleRotationAngle => idleRotationAngle;

        public Color AlertGizmoColor => alertGizmoColor;

        public float AlertRange
        {
            get => alertRange;
            set => alertRange = value;
        }

        #endregion

        #region Unity Callbacks


        void Awake()
        {
            _health = GetComponent<Health>();
            _player = GameObject.FindGameObjectWithTag(playerTag);
            _currentState = new IdleState();
            _currentState.EnterState(this);
            _currentAmmo = magazineSize;
            _lastKnownPlayerPosition = transform.forward; // Default forward if no player yet
            idleRotationOffset = transform.eulerAngles.y; // Initialize offset with current Y rotation
        }

        void Update()
        {
            if (_player != null)
            {
                _lastKnownPlayerPosition = _player.transform.position;
            }

            _currentState?.UpdateState(this);
            _timeSinceLastShot += Time.deltaTime;

            DeathHandler();

        }

        void OnDrawGizmos()
        {
            Gizmos.color = AlertGizmoColor;
            Gizmos.DrawWireSphere(transform.position, AlertRange);

            Gizmos.color = suppressiveGizmoColor;
            Gizmos.DrawWireSphere(transform.position, SuppressiveRange);

            Gizmos.color = shootGizmoColor;
            Gizmos.DrawWireSphere(transform.position, _shootRange);

            // Line of Sight Gizmo
            Gizmos.color = HasLineOfSightToPlayer() ? lineOfSightColor : noLineOfSightColor;
            Gizmos.DrawLine(firePoint.position, _lastKnownPlayerPosition);

            // Suppressive Fire Arc Gizmo
            Gizmos.color = suppressiveArcColor;
            Vector3 forwardDirection = transform.forward;
            Quaternion leftRayRotation = Quaternion.AngleAxis(-suppressiveFireSpreadAngle / 2f, Vector3.up);
            Quaternion rightRayRotation = Quaternion.AngleAxis(suppressiveFireSpreadAngle / 2f, Vector3.up);
            Vector3 leftRayDirection = leftRayRotation * forwardDirection;
            Vector3 rightRayDirection = rightRayRotation * forwardDirection;

            Gizmos.DrawRay(firePoint.position, leftRayDirection * SuppressiveRange);
            Gizmos.DrawRay(firePoint.position, rightRayDirection * SuppressiveRange);
            Gizmos.DrawWireSphere(transform.position + forwardDirection * SuppressiveRange, 0.5f); // Indicate end of range

            // Idle Rotation Gizmo (visualize the angle)
            Gizmos.color = Color.cyan;
            Quaternion initialRotation = Quaternion.Euler(0f, idleRotationOffset - idleRotationAngle / 2f, 0f);
            Quaternion finalRotation = Quaternion.Euler(0f, idleRotationOffset + idleRotationAngle / 2f, 0f);
            Vector3 initialDirection = initialRotation * Vector3.forward * 2f;
            Vector3 finalDirection = finalRotation * Vector3.forward * 2f;
            Gizmos.DrawRay(transform.position, initialDirection);
            Gizmos.DrawRay(transform.position, finalDirection);

            // Burst Sweep Gizmo
            Gizmos.color = Color.yellow;
            Vector3 currentForward = transform.forward;
            Quaternion sweepLeft = Quaternion.AngleAxis(-burstSweepAngle / 2f, Vector3.up);
            Quaternion sweepRight = Quaternion.AngleAxis(burstSweepAngle / 2f, Vector3.up);
            Gizmos.DrawRay(firePoint.position, sweepLeft * currentForward * _shootRange * 0.75f); // Slightly shorter to visualize
            Gizmos.DrawRay(firePoint.position, sweepRight * currentForward * _shootRange * 0.75f);
        }

        #endregion

        #region State Management

        public void SwitchState(IMachineGunnerState newState)
        {
            _currentState?.ExitState(this);
            _currentState = newState;
            _currentState.EnterState(this);
        }

        private void DeathHandler(){

            if(_health.CurrentHealth <= 0)
            {

                SwitchState(new DeathState());
            }

        }

        #endregion

        #region Detection Methods

        public bool IsPlayerInAlertRange()
        {
            return Vector3.Distance(transform.position, _player.transform.position) <= AlertRange;
        }

        public bool IsPlayerInSuppressiveRange()
        {
            return Vector3.Distance(transform.position, _player.transform.position) <= SuppressiveRange;
        }

        public bool IsPlayerInShootRange()
        {
            return Vector3.Distance(transform.position, _player.transform.position) <= _shootRange;
        }

        public bool HasLineOfSightToPlayer()
        {
            if (_player == null) return false;
            Vector3 directionToPlayer = _player.transform.position - transform.position;
            RaycastHit hit;
            if (Physics.Raycast(firePoint.position, directionToPlayer.normalized, out hit, Mathf.Infinity, lineOfSightMask))
            {
                return hit.collider.CompareTag(playerTag);
            }
            return false;
        }

        #endregion

       public void ShootBullet(Vector3 targetPosition, bool isSuppressive = false)
{
    if (_currentAmmo > 0 && !_isOverheated && _timeSinceLastShot >= fireRate)
    {
        // Flatten the target position to be level with the fire point
        targetPosition.y = firePoint.position.y;

        // Calculate horizontal direction
        Vector3 direction = (targetPosition - firePoint.position).normalized;

        if (isSuppressive)
        {
            float spreadAngle = suppressiveFireSpreadAngle;

            // Apply horizontal (XZ) spread only
            direction = Quaternion.Euler(
                0f,
                Random.Range(-spreadAngle, spreadAngle),
                0f
            ) * direction;
        }

        // Create horizontal rotation
        Quaternion bulletRotation = Quaternion.LookRotation(direction, Vector3.up);

        // Instantiate bullet
        Instantiate(bulletPrefab, firePoint.position, bulletRotation);
        AudioManager.PlaySound(SoundKeys.GunShot);
        ParticleManager.Instance.PlayParticle("Gunshot", firePoint.position, quaternion.identity);

                // Ammo, heat, and overheat logic
                _timeSinceLastShot = 0f;
        _currentAmmo--;
        _currentHeat += 1f;

        if (_currentHeat >= overheatThreshold)
        {
            _isOverheated = true;
            SwitchState(new OverheatAndReloadState());
        }
    }
}



        public void StartCoolingAndReloading()
        {
            _isOverheated = true;
            _reloadStartTime = Time.time;
            _currentAmmo = 0; // Immediately set ammo to 0 when overheating
        }

        public void UpdateCoolingAndReloading()
        {
            if (_isOverheated)
            {
                _currentHeat -= coolingRate * Time.deltaTime;
                _currentHeat = Mathf.Max(0f, _currentHeat); // Ensure heat doesn't go below zero

                if (Time.time >= _reloadStartTime + reloadTime)
                {
                    _currentAmmo = magazineSize;
                }

                if (_currentHeat <= 0f && _currentAmmo == magazineSize)
                {
                    _isOverheated = false;
                    // Decide which state to go back to based on player's position
                    if (IsPlayerInShootRange())
                    {
                        SwitchState(new ShootState());
                    }
                    else if (IsPlayerInSuppressiveRange())
                    {
                        SwitchState(new SuppressState());
                    }
                    else if (IsPlayerInAlertRange())
                    {
                        SwitchState(new AlertState());
                    }
                    else
                    {
                        SwitchState(new IdleState());
                    }
                }
            }
        }
        
    }
}