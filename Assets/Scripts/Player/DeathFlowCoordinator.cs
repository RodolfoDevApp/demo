using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class DeathFlowCoordinator : MonoBehaviour
{
    [Header("Refs")]
    public PlayerHealth health;
    public Animator bodyAnimator;           // Player/Visual/Body
    public PlayerDeathLocker locker;        // bloqueo de controles
    public GameOverUI gameOver;             // panel Game Over

    [Header("Animacion de muerte")]
    public string deathStateTag = "Death";  // tag del clip de muerte
    public float maxWaitSeconds = 3f;       // timeout de seguridad
    public string deathBoolName = "isDeath";

    int _isDeathHash;
    int _deathTagHash;
    bool _finished;
    bool _hasDeathBool;

    void Reset()
    {
        health ??= GetComponentInParent<PlayerHealth>() ?? GetComponent<PlayerHealth>();

        if (!bodyAnimator)
        {
            var t = transform.root ? transform.root.Find("Player/Visual/Body") : null;
            if (t) bodyAnimator = t.GetComponent<Animator>();
        }

        locker ??= GetComponentInParent<PlayerDeathLocker>() ?? GetComponent<PlayerDeathLocker>();

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
        _isDeathHash = Animator.StringToHash(string.IsNullOrEmpty(deathBoolName) ? "isDeath" : deathBoolName);
        _deathTagHash = Animator.StringToHash(string.IsNullOrEmpty(deathStateTag) ? "Death" : deathStateTag);

        // Detectar si el bool existe realmente en el Animator
        if (bodyAnimator)
        {
            foreach (var p in bodyAnimator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool && p.nameHash == _isDeathHash)
                {
                    _hasDeathBool = true;
                    break;
                }
            }
        }
    }

    void OnEnable()
    {
        if (health)
        {
            health.OnDeath.AddListener(OnDeath);
            health.OnRevive.AddListener(OnReviveReset);
        }
    }

    void OnDisable()
    {
        if (health)
        {
            health.OnDeath.RemoveListener(OnDeath);
            health.OnRevive.RemoveListener(OnReviveReset);
        }
    }

    void OnReviveReset()
    {
        _finished = false;

        if (bodyAnimator && bodyAnimator.isActiveAndEnabled)
        {
            if (_hasDeathBool) bodyAnimator.SetBool(_isDeathHash, false);

            // Rebind para limpiar pose y devolver a defaults
            bodyAnimator.Rebind();

            // No llamamos Animator.Update(0f) para evitar el warning de objeto inactivo
            bodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    void OnDeath()
    {
        if (_finished) return;
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        if (bodyAnimator && bodyAnimator.isActiveAndEnabled)
        {
            bodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (_hasDeathBool) bodyAnimator.SetBool(_isDeathHash, true);
            // No se llama Update(0f)
        }

        float t = 0f;
        bool enteredDeath = false;

        while (t < maxWaitSeconds)
        {
            if (bodyAnimator && bodyAnimator.isActiveAndEnabled)
            {
                var st = bodyAnimator.GetCurrentAnimatorStateInfo(0);
                if (st.tagHash == _deathTagHash) { enteredDeath = true; break; }
            }
            t += Time.unscaledDeltaTime;
            yield return null;
        }

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

        if (locker) locker.LockNow();
        if (gameOver) gameOver.Show();

        _finished = true;
    }

    public void ResetForNextDeath()
    {
        OnReviveReset();
    }
}
