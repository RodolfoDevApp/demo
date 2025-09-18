using UnityEngine;

// Dir mapping del proyecto: 0=down, 1=right, 2=left, 3=up
[RequireComponent(typeof(Animator))]
public class WeaponAnimatorDriver : MonoBehaviour
{
    [Header("Refs (Item)")]
    public Animator anim;
    public Animator bodyAnim;
    public Rigidbody2D playerRb;

    [Tooltip("SpriteRenderer ACTIVO que se usa para ordenar y como 'Source SR' del Muzzle.")]
    public SpriteRenderer itemSR;
    [Tooltip("SpriteRenderer del hijo (p.ej. 'Sprite') para melee.")]
    public SpriteRenderer itemSRChild;
    [Tooltip("SpriteRenderer del root (Item) para firearms.")]
    public SpriteRenderer itemSRRoot;

    public GameObject handsGO;

    [Header("Melee")]
    public MeleeHitbox2D melee;     // en escena (bat o puños)
    public MeleeHitbox2D fists;     // hitbox de puños

    [Header("Config de manos / render")]
    public bool usesOwnHands = true;
    public bool manageHandsFromDriver = false;

    [Header("Sorting")]
    public bool useRelativeSortingToBody = true;
    public SpriteRenderer bodySR;
    public int relativeFrontDelta = +1;
    public int relativeBackDelta = -1;

    [Header("Arma / inventario")]
    public bool isShotgun = true;
    public bool isMelee = false;
    public int clipSize = 4;
    public int loaded = 0;
    public int reserve = 20;

    int shellsToLoad = 0;
    bool isReloading = false;
    bool wasEmpty = false;

    const int DIR_DOWN = 0;
    const int DIR_RIGHT = 1;
    const int DIR_LEFT = 2;
    const int DIR_UP = 3;

    static readonly int P_Dir = Animator.StringToHash("Dir");
    static readonly int P_Speed = Animator.StringToHash("Speed");
    static readonly int P_Shoot = Animator.StringToHash("Shoot");
    static readonly int P_Reload = Animator.StringToHash("Reload");
    static readonly int P_Rack = Animator.StringToHash("Rack");
    static readonly int P_IsReloading = Animator.StringToHash("IsReloading");
    static readonly int P_ShellsToLoad = Animator.StringToHash("ShellsToLoad");
    static readonly int P_WasEmpty = Animator.StringToHash("WasEmpty");
    static readonly int P_Attack = Animator.StringToHash("Attack");

    [Header("Debug")]
    public bool dbgShowGUI = false;
    public KeyCode dbgToggleGUIKey = KeyCode.F3;

    [Header("Muzzle")]
    public MuzzleFlash2D muzzle;              // fogonazo visual
    public bool muzzleViaAnimEvent = false;

    // === Lectura directa de anchors del arma actual ===
    [Header("CurrentWeapon/Anchors (prefab)")]
    public Transform currentWeaponParent;              // Item/CurrentWeapon
    public string anchorsContainerName = "Anchors";
    public string muzzleDownName = "Muzzle_Down";
    public string muzzleRightName = "Muzzle_Right";
    public string muzzleLeftName = "Muzzle_Left";
    public string muzzleUpName = "Muzzle_Up";
    public string gripDownName = "Grip_Down";
    public string gripRightName = "Grip_Right";
    public string gripLeftName = "Grip_Left";
    public string gripUpName = "Grip_Up";

    [Header("WeaponSockets (en escena)")]
    public Transform weaponSockets;            // Player/Visual/WeaponSockets
    public Transform mountDown, mountRight, mountLeft, mountUp;

    // ===== Disparo alternativo (tuyos) =====
    [Header("Disparo alternativo (opcionales)")]
    public WeaponProjectileShooter2D projectileShooter; // pistol/gun
    public ShotgunCone2D shotgunCone;                   // shotgun AOE

    // ---------- Input ----------
    [Header("Input (keys)")]
    public KeyCode firePrimary = KeyCode.LeftControl;
    public KeyCode fireSecondary = KeyCode.RightControl;
    public KeyCode reloadKey = KeyCode.R;

