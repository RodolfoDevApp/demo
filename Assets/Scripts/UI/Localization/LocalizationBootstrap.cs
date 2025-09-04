// Assets/Scripts/UI/Localization/LocalizationBootstrap.cs
using UnityEngine;

public class LocalizationBootstrap : MonoBehaviour
{
    public string resourcesFolder = "Localization";
    public Language defaultLanguage = Language.ES;

    void Awake()
    {
        LocalizationService.Initialize(resourcesFolder, defaultLanguage);
    }
}
