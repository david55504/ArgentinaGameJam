using UnityEngine;

public class HeatFeedback : MonoBehaviour
{
    [Header("Termómetro UI")]
    [Tooltip("El transform del eje que rotará (la aguja del termómetro)")]
    public Transform ejeTermometro;

    [Tooltip("Rango de rotación en Z: desde 0° (calor mínimo) hasta este valor (calor máximo)")]
    public float maxRotationZ = 165f;

    [Tooltip("Velocidad de suavizado de la rotación (mayor = más rápido)")]
    [Range(1f, 20f)]
    public float rotationSpeed = 8f;

    [Header("Color del Personaje")]
    [Tooltip("Material del jugador (arrastra directamente desde el Project)")]
    public Material playerMaterial;

    [Tooltip("Color cuando el calor está al máximo")]
    public Color overheatColor = new Color(1f, 0.2f, 0.2f, 1f); // Rojo

    [Tooltip("Velocidad de transición del color")]
    [Range(1f, 20f)]
    public float colorSpeed = 5f;

    // IDs de propiedades de shader (optimización)
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Standard

    private Color _originalColor;
    private bool _hasInitializedColor;
    private float _targetRotationZ;
    private float _currentRotationZ;

    private void Awake()
    {
        // Inicializar rotación actual
        if (ejeTermometro != null)
        {
            _currentRotationZ = ejeTermometro.localEulerAngles.z;
        }

        // Guardar color original del material
        if (playerMaterial != null)
        {
            if (playerMaterial.HasProperty(BaseColorId))
                _originalColor = playerMaterial.GetColor(BaseColorId);
            else if (playerMaterial.HasProperty(ColorId))
                _originalColor = playerMaterial.GetColor(ColorId);
            else
                _originalColor = Color.white;

            _hasInitializedColor = true;
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // Obtener datos de calor
        int currentHeat = GameManager.Instance.heat;
        int maxHeat = GameManager.Instance.maxHeat;

        if (maxHeat <= 0) return; // Evitar división por cero

        // Calcular porcentaje de calor (0.0 a 1.0)
        float heatPercentage = Mathf.Clamp01((float)currentHeat / maxHeat);

        // Actualizar termómetro
        UpdateThermometer(heatPercentage);

        // Actualizar color del personaje
        UpdatePlayerColor(heatPercentage);
    }

    private void UpdateThermometer(float heatPercentage)
    {
        if (ejeTermometro == null) return;

        // Calcular rotación objetivo basada en el porcentaje
        _targetRotationZ = heatPercentage * maxRotationZ;

        // Suavizar la transición
        _currentRotationZ = Mathf.Lerp(_currentRotationZ, _targetRotationZ, Time.deltaTime * rotationSpeed);

        // Aplicar rotación solo en el eje Z
        Vector3 currentRotation = ejeTermometro.localEulerAngles;
        ejeTermometro.localEulerAngles = new Vector3(
            currentRotation.x,
            currentRotation.y,
            _currentRotationZ
        );
    }

    private void UpdatePlayerColor(float heatPercentage)
    {
        if (playerMaterial == null) return;

        // Interpolación de color: blanco (0% calor) -> overheatColor (100% calor)
        Color targetColor = Color.Lerp(Color.white, overheatColor, heatPercentage);

        // Obtener color actual del material
        Color currentColor = Color.white;
        if (playerMaterial.HasProperty(BaseColorId))
            currentColor = playerMaterial.GetColor(BaseColorId);
        else if (playerMaterial.HasProperty(ColorId))
            currentColor = playerMaterial.GetColor(ColorId);

        // Suavizar transición de color
        Color smoothColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * colorSpeed);

        // Aplicar a ambas propiedades para compatibilidad URP/Standard
        if (playerMaterial.HasProperty(BaseColorId))
            playerMaterial.SetColor(BaseColorId, smoothColor);
        if (playerMaterial.HasProperty(ColorId))
            playerMaterial.SetColor(ColorId, smoothColor);
    }

    // Método público para resetear el feedback (llamar cuando se reinicia el juego)
    public void ResetFeedback()
    {
        // Resetear rotación
        _currentRotationZ = 0f;
        _targetRotationZ = 0f;

        if (ejeTermometro != null)
        {
            Vector3 currentRotation = ejeTermometro.localEulerAngles;
            ejeTermometro.localEulerAngles = new Vector3(
                currentRotation.x,
                currentRotation.y,
                0f
            );
        }

        // Resetear color del personaje al original
        if (playerMaterial != null && _hasInitializedColor)
        {
            if (playerMaterial.HasProperty(BaseColorId))
                playerMaterial.SetColor(BaseColorId, _originalColor);
            if (playerMaterial.HasProperty(ColorId))
                playerMaterial.SetColor(ColorId, _originalColor);
        }
    }

    private void OnEnable()
    {
        // Suscribirse al evento de reset del GameManager si existe
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameReset += OnGameReset;
        }
    }

    private void OnDisable()
    {
        // Desuscribirse del evento
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameReset -= OnGameReset;
        }
    }

    private void OnGameReset()
    {
        ResetFeedback();
    }

#if UNITY_EDITOR
    // Método de ayuda para visualizar en el editor
    private void OnValidate()
    {
        // Asegurar que los valores estén en rangos válidos
        maxRotationZ = Mathf.Clamp(maxRotationZ, 0f, 360f);
        rotationSpeed = Mathf.Max(0.1f, rotationSpeed);
        colorSpeed = Mathf.Max(0.1f, colorSpeed);
    }
#endif
}