    // ---------- Inventario ----------
    [Header("Ammo / Inventory Bridge (opcional)")]
    public bool useInventoryAmmo = true;
    public InventoryRuntime inventory;
    public WeaponHotbarSimple hotbar;
    public ItemDef ammo9mm;
    public ItemDef ammoRifle;
    public ItemDef ammoShells;

    void Reset()
    {
        anim ??= GetComponent<Animator>();
        if (!itemSRRoot) itemSRRoot = GetComponent<SpriteRenderer>();

        if (!itemSRChild)
        {
            var srs = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
            {
                if (sr == itemSRRoot) continue;
                if (muzzle && sr == muzzle.sr) continue;
                itemSRChild = sr; break;
            }
        }

        if (!playerRb) playerRb = GetComponentInParent<Rigidbody2D>();
        if (!bodyAnim) bodyAnim = GetComponentInParent<Animator>();
        if (!bodySR) bodySR = GetComponentInParent<SpriteRenderer>();
        if (!melee) melee = GetComponentInChildren<MeleeHitbox2D>(true);

        AutoWireWeaponRoots();
        if (!projectileShooter) projectileShooter = GetComponent<WeaponProjectileShooter2D>();
        if (!shotgunCone) shotgunCone = GetComponent<ShotgunCone2D>();
    }

    void Awake()
    {
        if (!anim) anim = GetComponent<Animator>();
        if (!itemSRRoot) itemSRRoot = GetComponent<SpriteRenderer>();
        if (!itemSR) itemSR = itemSRRoot ? itemSRRoot : GetComponent<SpriteRenderer>();
        if (muzzle && itemSR) muzzle.sourceSR = itemSR;

        if (!melee) melee = GetComponentInChildren<MeleeHitbox2D>(true);

        if (manageHandsFromDriver) ApplyHandsVisibility(true);

        AutoWireWeaponRoots();

        if (!projectileShooter) projectileShooter = GetComponent<WeaponProjectileShooter2D>();
        if (!shotgunCone) shotgunCone = GetComponent<ShotgunCone2D>();
    }

    void AutoWireWeaponRoots()
    {
        if (!currentWeaponParent)
        {
            var item = transform; // este script vive en Item
            var cw = item.Find("CurrentWeapon");
            if (!cw && item.parent) cw = item.parent.Find("CurrentWeapon");
            currentWeaponParent = cw;
        }

        if (!weaponSockets)
        {
            var root = transform.root ? transform.root : transform;
            weaponSockets = root.Find("Player/Visual/WeaponSockets") ?? root.Find("Visual/WeaponSockets");
        }
        if (weaponSockets)
        {
            if (!mountDown) mountDown = weaponSockets.Find("Mount_Down");
            if (!mountRight) mountRight = weaponSockets.Find("Mount_Right");
            if (!mountLeft) mountLeft = weaponSockets.Find("Mount_Left");
            if (!mountUp) mountUp = weaponSockets.Find("Mount_Up");
        }
    }

    void Start()
    {
        UseChildSpriteRenderer(isMelee && itemSRChild);
    }

    void OnEnable() { if (manageHandsFromDriver) ApplyHandsVisibility(true); }
    void OnDisable() { if (manageHandsFromDriver) ApplyHandsVisibility(false); }

    void Update()
    {
        if (Input.GetKeyDown(dbgToggleGUIKey)) dbgShowGUI = !dbgShowGUI;

        int dirForAnim = GetDir();
        SetIntegerIfExists(anim, P_Dir, dirForAnim);
        SetFloatIfExists(anim, P_Speed, GetSpeed());

        if (useInventoryAmmo) SyncReserveFromInventory();

        if (Input.GetKeyDown(firePrimary) || Input.GetKeyDown(fireSecondary))
            TryShootOrAttack();

        if (Input.GetKeyDown(reloadKey))
            TryStartReload();
    }

    void LateUpdate()
    {
        int dir = GetCurrentDirFromAnim();
        if (useRelativeSortingToBody && bodySR && itemSR)
        {
            itemSR.sortingLayerID = bodySR.sortingLayerID;
            itemSR.sortingOrder = (dir == DIR_UP)
                ? bodySR.sortingOrder + relativeBackDelta
                : bodySR.sortingOrder + relativeFrontDelta;
        }
    }

