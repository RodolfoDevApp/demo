using System.Collections;
using UnityEngine;

// Dir mapping del proyecto: 0=down, 1=right, 2=left, 3=up
public class MuzzleFlash2D : MonoBehaviour
{
    [Header("Refs")]
    public Animator anim;            // Animator del Muzzle (controller con fire_*)
    public SpriteRenderer sourceSR;  // SR del ARMA (Item) - ¡NO el del Muzzle!
    public SpriteRenderer sr;        // SR del propio Muzzle

    [Header("Animator")]
    public string dirParamName = "Dir";
    public string flashTrigger = "Flash";
    public string idleStateName = "empty_1f";

    [Header("Playback")]
    public bool useDirectStatePlay = true;

    [Header("Timing")]
    public bool autoHide = true;
    public float flashLifetime = 0.07f;

    void Awake()
    {
        if (!anim) anim = GetComponent<Animator>();
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (!sourceSR) sourceSR = GetComponentInParent<SpriteRenderer>();
        if (sr) { sr.enabled = false; sr.sprite = null; }
        if (anim) anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    public void Play(int dir) => Play(dir, null);

    public void Play(int dir, SpriteRenderer bodySR)
    {
        if (!sr || !sourceSR) return;

        // Sorting por encima del arma/body
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

        // Mostrar y reproducir
        sr.sprite = null;
        sr.enabled = true;

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
                anim.Rebind();
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
}
