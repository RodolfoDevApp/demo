using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [Header("Refs UI")]
    public CanvasGroup group;
    public LocalizedText title;     // key: "gameover.title"
    public LocalizedText subtitle;  // opcional
    public Button btnRevive;
    public Button btnRetry;
    public Button btnQuit;

    [Header("Jugador")]
    public PlayerHealth player;
    [Tooltip("Animators que limpiar al revivir (Body, Hands, Item).")]
    public Animator[] animatorsToClear;
    public string deathBoolName = "isDeath";

    [Header("Comportamiento")]
    public bool pauseOnShow = true;

    float _prevTS = 1f;

    void Awake()
    {
        HideImmediate();
        if (btnRevive) btnRevive.onClick.AddListener(Revive);
        if (btnRetry) btnRetry.onClick.AddListener(Retry);
        if (btnQuit) btnQuit.onClick.AddListener(QuitGame);
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
        if (pauseOnShow) Time.timeScale = _prevTS;
        SetVisible(false);

        // limpiar flag de muerte y rebind
        if (animatorsToClear != null)
        {
            foreach (var a in animatorsToClear)
            {
                if (!a) continue;
                if (!string.IsNullOrEmpty(deathBoolName)) a.SetBool(deathBoolName, false);
                a.Rebind(); a.Update(0f);
            }
        }

        if (player) player.Revive(player.maxHP); // reaparece con vida llena
    }

    public void Retry()
    {
        if (pauseOnShow) Time.timeScale = 1f;
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
