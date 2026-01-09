using UnityEngine;

public class CameraSystem : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Transform del jugador (arrastra el GameObject del Player aquí)")]
    public Transform playerTransform;

    [Tooltip("Punto central del mapa para vista de fondo")]
    public Transform targetBackground;

    [Header("Seguimiento del Jugador")]
    [Tooltip("Suavizado del seguimiento cuando sigue al jugador")]
    [Range(0.01f, 1f)]
    public float followSmoothTime = 0.15f;

    [Header("Transiciones")]
    [Tooltip("Velocidad de movimiento entre el jugador y el fondo")]
    [Range(1f, 20f)]
    public float transitionSpeed = 5f;

    [Tooltip("Velocidad del cambio de zoom (orthographic size)")]
    [Range(1f, 20f)]
    public float sizeTransitionSpeed = 3f;

    [Header("Tamaño de Cámara")]
    [Tooltip("Tamaño de la cámara cuando muestra el tablero completo")]
    public float backgroundSize = 10f;

    // Referencias internas
    private Camera _camera;
    private Transform _targetPlayer;
    private Vector3 _offset;
    private float _originalSize;
    
    // Variables para suavizado
    private Vector3 _velocity = Vector3.zero;
    private TurnState _lastState;

    private void Start()
    {
        // Obtener componente de cámara
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            Debug.LogError("CameraSystem: No se encontró componente Camera.");
            enabled = false;
            return;
        }

        // Guardar tamaño original de la cámara
        _originalSize = _camera.orthographicSize;

        // Crear objeto TargetPlayer
        GameObject targetPlayerObj = new GameObject("TargetPlayer");
        _targetPlayer = targetPlayerObj.transform;

        // Posicionar TargetPlayer en la posición inicial de la cámara
        _targetPlayer.position = transform.position;

        // Calcular offset entre cámara y jugador si existe
        if (playerTransform != null)
        {
            _offset = transform.position - playerTransform.position;
        }
        else
        {
            Debug.LogWarning("CameraSystem: playerTransform no asignado. Usa la posición actual como offset.");
            _offset = Vector3.zero;
        }

        // Inicializar estado
        if (GameManager.Instance != null)
        {
            _lastState = GameManager.Instance.state;
        }
    }

    private void LateUpdate()
    {
        if (GameManager.Instance == null || _camera == null || _targetPlayer == null)
            return;

        TurnState currentState = GameManager.Instance.state;

        // Detectar cambio de estado
        if (currentState != _lastState)
        {
            _lastState = currentState;
        }

        // Decidir modo según el estado del juego
        switch (currentState)
        {
            case TurnState.PlayerTurn:
            case TurnState.Busy:
                UpdatePlayerMode();
                break;

            case TurnState.EnemyTurn:
            case TurnState.Won:
            case TurnState.Lost:
                UpdateBackgroundMode();
                break;
        }
    }

    private void UpdatePlayerMode()
    {
        if (playerTransform == null) return;

        // Actualizar posición del TargetPlayer para seguir al jugador con offset
        Vector3 targetPosition = playerTransform.position + _offset;
        _targetPlayer.position = Vector3.SmoothDamp(
            _targetPlayer.position,
            targetPosition,
            ref _velocity,
            followSmoothTime
        );

        // Mover cámara hacia TargetPlayer
        Vector3 newCameraPosition = Vector3.Lerp(
            transform.position,
            _targetPlayer.position,
            Time.deltaTime * transitionSpeed
        );
        transform.position = newCameraPosition;

        // Volver al tamaño original de la cámara
        _camera.orthographicSize = Mathf.Lerp(
            _camera.orthographicSize,
            _originalSize,
            Time.deltaTime * sizeTransitionSpeed
        );
    }

    private void UpdateBackgroundMode()
    {
        if (targetBackground == null)
        {
            Debug.LogWarning("CameraSystem: targetBackground no asignado. No se puede cambiar a modo fondo.");
            return;
        }

        // Mantener la altura Z de la cámara, solo mover en X e Y
        Vector3 targetPosition = new Vector3(
            targetBackground.position.x,
            targetBackground.position.y,
            transform.position.z
        );

        // Mover cámara hacia el fondo
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * transitionSpeed
        );

        // Cambiar tamaño de la cámara para mostrar todo el tablero
        _camera.orthographicSize = Mathf.Lerp(
            _camera.orthographicSize,
            backgroundSize,
            Time.deltaTime * sizeTransitionSpeed
        );
    }

    // Método público para actualizar el offset manualmente si es necesario
    public void RecalculateOffset()
    {
        if (playerTransform != null)
        {
            _offset = transform.position - playerTransform.position;
            Debug.Log($"CameraSystem: Offset recalculado a {_offset}");
        }
    }

    // Método público para forzar la cámara a una posición específica (útil para resets)
    public void SnapToPlayer()
    {
        if (playerTransform == null || _targetPlayer == null) return;

        Vector3 targetPosition = playerTransform.position + _offset;
        _targetPlayer.position = targetPosition;
        transform.position = targetPosition;
        _velocity = Vector3.zero;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Asegurar que los valores estén en rangos válidos
        followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
        transitionSpeed = Mathf.Max(0.1f, transitionSpeed);
        sizeTransitionSpeed = Mathf.Max(0.1f, sizeTransitionSpeed);
        backgroundSize = Mathf.Max(0.1f, backgroundSize);
    }

    private void OnDrawGizmosSelected()
    {
        // Visualizar el offset en el editor
        if (playerTransform != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 targetPos = playerTransform.position + _offset;
            Gizmos.DrawWireSphere(targetPos, 0.5f);
            Gizmos.DrawLine(playerTransform.position, targetPos);
        }

        // Visualizar posición del fondo
        if (targetBackground != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(targetBackground.position, 1f);
        }
    }
#endif
}