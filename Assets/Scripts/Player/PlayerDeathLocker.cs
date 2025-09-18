using System.Collections.Generic;
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
    public bool lockImmediately = false;

    bool locked;

    // refs internas para ordenar habilitacion
    PlayerController2D _pc;
    WeaponHotbarSimple _whs;
    PickupController _pick;
    HandsAnimatorDriver _hands;
    WeaponAnimatorDriver _weap;

    void Reset() { AutoWire(); }
    void Awake() { AutoWire(); } // SIEMPRE

    void OnEnable()
    {
        if (health)
        {
            if (lockImmediately) health.OnDeath.AddListener(LockNow);
            health.OnRevive.AddListener(UnlockForRespawn);
        }
    }

    void OnDisable()
    {
        if (health)
        {
            if (lockImmediately) health.OnDeath.RemoveListener(LockNow);
            health.OnRevive.RemoveListener(UnlockForRespawn);
        }
    }

    void AutoWire()
    {
        health ??= GetComponentInParent<PlayerHealth>() ?? GetComponent<PlayerHealth>();
        rb ??= GetComponent<Rigidbody2D>() ?? GetComponentInParent<Rigidbody2D>();

        _pc = GetComponent<PlayerController2D>() ?? GetComponentInParent<PlayerController2D>();
        _whs = GetComponent<WeaponHotbarSimple>() ?? GetComponentInChildren<WeaponHotbarSimple>(true);
        _pick = GetComponent<PickupController>() ?? GetComponentInParent<PickupController>();
        _hands = GetComponentInChildren<HandsAnimatorDriver>(true);
        _weap = GetComponentInChildren<WeaponAnimatorDriver>(true);

        if (!bodyAnim)
        {
            var t = transform.root ? transform.root.Find("Player/Visual/Body") : null;
            if (t) bodyAnim = t.GetComponent<Animator>();
            if (!bodyAnim) bodyAnim = GetComponentInChildren<Animator>(true);
        }

        // reconstruye toDisable limpio y en orden
        var list = new List<MonoBehaviour>(8);
        if (_pc) list.Add(_pc);
        if (_whs) list.Add(_whs);
        if (_pick) list.Add(_pick);
        if (_hands) list.Add(_hands);
        if (_weap) list.Add(_weap);
        toDisable = list.ToArray();
    }

    public void LockNow()
    {
        if (locked) return;
        locked = true;

        if (toDisable != null)
            for (int i = 0; i < toDisable.Length; i++)
                if (toDisable[i] && toDisable[i].enabled) toDisable[i].enabled = false;

        if (rb)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        if (bodyAnim && !string.IsNullOrEmpty(speedParam))
            bodyAnim.SetFloat(speedParam, 0f);
    }

    public void UnlockForRespawn()
    {
        // por si algo cambio en runtime
        AutoWire();
        locked = false;

        if (rb)
        {
            rb.simulated = true;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.WakeUp();
        }

        // habilitar en orden de dependencias
        if (_hands) _hands.enabled = true;
        if (_weap) _weap.enabled = true;
        if (_whs) _whs.enabled = true;
        if (_pick) _pick.enabled = true;
        if (_pc) _pc.enabled = true;

        // fallback: asegurar que TODO quede enabled
        if (toDisable != null)
            for (int i = 0; i < toDisable.Length; i++)
                if (toDisable[i]) toDisable[i].enabled = true;
    }

    [ContextMenu("Force Unlock Now")]
    void CM_ForceUnlock() => UnlockForRespawn();
}
