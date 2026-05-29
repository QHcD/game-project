using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class EnemyRootMotionLock : MonoBehaviour
{
    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator != null)
            _animator.applyRootMotion = false;
    }

    private void OnEnable()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_animator != null)
            _animator.applyRootMotion = false;
    }

    private void OnAnimatorMove()
    {
        if (_animator != null && _animator.applyRootMotion)
            _animator.applyRootMotion = false;
    }
}
