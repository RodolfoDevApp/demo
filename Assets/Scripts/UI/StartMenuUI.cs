using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StartMenuUI : MonoBehaviour
{
    public CanvasGroup group;
    public Button btnStart;
    public Button btnQuit;

    public WaveDirector2D[] waveDirectors;
    public bool autoFindWaveDirectors = true;
    public bool autoDisableWaveDirectors = true;
    public bool pauseTime = true;

    float prevTS = 1f;

    void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();

        if (autoFindWaveDirectors && (waveDirectors == null || waveDirectors.Length == 0))
            waveDirectors = FindObjectsOfType<WaveDirector2D>(true);

        if (pauseTime) { prevTS = Time.timeScale; Time.timeScale = 0f; }
        if (group) { group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true; }

        if (autoDisableWaveDirectors && waveDirectors != null)
            for (int i = 0; i < waveDirectors.Length; i++)
                if (waveDirectors[i]) waveDirectors[i].enabled = false;

        if (btnStart) btnStart.onClick.AddListener(StartGame);
        if (btnQuit) btnQuit.onClick.AddListener(QuitGame);
    }

    void StartGame()
    {
        if (group) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }

        if (pauseTime) Time.timeScale = prevTS;

        if (waveDirectors != null)
        {
            for (int i = 0; i < waveDirectors.Length; i++)
            {
                var wd = waveDirectors[i];
                if (!wd) continue;

                if (!wd.gameObject.activeSelf) wd.gameObject.SetActive(true);
                wd.enabled = true; // WaveDirector2D.OnEnable() debe iniciar las oleadas
            }
        }
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
