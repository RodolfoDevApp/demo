using UnityEngine;

public static class AnimatorExt
{
    public static bool HasParameter(this Animator a, int hash)
    {
        if (!a) return false;
        var ps = a.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].nameHash == hash) return true;
        return false;
    }

    public static void ResetTriggerIfExists(this Animator a, int hash)
    {
        if (a && a.HasParameter(hash)) a.ResetTrigger(hash);
    }
    public static void SetTriggerIfExists(this Animator a, int hash)
    {
        if (a && a.HasParameter(hash)) { a.ResetTrigger(hash); a.SetTrigger(hash); }
    }
    public static void SetBoolIfExists(this Animator a, int hash, bool v)
    {
        if (a && a.HasParameter(hash)) a.SetBool(hash, v);
    }
    public static void SetIntegerIfExists(this Animator a, int hash, int v)
    {
        if (a && a.HasParameter(hash)) a.SetInteger(hash, v);
    }
    public static void SetFloatIfExists(this Animator a, int hash, float v)
    {
        if (a && a.HasParameter(hash)) a.SetFloat(hash, v);
    }
}
