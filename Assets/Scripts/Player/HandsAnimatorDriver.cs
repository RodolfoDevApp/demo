using UnityEngine;

[RequireComponent(typeof(Animator))]
public class HandsAnimatorDriver : MonoBehaviour
{
    [Header("Refs")]
    public Animator anim;       // Animator de Hands
    public Animator bodyAnim;   // para leer Dir/Speed
    public Rigidbody2D rb;      // opcional
    public WeaponHotbarSimple hotbar; // para saber si estamos en modo manos

    // mapping de tu proyecto
    const int DIR_DOWN = 0, DIR_RIGHT = 1, DIR_LEFT = 2, DIR_UP = 3;

    // parámetros del controller de Hands
    static readonly int P_Dir = Animator.StringToHash("Dir");
    static readonly int P_Speed = Animator.StringToHash("Speed");
    static readonly int P_Punch = Animator.StringToHash("Punch");
    static readonly int P_Pick = Animator.StringToHash("Pick");

    void Reset()
    {
        anim ??= GetComponent<Animator>();
        if (!bodyAnim) bodyAnim = GetComponentInParent<Animator>();
        if (!rb) rb = GetComponentInParent<Rigidbody2D>();

        // primero intenta en el parent (Player); si no, busca en escena
        if (!hotbar) hotbar = GetComponentInParent<WeaponHotbarSimple>();
        if (!hotbar) hotbar = FindFirstObjectByType<WeaponHotbarSimple>();
    }

    void Update()
    {
        if (!anim) return;

        // Dir / Speed
        int dir = GetDir();
        float spd = GetSpeed();
        anim.SetInteger(P_Dir, dir);
        anim.SetFloat(P_Speed, spd);

        // Sólo golpear si estamos en manos (sin arma equipada)
        bool inHandsMode = (hotbar == null) ? true : hotbar.IsHandsMode;
        if (inHandsMode && Input.GetKeyDown(KeyCode.J))
        {
            anim.ResetTrigger(P_Punch);
            anim.SetTrigger(P_Punch);
        }
    }

    int GetDir()
    {
        if (bodyAnim && HasParam(bodyAnim, P_Dir))
            return bodyAnim.GetInteger(P_Dir);

        if (!rb) return DIR_DOWN;

        Vector2 v = rb.linearVelocity; // Unity 6
        if (v.sqrMagnitude < 0.0001f)
            return anim ? anim.GetInteger(P_Dir) : DIR_DOWN;

        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            return (v.x >= 0f) ? DIR_RIGHT : DIR_LEFT;
        else
            return (v.y >= 0f) ? DIR_UP : DIR_DOWN;
    }

    float GetSpeed() => rb ? rb.linearVelocity.magnitude : 0f;

    static bool HasParam(Animator a, int hash)
    {
        if (!a) return false;
        var ps = a.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].nameHash == hash) return true;
        return false;
    }
}
