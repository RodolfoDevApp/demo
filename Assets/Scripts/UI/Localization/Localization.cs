// Assets/Scripts/UI/Localization/Localization.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using MiniJSON;

public static class Localization
{
    public static event Action OnLanguageChanged;

    static Dictionary<string, string> _es = new(StringComparer.OrdinalIgnoreCase);
    static Dictionary<string, string> _en = new(StringComparer.OrdinalIgnoreCase);
    static Language _current = Language.ES;

    public static Language CurrentLang => _current;

    // ----- Inicialización -----
    public static void Init(LocalizationDB db, Language defaultLang)
    {
        _es.Clear(); _en.Clear();
        if (db != null)
        {
            foreach (var e in db.entries)
            {
                if (string.IsNullOrEmpty(e.key)) continue;
                if (!_es.ContainsKey(e.key)) _es[e.key] = string.IsNullOrEmpty(e.es) ? e.en : e.es;
                if (!_en.ContainsKey(e.key)) _en[e.key] = string.IsNullOrEmpty(e.en) ? e.es : e.en;
            }
        }
        SetLanguage(defaultLang);
    }

    // Carga JSON: Resources/<folder>/es.json y en.json (objeto plano: { "key":"value", ... })
    public static void InitFromJsonFolder(string folder, Language defaultLang)
    {
        _es = LoadJson($"{folder}/es");
        _en = LoadJson($"{folder}/en");
        SetLanguage(defaultLang);
    }

    static Dictionary<string, string> LoadJson(string resPathNoExt)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ta = Resources.Load<TextAsset>(resPathNoExt);
        if (!ta) { Debug.LogWarning($"Localization: no JSON at Resources/{resPathNoExt}.json"); return dict; }

        var obj = Json.Deserialize(ta.text) as Dictionary<string, object>;
        if (obj == null) { Debug.LogWarning($"Localization: JSON malformed at {resPathNoExt}"); return dict; }

        foreach (var kv in obj)
        {
            if (kv.Value is string s) dict[kv.Key] = s;
            else if (kv.Value != null) dict[kv.Key] = kv.Value.ToString();
        }
        return dict;
    }

    // ----- Uso -----
    public static void SetLanguage(Language lang)
    {
        _current = lang;
        OnLanguageChanged?.Invoke();
    }

    public static string T(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        var map = (_current == Language.EN) ? _en : _es;
        if (map != null && map.TryGetValue(key, out var s) && !string.IsNullOrEmpty(s)) return s;
        // fallback al otro idioma
        var other = (_current == Language.EN) ? _es : _en;
        if (other != null && other.TryGetValue(key, out var s2) && !string.IsNullOrEmpty(s2)) return s2;
        return key;
    }
}
