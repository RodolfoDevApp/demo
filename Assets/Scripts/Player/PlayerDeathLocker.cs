using UnityEngine;

[DisallowMultipleComponent]
public class PlayerDeathLocker : MonoBehaviour
{
    [Header("Refs")]
    public PlayerHealth health;
    public Rigidbody2D rb;

    [Header("Se desactivan al morir")]
    public MonoBehaviour[] toDisable;

    [Header("Opcional")]
    public Animator bodyAnim;
    public string speedParam = "Speed";

    [Header("Comportamiento")]
    public bool lockImmediately = false; // << respeta el toggle del inspector

    bool locked;

    void Reset()
    {
        health ??= GetComponentInParent<PlayerHealth>() ?? GetComponent<PlayerHealth>();
        rb ??= GetComponent<Rigidbody2D>() ?? GetComponentInParent<Rigidbody2D>();

        var pc = GetComponent<PlayerController2D>() ?? GetComponentInParent<PlayerController2D>();
        var whs = GetComponent<WeaponHotbarSimple>() ?? GetComponentInChildren<WeaponHotbarSimple>(true);
        var pick = GetComponent<PickupController>() ?? GetComponentInParent<PickupController>();
        var hands = GetComponentInChildren<HandsAnimatorDriver>(true);
        var weap = GetComponentInChildren<WeaponAnimatorDriver>(true);

        toDisable = new MonoBehaviour[] { pc, whs, pick, hands, weap };

        if (!bodyAnim)
        {
            var t = transform.root.Find("Player/Visual/Body");
            if (t) bodyAnim = t.GetComponent<Animator>();
            if (!bodyAnim) bodyAnim = GetComponentInChildren<Animator>();
        }
    }

    void OnEnable()
    {
        if (health && lockImmediately) health.OnDeath.AddListener(LockNow);
    }

    void OnDisable()
    {
        if (health && lockImmediately) health.OnDeath.RemoveListener(LockNow);
    }

    public void LockNow()
    {
        if (locked) return;
        locked = true;

        if (toDisable != null)
            foreach (var mb in toDisable)
                if (mb && mb.enabled) mb.enabled = false;

        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        if (bodyAnim && !string.IsNullOrEmpty(speedParam))
            bodyAnim.SetFloat(speedParam, 0f);
    }

    public void UnlockForRespawn()
    {
        locked = false;
        if (rb) rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (toDisable != null)
            foreach (var mb in toDisable)
                if (mb) mb.enabled = true;
    }
}
