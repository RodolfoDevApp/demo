using UnityEngine;

[DefaultExecutionOrder(5000)]
public class WeaponHotbarSimple : MonoBehaviour
{
    [System.Serializable]
    public struct DirectionalOffsets { public Vector2 down, up, left, right; public bool swapLeftRight; }
    enum Slot { None, Pistol, Gun, Shotgun, Bat }
    enum Dir { Down, Up, Left, Right }

    [Header("Refs")]
    public WeaponAnimatorDriver driver;
    public Animator itemAnimator;
    public GameObject itemGO;
    public GameObject handsGO;

    [Header("Muzzle (opcional)")]
    public MuzzleFlash2D muzzle;

    WeaponHitscan2D hitscan;
    MeleeHitbox2D meleeHitbox;

    [Header("Anchor del fogonazo")]
    [SerializeField] Transform muzzlePoint;

    [Header("ItemDef IDs (map de equip)")]
    public string idPistol = "pistol";
    public string idGun = "gun";
    public string idShotgun = "shotgun";
    public string idBat = "bat";

    [Header("Overrides (AOC)")]
    public AnimatorOverrideController pistolAOC;
    public AnimatorOverrideController gunAOC;
    public AnimatorOverrideController shotgunAOC;
    [Header("Melee (Bat)")]
    public AnimatorOverrideController meleeBatAOC;

    [Header("Pistol")]
    public int pistolClipSize = 10;
    public int pistolStartLoaded = 10;
    public int pistolStartReserve = 60;
    public DirectionalOffsets pistolItemOffsets;
    public DirectionalOffsets pistolMuzzleNudges;

    [Header("Gun/Rifle")]
    public int gunClipSize = 30;
    public int gunStartLoaded = 30;
    public int gunStartReserve = 120;
    public DirectionalOffsets gunItemOffsets;
    public DirectionalOffsets gunMuzzleNudges;

    [Header("Shotgun")]
    public int shotgunClipSize = 4;
    public int shotgunStartLoaded = 0;
    public int shotgunStartReserve = 20;
    public DirectionalOffsets shotgunItemOffsets;
    public DirectionalOffsets shotgunMuzzleNudges;

    [Header("Bat")]
    public bool batUsesOwnHands = true;

    int pistolLoaded, pistolReserve;
    int gunLoaded, gunReserve;
    int shotgunLoaded, shotgunReserve;

    Slot current = Slot.None;
    public bool IsHandsMode => current == Slot.None;

    // ---- NUEVO: estado por id para toggling ----
    public string CurrentItemId { get; private set; } = "";   // vacío = manos
    public bool IsEquippedId(string id) => !IsHandsMode && !string.IsNullOrEmpty(id) && id == CurrentItemId;

    static readonly int P_Reload = Animator.StringToHash("Reload");
    static readonly int P_Shoot = Animator.StringToHash("Shoot");
    static readonly int P_Rack = Animator.StringToHash("Rack");

    const string AX = "AimX", AY = "AimY";
    const string DX = "DirX", DY = "DirY";
    const string MX = "MoveX", MY = "MoveY";
    const string FX = "Facing"; const string DIR = "Direction";
    const string BUP = "Up", BDOWN = "Down", BLEFT = "Left", BRIGHT = "Right";

    [Header("Debug / Controles directos")]
    public bool allowDebugNumberKeys = false; // deja en false para no forzar armas

    void Awake()
    {
        if (!driver) driver = GetComponentInChildren<WeaponAnimatorDriver>(true);
        if (!itemAnimator && driver) itemAnimator = driver.anim;
        if (!itemGO && driver) itemGO = driver.gameObject;
        if (!muzzle && driver) muzzle = driver.muzzle;

        if (driver) hitscan = driver.GetComponent<WeaponHitscan2D>();
        meleeHitbox = GetComponentInChildren<MeleeHitbox2D>(true);

        pistolLoaded = pistolStartLoaded; pistolReserve = pistolStartReserve;
        gunLoaded = gunStartLoaded; gunReserve = gunStartReserve;
        shotgunLoaded = shotgunStartLoaded; shotgunReserve = shotgunStartReserve;

        Unequip(); // empezar en manos
    }

