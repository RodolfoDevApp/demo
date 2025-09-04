using UnityEngine;

[DisallowMultipleComponent]
public class HandsMeleeBridge : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Hitbox de pu�os (MeleeHitbox2D) que vive como HIJO de 'Hands'.")]
    public MeleeHitbox2D hitbox;
    [Tooltip("Animator del Body para leer 'Dir' si llamas Begin() sin par�metro.")]
    public Animator bodyAnim;

    // mapping del proyecto: 0=Down, 1=Right, 2=Left, 3=Up
    static readonly int P_Dir = Animator.StringToHash("Dir");

    void Reset()
    {
        if (!hitbox) hitbox = GetComponentInChildren<MeleeHitbox2D>(true);
        if (!bodyAnim) bodyAnim = GetComponentInParent<Animator>();
    }

    // ---------- Animation Events (desde clips de HANDS) ----------

    // Usa este si en el Animation Event pasas el int de direcci�n (0/1/2/3)
    public void Begin(int dir)
    {
        if (hitbox) hitbox.Begin(dir);
    }

    // Usa este si NO quieres pasar par�metros; leer� 'Dir' del body.
    public void Begin()
    {
        int dir = 0;
        if (bodyAnim && HasParam(bodyAnim, P_Dir))
            dir = bodyAnim.GetInteger(P_Dir);
        Begin(dir);
    }

    // Cierra ventana de golpe
    public void End()
    {
        if (hitbox) hitbox.End();
    }

    void OnDisable() { End(); } // failsafe

    static bool HasParam(Animator a, int hash)
    {
        if (!a) return false;
        foreach (var p in a.parameters) if (p.nameHash == hash) return true;
        return false;
    }
}
