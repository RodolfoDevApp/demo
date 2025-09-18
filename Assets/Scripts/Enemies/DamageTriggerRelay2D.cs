using System;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Combat/Damage Trigger Relay 2D")]
public class DamageTriggerRelay2D : MonoBehaviour
{
    public event Action<Collider2D> onEnter;
    public event Action<Collider2D> onStay;

    Collider2D _col;

    void Reset()
    {
        _col = GetComponent<Collider2D>();
        if (!_col) _col = gameObject.AddComponent<BoxCollider2D>();
        _col.isTrigger = true;
    }

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (_col) _col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other) => onEnter?.Invoke(other);
    void OnTriggerStay2D(Collider2D other) => onStay?.Invoke(other);
}