    // --- Helpers de combate ---
    public void EnableFists() { isMelee = true; if (fists) melee = fists; }
    public void UseMeleeHitbox(MeleeHitbox2D hb) { isMelee = true; if (hb) melee = hb; }
    public void DisableMelee() { isMelee = false; }

    // --- SR switcher ---
    public void UseChildSpriteRenderer(bool useChild)
    {
        var target = (useChild && itemSRChild) ? itemSRChild : itemSRRoot;
        itemSR = target;
        if (muzzle && itemSR) muzzle.sourceSR = itemSR;

        if (!Application.isPlaying) return;
        if (itemSRRoot) itemSRRoot.enabled = (target == itemSRRoot);
        if (itemSRChild) itemSRChild.enabled = (target == itemSRChild);
    }

    // ---------- Disparo / Ataque ----------
    void TryShootOrAttack()
    {
        if (isReloading) return;

        if (isMelee)
        {
            SetTriggerIfExists(anim, P_Attack);
            return;
        }

        if (loaded <= 0) return;
        loaded--;
        SetTriggerIfExists(anim, P_Shoot);

        if (!muzzleViaAnimEvent && muzzle) muzzle.Play(GetCurrentDirFromAnim(), bodySR);
    }

    // ---------- Recarga ----------
    void TryStartReload()
    {
        if (isMelee) return;

        if (useInventoryAmmo && GetCurrentAmmoDef() != null)
        {
            reserve = GetCurrentAmmoCountSafe();
            if (reserve <= 0) return;
        }

        if (!isShotgun)
        {
            if (loaded >= clipSize || reserve <= 0) return;
            wasEmpty = (loaded == 0);
            int need = clipSize - loaded;

            int available = useInventoryAmmo ? GetCurrentAmmoCountSafe() : reserve;
            int add = Mathf.Min(need, available);
            if (add <= 0) return;

            loaded += add;
            reserve = Mathf.Max(0, available - add);
            if (useInventoryAmmo) ConsumeAmmoFromInventory(add);

            SetBoolIfExists(anim, P_WasEmpty, wasEmpty);
            SetTriggerIfExists(anim, P_Reload);
            return;
        }

        if (isReloading || loaded >= clipSize || reserve <= 0) return;

        wasEmpty = (loaded == 0);
        int canLoad = clipSize - loaded;
        int availableShells = useInventoryAmmo ? GetCurrentAmmoCountSafe() : reserve;
        shellsToLoad = Mathf.Min(canLoad, availableShells);
        if (shellsToLoad <= 0) return;

        isReloading = true;

        SetBoolIfExists(anim, P_IsReloading, true);
        SetIntegerIfExists(anim, P_ShellsToLoad, shellsToLoad);
        SetBoolIfExists(anim, P_WasEmpty, wasEmpty);
        SetTriggerIfExists(anim, P_Reload);
    }

    // ---------- Animation Events ----------
    public void AE_InsertShell()
    {
        if (!isShotgun || !isReloading) return;

        if (shellsToLoad > 0)
        {
            if (useInventoryAmmo)
            {
                if (!ConsumeAmmoFromInventory(1))
                {
                    shellsToLoad = 0;
                    isReloading = false;
                    SetIntegerIfExists(anim, P_ShellsToLoad, 0);
                    SetBoolIfExists(anim, P_IsReloading, false);
                    return;
                }
                reserve = GetCurrentAmmoCountSafe();
            }
            else
            {
                if (reserve <= 0) return;
                reserve--;
            }

            shellsToLoad--; loaded++;
            SetIntegerIfExists(anim, P_ShellsToLoad, shellsToLoad);
        }

        if (shellsToLoad <= 0)
        {
            isReloading = false;
            SetIntegerIfExists(anim, P_ShellsToLoad, 0);
            SetBoolIfExists(anim, P_IsReloading, false);
            if (wasEmpty) SetTriggerIfExists(anim, P_Rack);
        }
    }

