// Assets/Scripts/UI/Localization/LocalizationService.cs
using UnityEngine;

public static class LocalizationService
{
    /// Inicializa desde ScriptableObject DB
    public static void Initialize(LocalizationDB db, Language defaultLang = Language.ES)
    {
        if (!db) { Debug.LogWarning("LocalizationService.Initialize(DB): DB is null."); return; }
        Localization.Init(db, defaultLang);
    }

    /// Inicializa desde carpeta JSON en Resources (requiere es.json y en.json)
    public static void Initialize(string resourcesFolder, Language defaultLang = Language.ES)
    {
        if (string.IsNullOrEmpty(resourcesFolder)) resourcesFolder = "Localization";
        Localization.InitFromJsonFolder(resourcesFolder, defaultLang);
    }

    public static void Set(Language lang) => Localization.SetLanguage(lang);
    public static Language Current => Localization.CurrentLang;
    public static string T(string key) => Localization.T(key);
}
