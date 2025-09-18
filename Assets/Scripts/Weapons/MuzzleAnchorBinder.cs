using UnityEngine;

[DisallowMultipleComponent]
public class MuzzleAnchorBinder : MonoBehaviour
{
    [Header("Refs")]
    public Animator bodyAnim;                 // Body (lee Dir)
    public Transform currentWeaponParent;     // Item/CurrentWeapon
    public Transform muzzle;                  // Player/Visual/Item/Muzzle (el que movemos)

    [Header("Anchors (nombres en el prefab del arma)")]
    public string anchorsContainerName = "Anchors";
    public string muzzleDownName = "Muzzle_Down";
    public string muzzleRightName = "Muzzle_Right";
    public string muzzleLeftName = "Muzzle_Left";
    public string muzzleUpName = "Muzzle_Up";

    [Header("Params")]
    public string dirParamName = "Dir";       // 0=down,1=right,2=left,3=up

    void Reset() { AutoWire(); }
    void Awake() { AutoWire(); }

    void AutoWire()
    {
        if (!bodyAnim)
        {
            var root = transform.root ? transform.root : transform;
            var t = root.Find("Player/Visual/Body") ?? root.Find("Visual/Body") ?? transform;
            bodyAnim = t.GetComponent<Animator>() ?? GetComponentInParent<Animator>();
        }

        EnsureCurrentWeaponParent();

        if (!muzzle)
        {
            var m = transform.Find("Muzzle");
            if (!m && transform.parent) m = transform.parent.Find("Muzzle");
            muzzle = m;
        }
    }

    void EnsureCurrentWeaponParent()
    {
        if (currentWeaponParent && currentWeaponParent.name == "CurrentWeapon") return;

        var here = transform.Find("CurrentWeapon");
        if (here) { currentWeaponParent = here; return; }

        var root = transform.root ? transform.root : transform;
        var item = transform.Find("Item")
               ?? root.Find("Player/Visual/Item")
               ?? root.Find("Visual/Item")
               ?? root.Find("Item");

        if (item)
        {
            var cw = item.Find("CurrentWeapon");
            if (!cw)
            {
                var go = new GameObject("CurrentWeapon");
                cw = go.transform;
                cw.SetParent(item, false);
                cw.localPosition = Vector3.zero;
            }
            currentWeaponParent = cw;
        }
    }

    void LateUpdate()
    {
        if (!muzzle) return;

        // coloca el Muzzle EXACTAMENTE en el anchor; sin offsets
        var pos = GetAnchorWorldPos();
        muzzle.position = new Vector3(pos.x, pos.y, muzzle.position.z);
        muzzle.rotation = Quaternion.identity;
    }

    // === API para shooters/shotgun ===
    public Vector3 GetAnchorWorldPos()
    {
        var root = GetWeaponRoot();
        if (!root) return muzzle ? muzzle.position : transform.position;

        var anchors = root.Find(anchorsContainerName);
        if (!anchors) return muzzle ? muzzle.position : transform.position;

        int d = GetDir();
        Transform target = GetMuzzleAnchor(anchors, d);
        if (!target) return muzzle ? muzzle.position : transform.position;

        return target.position; // SIN modificar Y
    }

    Transform GetWeaponRoot()
    {
        if (!currentWeaponParent || currentWeaponParent.childCount == 0) return null;
        return currentWeaponParent.GetChild(0);
    }

    int GetDir() =>
        (bodyAnim && !string.IsNullOrEmpty(dirParamName))
        ? bodyAnim.GetInteger(dirParamName) : 0;

    Transform GetMuzzleAnchor(Transform anchors, int dir) => dir switch
    {
        0 => anchors.Find(muzzleDownName),
        1 => anchors.Find(muzzleRightName),
        2 => anchors.Find(muzzleLeftName),
        3 => anchors.Find(muzzleUpName),
        _ => null
    };
}