    public void AE_Muzzle()
    {
        if (isMelee) return;

        // Fogonazo visual
        if (muzzle) muzzle.Play(GetCurrentDirFromAnim(), bodySR);

        // === ORIGEN CORRECTO, independiente del orden de Update/LateUpdate ===
        Vector2 origin = (Vector2)GetMuzzleWorldPos_Deterministic();

        int d = GetCurrentDirFromAnim();
        Vector2 dir = DirToVector(d);
        GameObject owner = transform.root ? transform.root.gameObject : gameObject;

        if (isShotgun)
        {
            if (shotgunCone && shotgunCone.enabled)
                shotgunCone.Fire(origin, dir, owner);
        }
        else
        {
            if (projectileShooter && projectileShooter.enabled)
                projectileShooter.Fire();
        }

        Debug.DrawLine(origin, origin + dir * 0.5f, Color.magenta, 0.1f);
    }

    // --- Helpers ---
    Vector2 DirToVector(int d)
    {
        switch (d)
        {
            case 0: return Vector2.down;
            case 1: return Vector2.right;
            case 2: return Vector2.left;
            case 3: return Vector2.up;
            default: return Vector2.right;
        }
    }

    public void AE_MeleeStart()
    {
        if (!isMelee || !melee) return;
        int dir = GetCurrentDirFromAnim();
        melee.Begin(dir);
    }

    public void AE_MeleeStop()
    {
        if (!melee) return;
        melee.End();
    }

    // ---------- Inventario ----------
    void SyncReserveFromInventory()
    {
        var def = GetCurrentAmmoDef();
        if (!inventory || !def) return;
        reserve = inventory.Count(def);
    }

    ItemDef GetCurrentAmmoDef()
    {
        if (!hotbar) return null;
        string wid = hotbar.CurrentItemId;
        if (string.IsNullOrEmpty(wid)) return null;

        if (wid == hotbar.idPistol) return ammo9mm;
        if (wid == hotbar.idGun) return ammoRifle;
        if (wid == hotbar.idShotgun) return ammoShells;
        return null;
    }

    int GetCurrentAmmoCountSafe()
    {
        var def = GetCurrentAmmoDef();
        if (!inventory || !def) return reserve;
        return inventory.Count(def);
    }

    bool ConsumeAmmoFromInventory(int amount)
    {
        var def = GetCurrentAmmoDef();
        if (!inventory || !def || amount <= 0) return false;
        int before = inventory.Count(def);
        if (before < amount) return false;
        bool ok = inventory.TryRemove(def, amount);
        if (ok) reserve = Mathf.Max(0, before - amount);
        return ok;
    }

    // ---------- Utilidades ----------
    void ApplyHandsVisibility(bool enablePhase)
    {
        if (!handsGO) return;
        bool show = !usesOwnHands && enablePhase;
        handsGO.SetActive(show);
    }

    int GetDir()
    {
        if (HasParam(anim, P_Dir) && bodyAnim && HasParam(bodyAnim, P_Dir))
            return bodyAnim.GetInteger(P_Dir);

        Vector2 v = playerRb ? playerRb.linearVelocity : Vector2.zero;
        if (v.sqrMagnitude < 0.0001f)
            return HasParam(anim, P_Dir) ? anim.GetInteger(P_Dir) : DIR_DOWN;

        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            return (v.x >= 0f) ? DIR_RIGHT : DIR_LEFT;
        else
            return (v.y >= 0f) ? DIR_UP : DIR_DOWN;
    }

    public int GetCurrentDirFromAnim()
    {
        if (bodyAnim && HasParam(bodyAnim, P_Dir))
            return bodyAnim.GetInteger(P_Dir);
        return HasParam(anim, P_Dir) ? anim.GetInteger(P_Dir) : DIR_DOWN;
    }

    float GetSpeed() => playerRb ? playerRb.linearVelocity.magnitude : 0f;

    void OnGUI()
    {
        if (!dbgShowGUI) return;
        GUI.Box(new Rect(10, 10, 200, 80), "Weapon Debug");
        GUILayout.BeginArea(new Rect(15, 35, 180, 60));
        GUILayout.Label($"loaded/clip: {loaded}/{clipSize}");
        GUILayout.Label($"reserve: {reserve}");
        GUILayout.EndArea();
    }

