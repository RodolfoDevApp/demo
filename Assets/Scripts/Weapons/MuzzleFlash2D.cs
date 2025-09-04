using System.Collections;
using UnityEngine;

// Dir mapping del proyecto: 0=down, 1=right, 2=left, 3=up
public class MuzzleFlash2D : MonoBehaviour
{
    [Header("Refs")]
    public Animator anim;       // Animator del Muzzle (controller con fire_*)
    public SpriteRenderer sourceSR;   // SR del ARMA (Item) - ¡NO el del Muzzle!
    public SpriteRenderer sr;         // SR del propio Muzzle

    [Header("Animator")]
    public string dirParamName = "Dir";
    public string flashTrigger = "Flash";
    public string idleStateName = "empty_1f"; // pon aquí el nombre real del estado "idle"

    [Header("Playback")]
    public bool useDirectStatePlay = true; // true = anim.Play("fire_*") sin blending

    [Header("Auto placement")]
    public float edgePadding = 0.02f;
    [Tooltip("[0]=down, [1]=right, [2]=left, [3]=up")]
    public Vector2[] perDirNudge = new Vector2[4];

    [Header("Timing")]
    public bool autoHide = true;
    public float flashLifetime = 0.07f;

    [Header("Modo absoluto (opcional)")]
    public bool useAbsolutePositions = false;
    public Vector2 absDown, absUp, absLeft, absRight;

    // ------------ API usada por tu Hotbar ------------
    public void SetLocalPositions(Vector2 down, Vector2 up, Vector2 left, Vector2 right)
    {
        absDown = down; absUp = up; absLeft = left; absRight = right;
        useAbsolutePositions = true;
    }
    public void ClearLocalPositions() => useAbsolutePositions = false;

    public void SetNudges(Vector2 down, Vector2 up, Vector2 left, Vector2 right)
    {
        EnsureNudges();
        perDirNudge[0] = down;
        perDirNudge[1] = right;
        perDirNudge[2] = left;
        perDirNudge[3] = up;
    }
    // --------------------------------------------------

    void Awake()
    {
        if (!anim) anim = GetComponent<Animator>();
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (!sourceSR) sourceSR = GetComponentInParent<SpriteRenderer>();

        if (sr) { sr.enabled = false; sr.sprite = null; }
        if (anim) anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        EnsureNudges();
    }

    public void Play(int dir) => Play(dir, null);

    public void Play(int dir, SpriteRenderer bodySR)
    {
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (!sourceSR) sourceSR = GetComponentInParent<SpriteRenderer>();
        if (!sr || !sourceSR || !sourceSR.sprite) return;

        // 1) Posición (local al Item)
        Vector2 localPos;
        if (useAbsolutePositions)
        {
            localPos = dir switch
            {
                0 => absDown,
                1 => absRight,
                2 => absLeft,
                3 => absUp,
                _ => Vector2.zero
            };
        }
        else
        {
            var ext = sourceSR.sprite.bounds.extents; // en unidades del Item
            Vector2 baseLocal = dir switch
            {
                0 => new Vector2(0f, -ext.y),
                1 => new Vector2(ext.x, 0f),
                2 => new Vector2(-ext.x, 0f),
                3 => new Vector2(0f, ext.y),
                _ => Vector2.zero
            };
            Vector2 dirVec = dir switch
            {
                0 => Vector2.down,
                1 => Vector2.right,
                2 => Vector2.left,
                3 => Vector2.up,
                _ => Vector2.right
            };
            localPos = baseLocal + dirVec * edgePadding + perDirNudge[dir];
        }
        transform.localPosition = localPos;

        // 2) Sorting (encima del arma, o del body si lo pasas)
        if (bodySR)
        {
            sr.sortingLayerID = bodySR.sortingLayerID;
            sr.sortingOrder = bodySR.sortingOrder + 1;
        }
        else
        {
            sr.sortingLayerID = sourceSR.sortingLayerID;
            sr.sortingOrder = sourceSR.sortingOrder + 1;
        }

        // 3) Mostrar sin placeholder
        sr.sprite = null;
        sr.enabled = true;

        // 4) Reproducir sin blending y evaluar este mismo frame
        if (anim && anim.isActiveAndEnabled)
        {
            if (useDirectStatePlay)
            {
                string state = dir switch
                {
                    0 => "fire_down",
                    1 => "fire_right",
                    2 => "fire_left",
                    3 => "fire_up",
                    _ => "fire_down"
                };
                anim.Play(state, 0, 0f);
                anim.Update(0f);
            }
            else
            {
                if (!string.IsNullOrEmpty(dirParamName)) anim.SetInteger(dirParamName, dir);
                if (!string.IsNullOrEmpty(flashTrigger))
                {
                    anim.ResetTrigger(flashTrigger);
                    anim.SetTrigger(flashTrigger);
                }
                anim.Update(0f);
            }
        }
        else
        {
            // Fallback blink si no hay animator activo
            StopAllCoroutines();
            StartCoroutine(CoBlinkOnce());
        }

        if (autoHide)
        {
            StopAllCoroutines();
            StartCoroutine(CoAutoHide());
        }
    }

    public void Hide()
    {
        StopAllCoroutines();
        if (sr) { sr.enabled = false; sr.sprite = null; }

        if (anim && anim.isActiveAndEnabled)
        {
            int layer = 0;
            if (!string.IsNullOrEmpty(idleStateName) &&
                anim.HasState(layer, Animator.StringToHash(idleStateName)))
                anim.Play(idleStateName, layer, 0f);
            else
                anim.Rebind(); // sin warnings
        }
    }

    IEnumerator CoAutoHide()
    {
        yield return new WaitForSeconds(flashLifetime);
        if (sr) sr.enabled = false;
    }
    IEnumerator CoBlinkOnce()
    {
        sr.enabled = true;
        yield return new WaitForSeconds(flashLifetime);
        sr.enabled = false;
    }

    void EnsureNudges()
    {
        if (perDirNudge == null || perDirNudge.Length != 4)
            perDirNudge = new Vector2[4];
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (sr) sr.enabled = false;
    }

    void OnValidate()
    {
        if (!anim) anim = GetComponent<Animator>();
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (!sourceSR) sourceSR = GetComponentInParent<SpriteRenderer>();
        if (sr && sourceSR && sr == sourceSR)
            Debug.LogWarning("[MuzzleFlash2D] Source SR no puede ser el del Muzzle; asigna el del Item.");
    }
    public void SetNudgesPixels(int rightPx, int leftPx, int upPx, int downPx = 0)
    {
        if (!sourceSR) sourceSR = GetComponentInParent<SpriteRenderer>();
        EnsureNudges();

        float ppu = (sourceSR && sourceSR.sprite) ? sourceSR.sprite.pixelsPerUnit : 100f;
        float scaleX = (transform.parent ? transform.parent.lossyScale.x : 1f);
        float scaleY = (transform.parent ? transform.parent.lossyScale.y : 1f);

        // mapping del proyecto: 0=down, 1=right, 2=left, 3=up
        perDirNudge[0] = new Vector2(0f, downPx / (ppu * scaleY));   // Down  (0)
        perDirNudge[1] = new Vector2(+rightPx / (ppu * scaleX), 0f);               // Right (1)
        perDirNudge[2] = new Vector2(-leftPx / (ppu * scaleX), 0f);               // Left  (2)
        perDirNudge[3] = new Vector2(0f, upPx / (ppu * scaleY));   // Up    (3)
    }

}
