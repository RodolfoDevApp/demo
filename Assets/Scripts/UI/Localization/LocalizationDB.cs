// Assets/Scripts/UI/Localization/LocalizationDB.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LocalizationDB", menuName = "Localization/DB")]
public class LocalizationDB : ScriptableObject
{
    [Serializable] public class Entry { public string key; [TextArea] public string es; [TextArea] public string en; }
    public List<Entry> entries = new();
}
