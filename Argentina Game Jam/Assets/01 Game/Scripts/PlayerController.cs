using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 8f;
    
    [Header("Rotation")]
    public float rotationSpeed = 720f; // Grados por segundo (720 = 2 rotaciones completas/seg)

    public Tile currentTile { get; private set; }
    private bool _isMoving;

    public bool IsMoving => _isMoving;

    public void SnapToTile(Tile tile)
    {
        currentTile = tile;
        if (tile)
        {
            transform.position = tile.transform.position;
        }
    }

    public void TryMoveTo(Tile target)
    {
        if (_isMoving) return;
        if (currentTile == null || target == null) return;

        if (!BoardManager.Instance.AreAdjacent(currentTile.gridPos, target.gridPos))
        {
            Debug.Log("Not Allowed. Just adjacents Tiles (No diagonals)");
            return;
        }

        if (!GameManager.Instance.CanEnterTile(target))
        {
            // Mensajes específicos para Burn cap
            if (target.type == TileType.Burn)
                Debug.Log("No puedes pisar tantas rojas seguidas.");
            else if (!target.IsWalkable)
                Debug.Log("Bloqueado.");
            else
                Debug.Log("No puedes moverte ahí por otra razón.");
            return;
        }

        StartCoroutine(MoveRoutine(target));
    }

    private IEnumerator MoveRoutine(Tile target)
    {
        _isMoving = true;

        Vector3 start = transform.position;
        Vector3 end = target.transform.position;

        // ✨ NUEVO: Calcular dirección de movimiento
        Vector3 direction = (end - start).normalized;

        // ✨ NUEVO: Calcular rotación objetivo (mirando hacia la dirección)
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // ✨ NUEVO: Rotar antes de moverse (opcional: puedes rotar mientras se mueve)
            while (Quaternion.Angle(transform.rotation, targetRotation) > 0.1f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, 
                    targetRotation, 
                    rotationSpeed * Time.deltaTime
                );
                yield return null;
            }

            // Asegurar rotación exacta
            transform.rotation = targetRotation;
        }

        // ✨ MOVIMIENTO: Ahora se mueve después de rotar
        while ((transform.position - end).sqrMagnitude > 0.0004f)
        {
            transform.position = Vector3.MoveTowards(transform.position, end, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = end;
        currentTile = target;

        GameManager.Instance.OnPlayerEnteredTile(target);

        _isMoving = false;
    }
}