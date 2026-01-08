using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyUnit))]
public class EnemyActions : MonoBehaviour
{
    [SerializeField] private EnemyAnimationController animController;

    private EnemyUnit unit;

    private void Awake()
    {
        unit = GetComponent<EnemyUnit>();

        if (animController == null)
            animController = GetComponent<EnemyAnimationController>();
    }

    public IEnumerator AttackCoroutine()
    {
        var gm = GameManager.Instance;
        var player = gm != null ? gm.player : null;
        if (player == null)
        {
            unit.DebugLog("ERROR: Player is null during attack.");
            yield break;
        }

        unit.DebugLog(">>> ATTACK START <<<");

        // Rotate towards player
        Vector3 dir = (player.transform.position - transform.position).normalized;
        yield return RotateTowards(dir, unit.rotationSpeed, 0.5f);

        // Play anim
        if (animController != null) animController.PlayAttack();

        yield return new WaitForSeconds(0.5f);

        // Apply heat damage
        gm.ApplyEnemyAttackHeat(unit.attackHeatDamage);

        // VFX
        if (unit.attackEffectPrefab != null)
        {
            var fx = Instantiate(unit.attackEffectPrefab, player.transform.position, Quaternion.identity);
            Destroy(fx, unit.attackEffectDuration);
        }

        yield return new WaitForSeconds(0.3f);
        if (animController != null) animController.SetMoving(false);

        unit.DebugLog(">>> ATTACK END <<<");
    }

    public IEnumerator MoveToTileCoroutine(Tile destination)
    {
        if (destination == null || unit.currentTile == null)
        {
            unit.DebugLog("ERROR: Null references during move.");
            yield break;
        }

        unit.DebugLog($">>> MOVE START ({unit.currentTile.gridPos} -> {destination.gridPos}) <<<");

        // Rotate towards destination
        Vector3 dir = (destination.transform.position - transform.position).normalized;
        yield return RotateTowards(dir, unit.rotationSpeed, 0.5f);

        // Anim ON
        if (animController != null) animController.SetMoving(true);

        // Move
        Vector3 startPos = transform.position;
        Vector3 targetPos = destination.transform.position;

        if (unit.moveSpeed <= 0.01f)
        {
            unit.DebugLog("moveSpeed too low. Teleporting.");
            transform.position = targetPos;
        }
        else
        {
            float dur = Vector3.Distance(startPos, targetPos) / unit.moveSpeed;
            float t = 0f;

            while (t < dur)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / dur);
                transform.position = Vector3.Lerp(startPos, targetPos, a);
                yield return null;
            }

            transform.position = targetPos;
        }

        // Update tile
        unit.currentTile = destination;

        // Anim OFF
        if (animController != null) animController.SetMoving(false);

        yield return new WaitForSeconds(0.2f);
        unit.DebugLog(">>> MOVE END <<<");
    }

    private IEnumerator RotateTowards(Vector3 direction, float degreesPerSec, float maxTime)
    {
        if (direction.sqrMagnitude < 0.0001f) yield break;

        Quaternion target = Quaternion.LookRotation(direction);
        float elapsed = 0f;

        while (Quaternion.Angle(transform.rotation, target) > 1f && elapsed < maxTime)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degreesPerSec * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = target;
    }
}
