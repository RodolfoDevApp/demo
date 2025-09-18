using UnityEngine;

[DefaultExecutionOrder(5000)]
public class WeaponHotbarSimple : MonoBehaviour
{
    enum Slot { None, Pistol, Gun, Shotgun, Bat }

    [Header("Refs")]
    public Transform currentWeaponParent;      // PARENT donde van los prefabs (Item/CurrentWeapon)
    public WeaponAnimatorDriver driver;        // Item/WeaponAnimatorDriver
    public Animator itemAnimator;              // Animator del Item
    public GameObject itemGO;                  // GO: Item
    public GameObject handsGO;                 // GO: Hands

    [Header("Muzzle (opcional)")]
    public MuzzleFlash2D muzzle;

    // ====== NUEVO: refs a sistemas de disparo ======
    WeaponProjectileShooter2D projectileShooter;
    ShotgunCone2D shotgunCone;
    WeaponHitscan2D hitscan;

    [Header("Weapon Prefabs (con 'Anchors/*')")]
    public GameObject pistolPrefab;
    public GameObject riflePrefab;
    public GameObject shotgunPrefab;
    public GameObject batPrefab;               // opcional; si es null, usa el modo melee sin prefab

    [Header("ItemDef IDs (map de equip)")]
    public string idPistol = "pistol";
    public string idGun = "gun";
    public string idShotgun = "shotgun";
    public string idBat = "bat";

    [Header("Overrides (AOC)")]
    public AnimatorOverrideController pistolAOC;
    public AnimatorOverrideController gunAOC;
    public AnimatorOverrideController shotgunAOC;
    public AnimatorOverrideController meleeBatAOC;

    [Header("Munición inicial")]
    public int pistolClipSize = 10;
    public int pistolStartLoaded = 10;
    public int pistolStartReserve = 60;

    public int gunClipSize = 30;
    public int gunStartLoaded = 30;
    public int gunStartReserve = 120;

    public int shotgunClipSize = 4;
    public int shotgunStartLoaded = 0;
    public int shotgunStartReserve = 20;

    [Header("Bat")]
    public bool batUsesOwnHands = true;

    int pistolLoaded, pistolReserve;
    int gunLoaded, gunReserve;
    int shotgunLoaded, shotgunReserve;

    Slot current = Slot.None;
    public bool IsHandsMode => current == Slot.None;

    public string CurrentItemId { get; private set; } = "";
    public bool IsEquippedId(string id) => !IsHandsMode && !string.IsNullOrEmpty(id) && id == CurrentItemId;

    static readonly int P_Reload = Animator.StringToHash("Reload");
    static readonly int P_Shoot = Animator.StringToHash("Shoot");
    static readonly int P_Rack = Animator.StringToHash("Rack");

    [Header("Debug")]
    public bool allowDebugNumberKeys = false;

