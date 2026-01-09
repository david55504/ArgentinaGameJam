using UnityEngine;

public class EnemyAnimationController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Arrastra aquí el Animator del hijo (la maya del enemigo)")]
    public Animator animator;
    
    [Header("Animation Parameters")]
    private static readonly int IsMoving = Animator.StringToHash("isMoving");
    private static readonly int Attack = Animator.StringToHash("attack");
    
    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError($"No se encontró Animator en '{name}'. Por favor asigna el Animator manualmente en el Inspector.");
            }
        }
    }
    
    public void SetMoving(bool isMoving)
    {
        if (animator == null)
        {
            Debug.LogWarning($"Animator is null in {name}. Asigna el Animator en el Inspector.");
            return;
        }
        
        animator.SetBool(IsMoving, isMoving);
    }
    
    public void PlayAttack()
    {
        if (animator == null)
        {
            Debug.LogWarning($"Animator is null in {name}. Asigna el Animator en el Inspector.");
            return;
        }
        
        animator.SetTrigger(Attack);
    }
    
    public void ResetToIdle()
    {
        if (animator == null)
        {
            Debug.LogWarning($"Animator is null in {name}. Asigna el Animator en el Inspector.");
            return;
        }
        
        animator.ResetTrigger(Attack);
        animator.SetBool(IsMoving, false);
        animator.Play("Idle", 0, 0f);
        
        Debug.Log($"Enemy '{name}' animation reset to Idle.");
    }
}