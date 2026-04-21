using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Manages ragdoll physics on an enemy character.
/// Attach to the same GameObject as EnemyController.
/// Assign hipBone in the Inspector after running the Unity Ragdoll Wizard.
/// </summary>
public class RagdollController : MonoBehaviour
{
    [Header("Ragdoll")]
    [Tooltip("The hip/pelvis bone — receives the directional impulse on death.")]
    public Transform hipBone;
    [Tooltip("Force applied to the hip bone when ragdoll activates.")]
    public float explosionForce = 300f;
    [Tooltip("Fraction of the impulse redirected upward for a natural tumble.")]
    public float upwardModifier = 0.5f;
    [Tooltip("Seconds after which ragdoll bones are frozen in place.")]
    public float freezeDelay = 3f;
    [Tooltip("How long the ragdoll must stay near-still before freezing.")]
    public float settleDuration = 1.25f;
    [Tooltip("Velocity threshold used to detect settled ragdolls.")]
    public float settleVelocityThreshold = 0.08f;
    [Tooltip("Safety timeout if ragdoll never settles.")]
    public float maxFreezeWait = 12f;

    private Rigidbody[]  _boneRigidbodies;
    private Collider[]   _boneColliders;
    private Rigidbody    _rootRigidbody;
    private Collider     _rootCollider;
    private Animator     _animator;
    private NavMeshAgent _agent;
    private Coroutine    _freezeCoroutine;
    private PhysicsMaterial _ragdollFrictionMaterial;

    private void Awake()
    {
        _rootRigidbody = GetComponent<Rigidbody>();
        _rootCollider  = GetComponent<Collider>();
        _animator      = GetComponentInChildren<Animator>();
        _agent         = GetComponent<NavMeshAgent>();
        CacheBoneComponents();
        DisableRagdoll();
    }

    /// <summary>
    /// Switches the character to full ragdoll physics.
    /// </summary>
    /// <param name="hitDirection">World-space direction the hit came from (toward enemy).</param>
    /// <param name="forceMagnitude">Override impulse magnitude; pass ≤0 to use explosionForce.</param>
    public void EnableRagdoll(Vector3 hitDirection, float forceMagnitude = -1f)
    {
        if (_animator != null) _animator.enabled = false;
        if (_agent != null)    _agent.enabled    = false;
        if (_freezeCoroutine != null)
        {
            StopCoroutine(_freezeCoroutine);
            _freezeCoroutine = null;
        }

        // Root Rigidbody stays kinematic — bone Rigidbodies drive the ragdoll.
        // Root Collider is disabled so it doesn't fight the bone colliders.
        if (_rootCollider != null) _rootCollider.enabled = false;
        EnsureGroundCollisionLayers();
        EnsureRagdollFrictionMaterial();

        foreach (Rigidbody rb in _boneRigidbodies)
        {
            rb.isKinematic = false;
            rb.useGravity  = true;
            rb.detectCollisions = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        foreach (Collider col in _boneColliders)
        {
            col.enabled = true;
            col.material = _ragdollFrictionMaterial;
        }

        float force = forceMagnitude > 0f ? forceMagnitude : explosionForce;
        ApplyHipImpulse(hitDirection, force);

        _freezeCoroutine = StartCoroutine(FreezeWhenSettled());
    }

    /// <summary>
    /// Restores the character to animated/agent-driven state.
    /// </summary>
    public void DisableRagdoll()
    {
        if (_freezeCoroutine != null)
        {
            StopCoroutine(_freezeCoroutine);
            _freezeCoroutine = null;
        }

        if (_animator != null) _animator.enabled = true;
        if (_agent != null && _agent.isActiveAndEnabled) _agent.enabled = true;

        if (_rootCollider != null) _rootCollider.enabled = true;

        foreach (Rigidbody rb in _boneRigidbodies)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
        foreach (Collider col in _boneColliders)
            col.enabled = false;
    }

    private void CacheBoneComponents()
    {
        var allRbs  = GetComponentsInChildren<Rigidbody>(true);
        var allCols = GetComponentsInChildren<Collider>(true);

        var boneRbs  = new List<Rigidbody>(allRbs.Length);
        var boneCols = new List<Collider>(allCols.Length);

        foreach (Rigidbody rb in allRbs)
            if (rb.gameObject != gameObject) boneRbs.Add(rb);

        foreach (Collider col in allCols)
            if (col.gameObject != gameObject) boneCols.Add(col);

        _boneRigidbodies = boneRbs.ToArray();
        _boneColliders   = boneCols.ToArray();
    }

    private void ApplyHipImpulse(Vector3 hitDirection, float force)
    {
        Rigidbody hipRb = null;

        if (hipBone != null)
            hipRb = hipBone.GetComponent<Rigidbody>();

        // Fall back to first bone Rigidbody if hip is unassigned
        if (hipRb == null && _boneRigidbodies.Length > 0)
            hipRb = _boneRigidbodies[0];

        if (hipRb == null) return;

        Vector3 impulse = (hitDirection.normalized + Vector3.up * upwardModifier).normalized;
        hipRb.AddForce(impulse * force, ForceMode.Impulse);
    }

    private void FreezeRagdoll()
    {
        foreach (Rigidbody rb in _boneRigidbodies)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    private IEnumerator FreezeWhenSettled()
    {
        if (freezeDelay > 0f)
            yield return new WaitForSeconds(freezeDelay);

        float settledFor = 0f;
        float waited = 0f;

        while (waited < maxFreezeWait)
        {
            bool settled = true;

            for (int i = 0; i < _boneRigidbodies.Length; i++)
            {
                Rigidbody rb = _boneRigidbodies[i];
                if (rb == null || rb.isKinematic)
                    continue;

                if (rb.linearVelocity.sqrMagnitude > settleVelocityThreshold * settleVelocityThreshold ||
                    rb.angularVelocity.sqrMagnitude > settleVelocityThreshold * settleVelocityThreshold)
                {
                    settled = false;
                    break;
                }
            }

            if (settled)
                settledFor += Time.deltaTime;
            else
                settledFor = 0f;

            if (settledFor >= settleDuration)
                break;

            waited += Time.deltaTime;
            yield return null;
        }

        FreezeRagdoll();
        _freezeCoroutine = null;
    }

    private void EnsureGroundCollisionLayers()
    {
        int enemyLayer = gameObject.layer;
        int defaultLayer = LayerMask.NameToLayer("Default");
        int environmentLayer = LayerMask.NameToLayer("Environment");

        if (defaultLayer >= 0)
            Physics.IgnoreLayerCollision(enemyLayer, defaultLayer, false);
        if (environmentLayer >= 0)
            Physics.IgnoreLayerCollision(enemyLayer, environmentLayer, false);
    }

    private void EnsureRagdollFrictionMaterial()
    {
        if (_ragdollFrictionMaterial != null)
            return;

        _ragdollFrictionMaterial = new PhysicsMaterial("RagdollHighFriction")
        {
            dynamicFriction = 1f,
            staticFriction = 1f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Maximum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
    }
}
