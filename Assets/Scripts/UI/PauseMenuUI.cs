using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PauseMenuUI : MonoBehaviour
{
    public CanvasGroup pausePanel;
    public Button btnResume;
    public Button btnQuit;
    public KeyCode toggleKey = KeyCode.Escape;

    bool _shown;
    float _prevTS = 1f;

    void Awake()
    {
        if (btnResume) btnResume.onClick.AddListener(Resume);
        if (btnQuit) btnQuit.onClick.AddListener(QuitGame);
        HideImmediate();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (_shown) Resume();
            else Show();
        }
    }

    public void Show()
    {
        _shown = true;
        _prevTS = Time.timeScale;
        Time.timeScale = 0f;
        if (pausePanel)
        {
            pausePanel.alpha = 1f;
            pausePanel.blocksRaycasts = true;
            pausePanel.interactable = true;
        }
    }

    public void Resume()
    {
        _shown = false;
        Time.timeScale = _prevTS;
        if (pausePanel)
        {
            pausePanel.alpha = 0f;
            pausePanel.blocksRaycasts = false;
            pausePanel.interactable = false;
        }
    }

    void HideImmediate()
    {
        if (!pausePanel) return;
        pausePanel.alpha = 0f;
        pausePanel.blocksRaycasts = false;
        pausePanel.interactable = false;
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
