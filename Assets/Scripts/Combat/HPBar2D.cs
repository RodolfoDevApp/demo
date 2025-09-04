using UnityEngine;

[DisallowMultipleComponent]
public class HPBar2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Damageable target;          // Damageable del enemigo
    [SerializeField] Transform fill;             // hijo con SpriteRenderer (la barra)

    [Header("Posición")]
    [SerializeField] Vector3 worldOffset = new(0, 0.6f, 0);
    [Tooltip("Ajusta Y automáticamente según el alto del sprite del objetivo.")]
    [SerializeField] bool autoOffsetY = true;
    [SerializeField, Range(0f, 1.5f)] float yFactor = 0.6f;

    [Header("Render")]
    [Tooltip("Copiar capa/orden del SpriteRenderer del objetivo.")]
    [SerializeField] bool matchTargetSorting = true;
    [SerializeField] int sortingOffset = 10;     // dibujar por encima

    float fullWidth = 1f;
    SpriteRenderer fillSR;
    SpriteRenderer targetSR;

    void Reset()
    {
        if (!target) target = GetComponentInParent<Damageable>();
        if (!fill && transform.childCount > 0) fill = transform.GetChild(0);
    }

    void Awake()
    {
        if (!target) target = GetComponentInParent<Damageable>();
        if (fill) fillSR = fill.GetComponent<SpriteRenderer>();
        if (target) targetSR = target.GetComponentInChildren<SpriteRenderer>(true);

        if (!fillSR)
            Debug.LogWarning("[HPBar2D] El hijo 'Fill' necesita un SpriteRenderer.");
        else
            fullWidth = Mathf.Abs(fill.localScale.x) < 0.0001f ? 1f : fill.localScale.x;

        if (target)
        {
            target.onHealthChanged.AddListener(UpdateBar);
            UpdateBar(target.CurrentHP);
        }
    }

    void OnDestroy()
    {
        if (target) target.onHealthChanged.RemoveListener(UpdateBar);
    }

    void LateUpdate()
    {
        if (!target) return;

        // Offset automático basado en el alto del sprite del objetivo
        if (autoOffsetY && targetSR && targetSR.sprite)
        {
            float h = targetSR.sprite.bounds.extents.y * 2f; // alto en unidades de mundo
            worldOffset.y = h * yFactor;
        }

        // Seguir al objetivo (y sin rotar)
        transform.position = target.transform.position + worldOffset;
        transform.rotation = Quaternion.identity;

        // Asegurar que la barra se renderiza por encima del objetivo
        if (matchTargetSorting && fillSR && targetSR)
        {
            fillSR.sortingLayerID = targetSR.sortingLayerID;
            fillSR.sortingOrder = targetSR.sortingOrder + sortingOffset;
        }
    }

    // Actualiza el ancho de la barra anclando el lado izquierdo
    void UpdateBar(float hp)
    {
        if (!target || !fill) return;

        float t = Mathf.Clamp01(hp / target.MaxHP);

        var s = fill.localScale;
        float newW = fullWidth * t;
        float dx = (newW - s.x) * 0.5f;   // desplazar para anclar a la izquierda
        s.x = newW;
        fill.localScale = s;

        var p = fill.localPosition;
        p.x += dx;
        fill.localPosition = p;
    }
}
