// Assets/Scripts/UI/Localization/LocalizedText.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[RequireComponent(typeof(Behaviour))]
public class LocalizedText : MonoBehaviour
{
    public string key;
    TMP_Text tmp;
    Text ugui;

    void Awake()
    {
        tmp = GetComponent<TMP_Text>();
        ugui = (!tmp) ? GetComponent<Text>() : null;
    }

    void OnEnable()
    {
        Localization.OnLanguageChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        Localization.OnLanguageChanged -= Refresh;
    }

    public void Refresh()
    {
        var txt = LocalizationService.T(key);
        if (tmp) tmp.text = txt;
        else if (ugui) ugui.text = txt;
    }
}
