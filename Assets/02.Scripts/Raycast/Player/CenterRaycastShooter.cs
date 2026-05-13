using UnityEngine;
using UnityEngine.InputSystem;

public class CenterRaycastShooter : MonoBehaviour
{
    private enum AimDirectionMode
    {
        CameraCenter,
        CharacterForward
    }

    [Header("Raycast")]
    [SerializeField] private Camera m_cam;
    [SerializeField] private LayerMask m_hittableMask = ~0;
    [SerializeField] private float m_maxDistance = 100.0f;
    [SerializeField] private AimDirectionMode m_aimDirectionMode = AimDirectionMode.CharacterForward;
    [SerializeField] private float m_forwardRayOriginHeight = 1.2f;

    [Header("SphereCast")]
    [SerializeField] private float m_sphereRadius = 2.0f;
    [SerializeField] private float m_sphereMaxDistance = 10.0f;

    [Header("Debug")]
    [SerializeField] private bool m_drawDebugRay = true;
    [SerializeField] private bool m_drawGizmos = true;

    private PlayerInput _pi;
    private InputAction _fire;
    private Vector3 _lastRayOrigin;
    private Vector3 _lastRayDirection = Vector3.forward;
    private float _lastRayDistance;
    private bool _hasRaySample;
    private Vector3 _lastSphereOrigin;
    private Vector3 _lastSphereDirection = Vector3.forward;
    private float _lastSphereDistance;
    private bool _hasSphereSample;

    private void Awake()
    {
        _pi = GetComponent<PlayerInput>();
        if (_pi != null)
        {
            _fire = _pi.actions.FindAction("Fire", false);
        }

        if (m_cam == null)
        {
            m_cam = Camera.main;
        }
    }

    private void OnEnable()
    {
        if (_fire != null)
        {
            _fire.performed += OnRayFire;
        }
    }

    private void OnDisable()
    {
        if (_fire != null)
        {
            _fire.performed -= OnRayFire;
        }
    }


    private void OnRayFire(InputAction.CallbackContext _)
    {
        if (!TryBuildAimRay(out Ray ray))
        {
            return;
        }

        if (Physics.Raycast(ray, out RaycastHit hit, m_maxDistance, m_hittableMask, QueryTriggerInteraction.Ignore))
        {
            Debug.Log($"[CenterRaycastShooter] Hit {hit.collider.name} at {hit.point}");
            if (m_drawDebugRay)
            {
                Debug.DrawLine(ray.origin, hit.point, Color.green, 1.0f);
            }
            _lastRayDistance = hit.distance;
        }
        else
        {
            if (m_drawDebugRay)
            {
                Debug.DrawLine(ray.origin, ray.origin + ray.direction * m_maxDistance, Color.yellow, 0.5f);
            }
            _lastRayDistance = m_maxDistance;
        }

        _lastRayOrigin = ray.origin;
        _lastRayDirection = ray.direction;
        _hasRaySample = true;
        SphereCastExample();
    }


    void SphereCastExample()
    {
        if (!TryBuildAimRay(out Ray ray))
        {
            return;
        }

        if (Physics.SphereCast(ray.origin, m_sphereRadius, ray.direction, out RaycastHit hit, m_sphereMaxDistance, m_hittableMask, QueryTriggerInteraction.Ignore))
        {
            Debug.Log($"Sphere Hit {hit.collider.name}");
            _lastSphereDistance = hit.distance;
        }
        else
        {
            _lastSphereDistance = m_sphereMaxDistance;
        }

        _lastSphereOrigin = ray.origin;
        _lastSphereDirection = ray.direction;
        _hasSphereSample = true;
    }

    private bool TryBuildAimRay(out Ray ray)
    {
        ray = default;
        if (m_aimDirectionMode == AimDirectionMode.CameraCenter)
        {
            if (m_cam == null)
            {
                m_cam = Camera.main;
                if (m_cam == null)
                {
                    return false;
                }
            }

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            ray = m_cam.ScreenPointToRay(screenCenter);
            return true;
        }

        Vector3 forwardOrigin = transform.position + Vector3.up * m_forwardRayOriginHeight;
        ray = new Ray(forwardOrigin, transform.forward);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!m_drawGizmos)
        {
            return;
        }

        if (_hasRaySample)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_lastRayOrigin, _lastRayOrigin + _lastRayDirection * _lastRayDistance);
        }

        if (_hasSphereSample)
        {
            Vector3 endPoint = _lastSphereOrigin + _lastSphereDirection * _lastSphereDistance;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_lastSphereOrigin, m_sphereRadius);
            Gizmos.DrawWireSphere(endPoint, m_sphereRadius);
            Gizmos.DrawLine(_lastSphereOrigin, endPoint);
        }
    }


    void OverlapExample(Vector3 centerPostion)
    {
        Vector3 center = centerPostion;
        float radius = 5.0f;

        Collider[] hitColliders = Physics.OverlapSphere(center, radius);

        foreach (var hitCollider in hitColliders)
        {
            Debug.Log($"Detected : {hitCollider.name}");
        }
    }

    private Collider[] results = new Collider[10];

    void OptimizedOverlap()
    {
        //Allocation (할당--> 메모리 할당)
        //Alloc --> 줄임단어 -> 표준어   

        int count = Physics.OverlapSphereNonAlloc(transform.position, 5.0f, results);

        for (int i = 0; i < count; i++)
        {
            Debug.Log($"NonAlloc Hit : {results[i].name}");
        }


    }


}
