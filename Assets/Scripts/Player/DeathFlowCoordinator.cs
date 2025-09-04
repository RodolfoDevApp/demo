using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class DeathFlowCoordinator : MonoBehaviour
{
    [Header("Refs")]
    public PlayerHealth health;
    public Animator bodyAnimator;           // Player/Visual/Body
    public PlayerDeathLocker locker;        // tu script existente
    public GameOverUI gameOver;             // panel

    [Header("Detección de fin de anim")]
    public string deathStateTag = "Death";  // tag del clip de muerte
    public float maxWaitSeconds = 3f;       // timeout de seguridad

    int _isDeathHash;
    bool _finished;

    void Reset()
    {
        health ??= GetComponentInParent<PlayerHealth>() ?? GetComponent<PlayerHealth>();

        if (!bodyAnimator)
        {
            var t = transform.root.Find("Player/Visual/Body");
            if (t) bodyAnimator = t.GetComponent<Animator>();
        }

        locker ??= GetComponentInParent<PlayerDeathLocker>() ?? GetComponent<PlayerDeathLocker>();

        // Buscar GameOverUI aunque esté inactivo (soporta distintas versiones de Unity)
#if UNITY_2023_1_OR_NEWER || UNITY_2022_2_OR_NEWER
        if (!gameOver)
            gameOver = Object.FindFirstObjectByType<GameOverUI>(FindObjectsInactive.Include);
#else
        if (!gameOver)
            gameOver = Object.FindObjectOfType<GameOverUI>(true);
#endif
    }

    void Awake()
    {
        _isDeathHash = Animator.StringToHash("isDeath");
    }

    void OnEnable()
    {
        if (health) health.OnDeath.AddListener(OnDeath);
    }

    void OnDisable()
    {
        if (health) health.OnDeath.RemoveListener(OnDeath);
    }

    void OnDeath()
    {
        if (_finished) return;
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        // IMPORTANTE: no bloqueamos aquí. Dejamos que la anim corra.
        if (bodyAnimator && bodyAnimator.isActiveAndEnabled)
        {
            bodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            bodyAnimator.SetBool(_isDeathHash, true);
            bodyAnimator.Update(0f); // aplicar inmediatamente
        }

        // Esperar a que entre a un estado taggeado como "Death" (o timeout)
        float t = 0f;
        int tagHash = Animator.StringToHash(deathStateTag);
        bool enteredDeath = false;

        while (t < maxWaitSeconds)
        {
            if (bodyAnimator && bodyAnimator.isActiveAndEnabled)
            {
                var st = bodyAnimator.GetCurrentAnimatorStateInfo(0);
                if (st.tagHash == tagHash) { enteredDeath = true; break; }
            }
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Si entró, esperar a que consuma el clip (o hasta 1.5s extra)
        if (enteredDeath)
        {
            float extra = 1.5f;
            float t2 = 0f;
            while (t2 < extra)
            {
                if (!bodyAnimator || !bodyAnimator.isActiveAndEnabled) break;
                var st = bodyAnimator.GetCurrentAnimatorStateInfo(0);
                if (st.normalizedTime >= 0.99f) break;
                t2 += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // Ahora sí: bloquear y mostrar panel
        if (locker) locker.LockNow();
        if (gameOver) gameOver.Show();

        _finished = true;
    }
}
