using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AIActivityCuller2D : MonoBehaviour
{
    [Header("Target para distancia")]
    public Transform player;
    public string playerTag = "Player";

    [Header("Culling por distancia")]
    public bool enableDistanceCulling = false;
    public float sleepDistance = 35f;
    public float wakeDistance = 28f;
    public float checkInterval = 0.5f;

    [Header("Componentes a pausar")]
    public MonoBehaviour[] aiBehaviours; // ej: ZombieAxeAI, ZombieDaggerAI, ZombieBigAI
    public Behaviour[] extraBehaviours;  // ej: ContactDamage2D, MeleeHitbox2D

    float _nextCheck;
    bool _sleeping;
    readonly List<Behaviour> _all = new List<Behaviour>();

    void Reset()
    {
        player = null;
        _sleeping = false;
        _nextCheck = 0f;
    }

    void Awake()
    {
        if (!player && !string.IsNullOrEmpty(playerTag))
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go) player = go.transform;
        }

        _all.Clear();
        if (aiBehaviours != null)
            foreach (var b in aiBehaviours) if (b) _all.Add(b);
        if (extraBehaviours != null)
            foreach (var b in extraBehaviours) if (b) _all.Add(b);
    }

    void Update()
    {
        if (!enableDistanceCulling) return;
        if (!player) return;
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + Mathf.Max(0.05f, checkInterval);

        float d = Vector2.Distance(transform.position, player.position);

        if (!_sleeping && d >= sleepDistance)
        {
            SetActive(false);
            _sleeping = true;
        }
        else if (_sleeping && d <= wakeDistance)
        {
            SetActive(true);
            _sleeping = false;
        }
    }

    void OnEnable()
    {
        if (_sleeping) SetActive(false);
    }

    void OnDisable()
    {
        // no cambiamos estados aqui
    }

    void SetActive(bool on)
    {
        foreach (var b in _all)
        {
            if (!b) continue;
            // Caso especial: para no dejar ContactDamage encendido si dormimos
            if (!on && b is Behaviour) b.enabled = false;
            else b.enabled = on;
        }

        // Al reactivar, aseguremos ContactDamage apagado si tu IA lo enciende solo en dash (Axe)
        // Nota: si tu Dagger usa ContactDamage siempre encendido, no agregues ese componente a extraBehaviours.
    }
}
