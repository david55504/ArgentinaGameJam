using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    public Animator animator;  // Arrastra aquí el Animator del hijo (la maya)
    
    [Header("Animation Parameters")]
    private static readonly int IsMoving = Animator.StringToHash("isMoving");
    private static readonly int Attack = Animator.StringToHash("attack");
    private static readonly int Die = Animator.StringToHash("die");
    
    private PlayerController _playerController;
    
    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("No se encontró Animator en los hijos del Player.");
            }
        }
    }
    
    private void Update()
    {
        if (animator == null || _playerController == null) return;
        
        // Actualizar el estado de movimiento
        animator.SetBool(IsMoving, _playerController.IsMoving);
    }
    
    public void PlayAttack()
    {
        if (animator == null) return;
        animator.SetTrigger(Attack);
    }
    
    public void PlayDeath()
    {
        if (animator == null) return;
        animator.SetTrigger(Die);
    }

    // ✨ MÉTODO NUEVO - Asegúrate que esté aquí
    public void ResetToIdle()
    {
        if (animator == null) return;
        
        // Resetear todos los triggers
        animator.ResetTrigger(Attack);
        animator.ResetTrigger(Die);
        
        // Forzar isMoving a false
        animator.SetBool(IsMoving, false);
        
        // Reproducir el estado Idle directamente
        animator.Play("Idle", 0, 0f);
        
        Debug.Log("Player animation reset to Idle.");
    }
}