    void OnValidate()
    {
        if (!itemSRRoot) itemSRRoot = GetComponent<SpriteRenderer>();

        if (!itemSRChild)
        {
            var srs = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
            {
                if (sr == itemSRRoot) continue;
                if (muzzle && sr == muzzle.sr) continue;
                itemSRChild = sr; break;
            }
        }

        if (!bodyAnim) bodyAnim = GetComponentInParent<Animator>();
        if (!bodySR) bodySR = GetComponentInParent<SpriteRenderer>();
        if (!melee) melee = GetComponentInChildren<MeleeHitbox2D>(true);

        AutoWireWeaponRoots();

        if (!projectileShooter) projectileShooter = GetComponent<WeaponProjectileShooter2D>();
        if (!shotgunCone) shotgunCone = GetComponent<ShotgunCone2D>();
    }

    // ===== Helpers locales =====
    static bool HasParam(Animator a, int hash)
    {
        if (!a) return false;
        foreach (var p in a.parameters) if (p.nameHash == hash) return true;
        return false;
    }
    static void SetTriggerIfExists(Animator a, int h) { if (a && HasParam(a, h)) a.SetTrigger(h); }
    static void SetBoolIfExists(Animator a, int h, bool v) { if (a && HasParam(a, h)) a.SetBool(h, v); }
    static void SetIntegerIfExists(Animator a, int h, int v) { if (a && HasParam(a, h)) a.SetInteger(h, v); }
    static void SetFloatIfExists(Animator a, int h, float v) { if (a && HasParam(a, h)) a.SetFloat(h, v); }

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

    Transform GetWeaponRoot()
    {
        if (!currentWeaponParent || currentWeaponParent.childCount == 0) return null;
        return currentWeaponParent.GetChild(0);
    }

    Transform GetAnchors()
    {
        var root = GetWeaponRoot();
        if (!root) return null;
        var a = root.Find(anchorsContainerName);
        if (!a) a = FindDeep(root, anchorsContainerName);
        return a;
    }

    Transform GetGripForDir(Transform anchors, int dir)
    {
        if (!anchors) return null;
        switch (dir)
        {
            case 0: return anchors.Find(gripDownName);
            case 1: return anchors.Find(gripRightName);
            case 2: return anchors.Find(gripLeftName);
            case 3: return anchors.Find(gripUpName);
            default: return null;
        }
    }

    Transform GetMuzzleForDir(Transform anchors, int dir)
    {
        if (!anchors) return null;
        switch (dir)
        {
            case 0: return anchors.Find(muzzleDownName);
            case 1: return anchors.Find(muzzleRightName);
            case 2: return anchors.Find(muzzleLeftName);
            case 3: return anchors.Find(muzzleUpName);
            default: return null;
        }
    }

    Transform GetSocketForDir(int dir)
    {
        switch (dir)
        {
            case 0: return mountDown;
            case 1: return mountRight;
            case 2: return mountLeft;
            case 3: return mountUp;
            default: return null;
        }
    }

    // === POSICIÓN DETERMINÍSTICA: socket + (muzzle_local - grip_local) ===
    public Vector3 GetMuzzleWorldPos_Deterministic()
    {
        int dir = GetCurrentDirFromAnim();
        var anchors = GetAnchors();
        var grip = GetGripForDir(anchors, dir);
        var muz = GetMuzzleForDir(anchors, dir);
        var sock = GetSocketForDir(dir);

        if (anchors && grip && muz && sock)
        {
            // delta en espacio local del prefab del arma (constante)
            Vector3 delta = muz.localPosition - grip.localPosition;

            // asumiendo sin rotaciones raras: sumamos el delta al socket world
            var p = sock.position + delta;
            return new Vector3(p.x, p.y, 0f);
        }

        // Fallbacks
        if (muzzle) return muzzle.transform.position;
        return transform.position;
    }

    // (dejo también la lectura "directa" por si la quieres usar en otros scripts)
    public Vector3 GetMuzzleWorldPosFromCurrentWeapon()
    {
        int dir = GetCurrentDirFromAnim();
        var anchors = GetAnchors();
        var muz = GetMuzzleForDir(anchors, dir);
        if (muz) return new Vector3(muz.position.x, muz.position.y, 0f);
        if (muzzle) return muzzle.transform.position;
        return transform.position;
    }
}
