using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [Header("Refs UI")]
    public CanvasGroup group;
    public LocalizedText title;
    public LocalizedText subtitle;
    public Button btnRevive;
    public Button btnRetry;
    public Button btnQuit;

    [Header("Jugador")]
    public PlayerHealth player;
    public PlayerDeathLocker locker;
    public DeathFlowCoordinator deathFlow;
    public Animator[] animatorsToClear;
    public string deathBoolName = "isDeath";

    [Header("Comportamiento")]
    public bool pauseOnShow = true;

    float _prevTS = 1f;
    int _deathBoolHash;

    void Awake()
    {
        HideImmediate();
        DisableDecorativeRaycasts();

        if (btnRevive) btnRevive.onClick.AddListener(Revive);
        if (btnRetry) btnRetry.onClick.AddListener(Retry);
        if (btnQuit) btnQuit.onClick.AddListener(QuitGame);

        _deathBoolHash = Animator.StringToHash(string.IsNullOrEmpty(deathBoolName) ? "isDeath" : deathBoolName);
    }

    void DisableDecorativeRaycasts()
    {
        if (!group) return;
        var tmps = group.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in tmps) t.raycastTarget = false;
        var images = group.GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            if (!img) continue;
            var selectable = img.GetComponentInParent<Selectable>();
            if (selectable == null) img.raycastTarget = false;
        }
        var raws = group.GetComponentsInChildren<RawImage>(true);
        foreach (var ri in raws)
        {
            if (!ri) continue;
            var selectable = ri.GetComponentInParent<Selectable>();
            if (selectable == null) ri.raycastTarget = false;
        }
    }

    public void Show()
    {
        if (pauseOnShow) { _prevTS = Time.timeScale; Time.timeScale = 0f; }
        SetVisible(true);
        if (title) title.Refresh();
        if (subtitle) subtitle.Refresh();
    }

    public void Hide()
    {
        if (pauseOnShow) Time.timeScale = _prevTS;
        SetVisible(false);
    }

    void HideImmediate()
    {
        if (!group) return;
        group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
    }

    void SetVisible(bool v)
    {
        if (!group) return;
        group.alpha = v ? 1f : 0f;
        group.blocksRaycasts = v;
        group.interactable = v;
    }

    public void Revive()
    {
        Time.timeScale = 1f;      // asegurar mundo corriendo
        SetVisible(false);

        if (animatorsToClear != null)
            foreach (var a in animatorsToClear)
                SafeClearDeath(a, _deathBoolHash);

        if (player) player.Revive(player.maxHP);

        // forzar unlock adicional por si el listener no esta
        if (locker) locker.UnlockForRespawn();

        if (deathFlow) deathFlow.ResetForNextDeath();
    }

    static void SafeClearDeath(Animator a, int deathBoolHash)
    {
        if (!a) return;
        bool hasDeathBool = false;
        var ps = a.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].nameHash == deathBoolHash && ps[i].type == AnimatorControllerParameterType.Bool)
            { hasDeathBool = true; break; }

        if (hasDeathBool) a.SetBool(deathBoolHash, false);
        if (a.isActiveAndEnabled) a.Rebind();
    }

    public void Retry()
    {
        Time.timeScale = 1f;
        var scn = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scn.buildIndex);
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
