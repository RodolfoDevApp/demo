using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

public class SurvivalHUD : MonoBehaviour
{
    [Header("Refs")]
    public WeaponAnimatorDriver weapon;
    public WaveDirector2D director;

    [Header("UI")]
    public TMP_Text ammoText;
    public TMP_Text waveText;
    public TMP_Text zombiesText;
    public TMP_Text nextWaveText;

    float nextWaveTimer = -1f;

    void Update()
    {
        if (weapon && ammoText)
        {
            if (weapon.clipSize > 0) ammoText.text = $"{weapon.loaded}/{weapon.clipSize} | {weapon.reserve}";
            else ammoText.text = "";
        }

        if (!director) return;

        int wave = ReadField<int>(director, "wave");
        var aliveList = ReadField<List<GameObject>>(director, "alive");

        int alive = 0;
        if (aliveList != null)
        {
            for (int i = aliveList.Count - 1; i >= 0; i--)
                if (aliveList[i]) alive++;
        }

        if (waveText) waveText.text = $"Wave: {Mathf.Max(1, wave)}";
        if (zombiesText) zombiesText.text = $"zombies: {alive}";

        if (alive == 0) { if (nextWaveTimer < 0f) nextWaveTimer = director.timeBetweenWaves; }
        else nextWaveTimer = -1f;

        if (nextWaveText)
        {
            if (nextWaveTimer >= 0f)
            {
                nextWaveTimer -= Time.deltaTime;
                int secs = Mathf.CeilToInt(Mathf.Max(0f, nextWaveTimer));
                nextWaveText.text = $"Next wave in: {secs}";
            }
            else nextWaveText.text = "";
        }
    }

    static T ReadField<T>(object obj, string name)
    {
        var f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f == null) return default;
        var v = f.GetValue(obj);
        if (v == null) return default;
        return (T)v;
    }
}
