using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]

public class WeaponMountBinder : MonoBehaviour
{
    [Header("Refs")]
    public Animator bodyAnim;                   // Body (lee Dir)
    public Transform itemTransform;             // <<--- ESTO es lo que se mueve (Item)
    public Transform currentWeaponParent;       // Item/CurrentWeapon
    public Transform weaponSockets;             // Player/Visual/WeaponSockets
    public Transform mountDown, mountRight, mountLeft, mountUp;

    [Header("Anchors en el prefab del arma")]
    public string anchorsContainerName = "Anchors";
    public string gripDownName = "Grip_Down";
    public string gripRightName = "Grip_Right";
    public string gripLeftName = "Grip_Left";
    public string gripUpName = "Grip_Up";

    [Header("Params")]
    public string dirParamName = "Dir";         // 0=down,1=right,2=left,3=up

    // cache
    Transform weaponRoot, anchors, gDown, gRight, gLeft, gUp;
    int cachedWeaponId;

    void Reset() { AutoWire(); }
    void Awake() { AutoWire(); }
    void OnValidate() { if (!Application.isPlaying) AutoWire(); }

    void AutoWire()
    {
        if (!itemTransform) itemTransform = transform;

        // Body animator
        if (!bodyAnim)
        {
            var root = transform.root ? transform.root : transform;
            var body = root.Find("Player/Visual/Body") ?? root.Find("Visual/Body");
            if (body) bodyAnim = body.GetComponent<Animator>();
        }

        // CurrentWeapon (crea si falta)
        if (!currentWeaponParent)
        {
            Transform item = (name == "Item") ? transform :
                             (transform.parent ? transform.parent.Find("Item") : null);
            if (item)
            {
                var cw = item.Find("CurrentWeapon");
                if (!cw)
                {
                    var go = new GameObject("CurrentWeapon");
                    cw = go.transform;
                    cw.SetParent(item, false);
                }
                currentWeaponParent = cw;
            }
        }

        // Sockets
        if (!weaponSockets)
        {
            var root = transform.root ? transform.root : transform;
            weaponSockets = root.Find("Player/Visual/WeaponSockets") ?? root.Find("Visual/WeaponSockets");
        }
        if (weaponSockets)
        {
            mountDown = mountDown ? mountDown : weaponSockets.Find("Mount_Down");
            mountRight = mountRight ? mountRight : weaponSockets.Find("Mount_Right");
            mountLeft = mountLeft ? mountLeft : weaponSockets.Find("Mount_Left");
            mountUp = mountUp ? mountUp : weaponSockets.Find("Mount_Up");
        }
    }

    void LateUpdate()
    {
        RefreshAnchorsIfWeaponChanged();
        if (!itemTransform) return;

        var socket = GetSocketForDir(GetDir());
        if (!socket) return;

        var grip = GetGripForDir(GetDir());

        // Queremos: grip == socket ? movemos Item para que coincidan
        Vector3 targetPos;
        if (grip && weaponRoot)                       // método exacto con grip
            targetPos = itemTransform.position + (socket.position - grip.position);
        else                                          // fallback (si no hay grip)
            targetPos = new Vector3(socket.position.x, socket.position.y, itemTransform.position.z);

        itemTransform.position = targetPos;
        itemTransform.rotation = Quaternion.identity;
    }

    // ---- cache anchors por arma instanciada ----
    void RefreshAnchorsIfWeaponChanged()
    {
        var root = GetWeaponRoot();
        int id = root ? root.GetInstanceID() : 0;
        if (id == cachedWeaponId) return;

        cachedWeaponId = id;
        weaponRoot = root;
        anchors = gDown = gRight = gLeft = gUp = null;

        if (!weaponRoot) return;

        anchors = FindDeep(weaponRoot, anchorsContainerName);
        if (!anchors) return;

        gDown = FindDeep(anchors, gripDownName);
        gRight = FindDeep(anchors, gripRightName);
        gLeft = FindDeep(anchors, gripLeftName);
        gUp = FindDeep(anchors, gripUpName);
    }

    Transform GetWeaponRoot()
    {
        if (!currentWeaponParent || currentWeaponParent.childCount == 0) return null;
        return currentWeaponParent.GetChild(0);
    }

    int GetDir()
    {
        return (bodyAnim && !string.IsNullOrEmpty(dirParamName))
            ? bodyAnim.GetInteger(dirParamName)
            : 0;
    }

    Transform GetSocketForDir(int dir)
    {
        switch (dir)
        {
            case 0: return mountDown;
            case 1: return mountRight;
            case 2: return mountLeft;
            case 3: return mountUp;
            default: return mountDown;
        }
    }

    Transform GetGripForDir(int dir)
    {
        switch (dir)
        {
            case 0: return gDown;
            case 1: return gRight;
            case 2: return gLeft;
            case 3: return gUp;
            default: return null;
        }
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (!t) return null;
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r) return r;
        }
        return null;
    }
}