    void Awake()
    {
        // Auto-wire básicos
        if (!driver) driver = GetComponentInChildren<WeaponAnimatorDriver>(true);
        if (!itemAnimator && driver) itemAnimator = driver ? driver.anim : null;
        if (!itemGO && driver) itemGO = driver.gameObject;
        if (!muzzle && driver) muzzle = driver.muzzle;

        // NUEVO: enganchar sistemas
        if (!projectileShooter && driver) projectileShooter = driver.GetComponent<WeaponProjectileShooter2D>();
        if (!shotgunCone && driver) shotgunCone = driver.GetComponent<ShotgunCone2D>();
        if (!hitscan && driver) hitscan = driver.GetComponent<WeaponHitscan2D>();

        pistolLoaded = pistolStartLoaded; pistolReserve = pistolStartReserve;
        gunLoaded = gunStartLoaded; gunReserve = gunStartReserve;
        shotgunLoaded = shotgunStartLoaded; shotgunReserve = shotgunStartReserve;

        EnsureCurrentWeaponParent();
        Unequip(); // empezar en manos
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E)) Unequip();

        if (!allowDebugNumberKeys) return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) Equip(Slot.Pistol, idPistol);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Equip(Slot.Gun, idGun);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Equip(Slot.Shotgun, idShotgun);
        if (Input.GetKeyDown(KeyCode.Alpha4)) Equip(Slot.Bat, idBat);
    }

    // === API por ID (UI/Inventario) ===
    public void EquipByItemId(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        if (IsEquippedId(id)) { Unequip(); return; }

        if (id == idPistol) { Equip(Slot.Pistol, idPistol); return; }
        if (id == idGun) { Equip(Slot.Gun, idGun); return; }
        if (id == idShotgun) { Equip(Slot.Shotgun, idShotgun); return; }
        if (id == idBat) { Equip(Slot.Bat, idBat); return; }
        // si no coincide, ignoramos
    }

    // API legacy por índice
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
        EnsureCurrentWeaponParent();

        bool itemIsOff = itemGO && !itemGO.activeSelf;
        if (slot == current && !itemIsOff) return;

        SaveCurrentAmmo();
        if (muzzle) muzzle.Hide();
        if (itemGO) itemGO.SetActive(true);

        if (handsGO) handsGO.SetActive(false);
        if (driver) driver.UseChildSpriteRenderer(false);

        SetCombatMode(isMelee: (slot == Slot.Bat));

        // Instanciar el prefab correspondiente (si aplica)
        switch (slot)
        {
            case Slot.Pistol:
                ReplaceCurrentWeapon(pistolPrefab);
                if (itemAnimator && pistolAOC) itemAnimator.runtimeAnimatorController = pistolAOC;
                ApplyToDriver(false, pistolClipSize, pistolLoaded, pistolReserve);
                EnableSystems(useProjectile: true, useShotgunCone: false, useHitscan: false);
                ClearDriverTriggersSafe();
                break;

            case Slot.Gun:
                ReplaceCurrentWeapon(riflePrefab);
                if (itemAnimator && gunAOC) itemAnimator.runtimeAnimatorController = gunAOC;
                ApplyToDriver(false, gunClipSize, gunLoaded, gunReserve);
                EnableSystems(useProjectile: true, useShotgunCone: false, useHitscan: false);
                ClearDriverTriggersSafe();
                break;

            case Slot.Shotgun:
                ReplaceCurrentWeapon(shotgunPrefab);
                if (itemAnimator && shotgunAOC) itemAnimator.runtimeAnimatorController = shotgunAOC;
                ApplyToDriver(true, shotgunClipSize, shotgunLoaded, shotgunReserve);
                EnableSystems(useProjectile: false, useShotgunCone: true, useHitscan: false);
                ClearDriverTriggersSafe();
                break;

            case Slot.Bat:
                ReplaceCurrentWeapon(batPrefab); // si es null, simplemente no instanciamos nada
                if (itemAnimator && meleeBatAOC) itemAnimator.runtimeAnimatorController = meleeBatAOC;

                if (driver)
                {
                    driver.isShotgun = false;
                    driver.isMelee = true;
                    driver.clipSize = 0;
                    driver.loaded = 0;
                    driver.reserve = 0;
                    driver.usesOwnHands = batUsesOwnHands;
                }
                if (handsGO) handsGO.SetActive(!batUsesOwnHands);
                if (muzzle) muzzle.Hide();
                EnableSystems(useProjectile: false, useShotgunCone: false, useHitscan: false);
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

        // apaga solo el sprite del Item; dejamos CurrentWeapon si quieres mantener la vista apagada
        if (itemGO) itemGO.SetActive(false);

        if (driver)
        {
            driver.usesOwnHands = false;
            driver.isShotgun = false;
            driver.isMelee = false;
            driver.clipSize = 0;
            driver.loaded = 0;
            driver.reserve = 0;
            driver.UseChildSpriteRenderer(false);
        }

        SetCombatMode(isMelee: false);
        EnableSystems(false, false, false);

        current = Slot.None;
        CurrentItemId = "";
    }

    // ===== helpers =====
    void EnsureCurrentWeaponParent()
    {
        if (currentWeaponParent && currentWeaponParent.name == "CurrentWeapon") return;

        Transform baseParent = null;

        if (currentWeaponParent) baseParent = currentWeaponParent;
        if (!baseParent && itemGO) baseParent = itemGO.transform;

        if (!baseParent)
        {
            var root = transform.root ? transform.root : transform;
            baseParent = root.Find("Player/Visual/Item")
                      ?? root.Find("Visual/Item")
                      ?? root.Find("Item")
                      ?? transform; // último recurso
        }

        var cw = (baseParent.name == "CurrentWeapon") ? baseParent : baseParent.Find("CurrentWeapon");
        if (!cw)
        {
            var go = new GameObject("CurrentWeapon");
            cw = go.transform;
            cw.SetParent(baseParent, false);
            cw.localPosition = Vector3.zero;
            cw.localRotation = Quaternion.identity;
            cw.localScale = Vector3.one;
        }

        currentWeaponParent = cw;
    }

    void ReplaceCurrentWeapon(GameObject prefab)
    {
        EnsureCurrentWeaponParent();

        for (int i = currentWeaponParent.childCount - 1; i >= 0; i--)
            Destroy(currentWeaponParent.GetChild(i).gameObject);

        if (!prefab) return;

        var inst = Instantiate(prefab, currentWeaponParent);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
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

    void ApplyToDriver(bool isShotgun, int clip, int loaded, int reserve)
    {
        if (!driver) return;
        driver.isShotgun = isShotgun;
        driver.isMelee = false;
        driver.clipSize = clip;
        driver.loaded = Mathf.Clamp(loaded, 0, clip);
        driver.reserve = Mathf.Max(0, reserve);
        driver.usesOwnHands = true;
    }

    void SetCombatMode(bool isMelee)
    {
        if (hitscan) hitscan.enabled = !isMelee; // si lo quieres totalmente off, lo controla EnableSystems
        if (driver && driver.melee) driver.melee.enabled = isMelee;
    }

    // ====== NUEVO: habilitar/deshabilitar sistemas ======
    void EnableSystems(bool useProjectile, bool useShotgunCone, bool useHitscan)
    {
        if (projectileShooter) projectileShooter.enabled = useProjectile;
        if (shotgunCone) shotgunCone.enabled = useShotgunCone;
        if (hitscan) hitscan.enabled = useHitscan;
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
        {
            if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Trigger)
            { a.ResetTrigger(hash); break; }
        }
    }
}
