using UnityEngine;

[DisallowMultipleComponent]
public class EnemyTacticalActions : MonoBehaviour
{
    private Animator _anim;
    private bool _hasIsProne;

    private void Awake()
    {
        _anim = GetComponentInChildren<Animator>();
        if (_anim != null)
        {
            foreach (AnimatorControllerParameter p in _anim.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool && p.name == "IsProne")
                {
                    _hasIsProne = true;
                    break;
                }
            }
        }

        ClearProne();
    }

    private void OnEnable()
    {
        ClearProne();
    }

    private void ClearProne()
    {
        if (_anim != null && _hasIsProne)
            _anim.SetBool("IsProne", false);
    }
}