    void Update()
    {
        // E SIEMPRE desequipa (solicitado)
        if (Input.GetKeyDown(KeyCode.E)) Unequip();

        if (allowDebugNumberKeys)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) Equip(Slot.Pistol, idPistol);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Equip(Slot.Gun, idGun);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Equip(Slot.Shotgun, idShotgun);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Equip(Slot.Bat, idBat);
        }
    }

    void LateUpdate()
    {
        if (current == Slot.None || current == Slot.Bat) return;
        var anchor = FindOrCreateMuzzlePoint();
        var sr = GetActiveWeaponSR();
        if (!anchor || !sr || !sr.sprite) return;

        var d = DetectDir();
        var b = sr.sprite.bounds;
        Vector3 basePos = d switch
        {
            Dir.Right => new Vector3(b.extents.x, 0f, 0f),
            Dir.Left => new Vector3(-b.extents.x, 0f, 0f),
            Dir.Up => new Vector3(0f, b.extents.y, 0f),
            _ => new Vector3(0f, -b.extents.y, 0f)
        };
        anchor.localPosition = basePos;
    }

    // --------- NUEVO: toggle por id desde UI ---------
    public void EquipByItemId(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        // Si ya está equipada la misma → toggle a manos
        if (IsEquippedId(id)) { Unequip(); return; }

        if (id == idPistol) { Equip(Slot.Pistol, idPistol); return; }
        if (id == idGun) { Equip(Slot.Gun, idGun); return; }
        if (id == idShotgun) { Equip(Slot.Shotgun, idShotgun); return; }
        if (id == idBat) { Equip(Slot.Bat, idBat); return; }

        // No reconocido (munición, consumible, etc.) → no tocar equip
    }

    // Mantengo API existente por índice por si algo legacy lo usa
    public void Equip(int slotIndex) => Equip((Slot)slotIndex, GuessIdFromSlot((Slot)slotIndex));

    string GuessIdFromSlot(Slot s) => s switch
    {
        Slot.Pistol => idPistol,
        Slot.Gun => idGun,
        Slot.Shotgun => idShotgun,
        Slot.Bat => idBat,
        _ => ""
    };

    void Equip(Slot slot, string idForState)
    {
        bool itemIsOff = itemGO && !itemGO.activeSelf;
        if (slot == current && !itemIsOff) return;

        SaveCurrentAmmo();
        if (muzzle) muzzle.Hide();
        if (itemGO) itemGO.SetActive(true);

        if (handsGO) handsGO.SetActive(false);
        if (driver) driver.UseChildSpriteRenderer(false);

        SetCombatMode(isMelee: (slot == Slot.Bat));

        switch (slot)
        {
            case Slot.Pistol:
                if (itemAnimator && pistolAOC) itemAnimator.runtimeAnimatorController = pistolAOC;
                ApplyToDriver(false, pistolClipSize, pistolLoaded, pistolReserve, pistolItemOffsets);
                ApplyMuzzleNudges(pistolMuzzleNudges);
                AttachMuzzleToAnchor();
                ForceRangedOffsets();
                ClearDriverTriggersSafe();
                break;

            case Slot.Gun:
                if (itemAnimator && gunAOC) itemAnimator.runtimeAnimatorController = gunAOC;
                ApplyToDriver(false, gunClipSize, gunLoaded, gunReserve, gunItemOffsets);
                ApplyMuzzleNudges(gunMuzzleNudges);
                AttachMuzzleToAnchor();
                ForceRangedOffsets();
                ClearDriverTriggersSafe();
                break;

            case Slot.Shotgun:
                if (itemAnimator && shotgunAOC) itemAnimator.runtimeAnimatorController = shotgunAOC;
                ApplyToDriver(true, shotgunClipSize, shotgunLoaded, shotgunReserve, shotgunItemOffsets);
                ApplyMuzzleNudges(shotgunMuzzleNudges);
                AttachMuzzleToAnchor();
                ForceRangedOffsets();
                ClearDriverTriggersSafe();
                break;

            case Slot.Bat:
                if (itemAnimator && meleeBatAOC) itemAnimator.runtimeAnimatorController = meleeBatAOC;
                ApplyToDriver(false, 0, 0, 0, default);
                driver.isShotgun = false;
                driver.isMelee = true;

                driver.useAutoOffsets = false;
                driver.offsetDown = new Vector2(-0.08f, 0f);
                driver.offsetUp = new Vector2(0.09f, 0f);
                driver.offsetLeft = new Vector2(0.06f, 0f);
                driver.offsetRight = new Vector2(-0.08f, 0f);
                driver.swapLeftRight = false;

                driver.usesOwnHands = batUsesOwnHands;
                if (handsGO) handsGO.SetActive(!batUsesOwnHands);

                if (muzzle) muzzle.Hide();
                ClearDriverTriggersSafe();
                break;
        }

        current = slot;
        CurrentItemId = idForState ?? "";
    }

    public void Unequip()
    {
        SaveCurrentAmmo();

        if (muzzle) muzzle.Hide();
        if (handsGO) handsGO.SetActive(true);
        ClearDriverTriggersSafe();
        if (itemGO) itemGO.SetActive(false);

        if (driver)
        {
            driver.usesOwnHands = false;
            driver.isShotgun = false;
            driver.isMelee = false;
            driver.clipSize = 0;
            driver.loaded = 0;
            driver.reserve = 0;

            driver.useAutoOffsets = false;
            driver.offsetDown = Vector2.zero;
            driver.offsetUp = Vector2.zero;
            driver.offsetLeft = Vector2.zero;
            driver.offsetRight = Vector2.zero;
            driver.swapLeftRight = false;

            driver.UseChildSpriteRenderer(false);
        }

        SetCombatMode(isMelee: false);
        if (hitscan) hitscan.enabled = false;
        if (meleeHitbox) meleeHitbox.enabled = false;

        current = Slot.None;
        CurrentItemId = "";
    }

    void SaveCurrentAmmo()
    {
        if (!driver) return;
        switch (current)
        {
            case Slot.Pistol: pistolLoaded = driver.loaded; pistolReserve = driver.reserve; break;
            case Slot.Gun: gunLoaded = driver.loaded; gunReserve = driver.reserve; break;
            case Slot.Shotgun: shotgunLoaded = driver.loaded; shotgunReserve = driver.reserve; break;
        }
    }

    void ApplyToDriver(bool isShotgun, int clip, int loaded, int reserve, DirectionalOffsets offs)
    {
        if (!driver) return;
        driver.isShotgun = isShotgun;
        driver.isMelee = false;
        driver.clipSize = clip;
        driver.loaded = Mathf.Clamp(loaded, 0, clip);
        driver.reserve = Mathf.Max(0, reserve);
        driver.usesOwnHands = true;

        driver.offsetDown = offs.down;
        driver.offsetUp = offs.up;
        driver.offsetLeft = offs.left;
        driver.offsetRight = offs.right;
        driver.swapLeftRight = offs.swapLeftRight;
    }

    void ApplyMuzzleNudges(DirectionalOffsets m)
    {
        if (!muzzle) return;
        muzzle.ClearLocalPositions();
        muzzle.SetNudges(m.down, m.up, m.left, m.right);
    }

    void ForceRangedOffsets()
    {
        if (!driver) return;
        driver.useAutoOffsets = false;
        driver.offsetDown = new Vector2(0f, -0.18f);
        driver.offsetUp = new Vector2(0f, 0.15f);
        driver.offsetLeft = Vector2.zero;
        driver.offsetRight = Vector2.zero;
        driver.swapLeftRight = false;

        if (handsGO) handsGO.SetActive(false);
        driver.usesOwnHands = true;
        driver.UseChildSpriteRenderer(false);
    }

    void ClearDriverTriggersSafe()
    {
        if (!driver || !driver.anim) return;
        var a = driver.anim;
        if (!a.isActiveAndEnabled) return;

        ResetTriggerIfExists(a, P_Reload);
        ResetTriggerIfExists(a, P_Shoot);
        ResetTriggerIfExists(a, P_Rack);

        a.Rebind();
        a.Update(0f);
    }

    static void ResetTriggerIfExists(Animator a, int hash)
    {
        if (!a) return;
        foreach (var p in a.parameters)
            if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Trigger)
            { a.ResetTrigger(hash); break; }
    }

    SpriteRenderer GetActiveWeaponSR() => driver ? driver.itemSRRoot : null;

    Transform FindOrCreateMuzzlePoint()
    {
        if (muzzlePoint) return muzzlePoint;
        var sr = GetActiveWeaponSR();
        if (!sr) return null;

        var found = sr.transform.Find("MuzzlePoint");
        if (found) return muzzlePoint = found;

        var go = new GameObject("MuzzlePoint");
        var t = go.transform; t.SetParent(sr.transform, false);
        if (sr.sprite)
        { var b = sr.sprite.bounds; t.localPosition = new Vector3(b.extents.x, 0f, 0f); }
        else t.localPosition = new Vector3(0.2f, 0f, 0f);
        return muzzlePoint = t;
    }

    void AttachMuzzleToAnchor()
    {
        var anchor = FindOrCreateMuzzlePoint();
        if (!muzzle || !anchor) return;
        muzzle.transform.SetParent(anchor, false);
        muzzle.transform.localPosition = Vector3.zero;
        muzzle.transform.localRotation = Quaternion.identity;
        muzzle.transform.localScale = Vector3.one;
    }

    Dir DetectDir()
    {
        if (driver && driver.bodyAnim && HasParam(driver.bodyAnim, "Dir"))
        {
            int d = driver.bodyAnim.GetInteger("Dir");
            return d switch { 0 => Dir.Down, 1 => Dir.Right, 2 => Dir.Left, 3 => Dir.Up, _ => Dir.Right };
        }

        if (TryGetFloat(itemAnimator, AX, out float ax) || TryGetFloat(itemAnimator, DX, out ax) || TryGetFloat(itemAnimator, MX, out ax))
        {
            float ay; if (!TryGetFloat(itemAnimator, AY, out ay) && !TryGetFloat(itemAnimator, DY, out ay) && !TryGetFloat(itemAnimator, MY, out ay)) ay = 0f;
            if (Mathf.Abs(ax) >= Mathf.Abs(ay)) return ax >= 0f ? Dir.Right : Dir.Left;
            return ay >= 0f ? Dir.Up : Dir.Down;
        }

        if (TryGetInt(itemAnimator, FX, out int f) || TryGetInt(itemAnimator, DIR, out f))
        {
            return f switch { 0 => Dir.Down, 1 => Dir.Left, 2 => Dir.Right, 3 => Dir.Up, _ => Dir.Right };
        }

        if (TryGetBool(itemAnimator, BUP, out bool bu) && bu) return Dir.Up;
        if (TryGetBool(itemAnimator, BDOWN, out bool bd) && bd) return Dir.Down;
        if (TryGetBool(itemAnimator, BLEFT, out bool bl) && bl) return Dir.Left;
        if (TryGetBool(itemAnimator, BRIGHT, out bool br) && br) return Dir.Right;

        return Dir.Right;
    }

    static bool HasParam(Animator a, string name) { if (!a) return false; foreach (var p in a.parameters) if (p.name == name) return true; return false; }
    static bool TryGetFloat(Animator a, string p, out float v) { v = 0f; if (!a) return false; foreach (var pm in a.parameters) if (pm.type == AnimatorControllerParameterType.Float && pm.name == p) { v = a.GetFloat(p); return true; } return false; }
    static bool TryGetInt(Animator a, string p, out int v) { v = 0; if (!a) return false; foreach (var pm in a.parameters) if (pm.type == AnimatorControllerParameterType.Int && pm.name == p) { v = a.GetInteger(p); return true; } return false; }
    static bool TryGetBool(Animator a, string p, out bool v) { v = false; if (!a) return false; foreach (var pm in a.parameters) if (pm.type == AnimatorControllerParameterType.Bool && pm.name == p) { v = a.GetBool(p); return true; } return false; }

    void SetCombatMode(bool isMelee)
    {
        if (hitscan) hitscan.enabled = !isMelee;
        if (meleeHitbox) meleeHitbox.enabled = isMelee;
    }
}
