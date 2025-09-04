using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PickupController : MonoBehaviour
{
    [Header("Input")]
    public KeyCode pickupKey = KeyCode.F;

    [Header("Detección")]
    public float pickupRadius = 1.0f;
    public LayerMask pickupMask; // opcional: filtra capa de pickups

    [Header("Refs")]
    public WeaponHotbarSimple hotbar;  // para saber si trae arma o manos
    public HandsPickProxy handsProxy;  // para disparar anim "Pick"

    IPickable pendingTarget; // objetivo que se recogerá al final de la anim

    void Reset()
    {
        if (!hotbar) hotbar = GetComponentInChildren<WeaponHotbarSimple>(true);
        if (!handsProxy) handsProxy = GetComponentInChildren<HandsPickProxy>(true);
    }

    void Update()
    {
        if (!Input.GetKeyDown(pickupKey)) return;

        var target = FindNearestPickable();
        if (target == null) return;

        bool hasWeapon = (hotbar != null && !hotbar.IsHandsMode);

        if (hasWeapon)
        {
            // Con arma: recoge sin animación de manos.
            target.Collect(gameObject);
        }
        else
        {
            // Con manos: reproducir anim y recoger al final (vía Event) o fallback.
            pendingTarget = target;
            handsProxy?.PlayPick();
            // Fallback por si aún no agregas Animation Event: recoge tras un breve delay.
            StartCoroutine(FallbackCollectAfter(0.20f));
        }
    }

    // Animation Event desde el clip de Hands en el frame de “agarre”
    // Agrega un evento que llame a:  PickupController.AE_DoCollect
    public void AE_DoCollect()
    {
        if (pendingTarget == null) return;
        pendingTarget.Collect(gameObject);
        pendingTarget = null;
    }

    IEnumerator FallbackCollectAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        AE_DoCollect(); // no pasa nada si ya se recogió por el Event
    }

    IPickable FindNearestPickable()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, pickupRadius, pickupMask);
        IPickable best = null;
        float bestDist = float.MaxValue;

        foreach (var h in hits)
        {
            var p = h.GetComponentInParent<IPickable>() ?? h.GetComponent<IPickable>();
            if (p == null) continue;
            float d = (h.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
#endif
}
