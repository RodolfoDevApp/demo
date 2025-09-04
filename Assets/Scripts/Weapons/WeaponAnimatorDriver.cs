using UnityEngine;

[RequireComponent(typeof(Animator))]
public class WeaponAnimatorDriver : MonoBehaviour
{
    [Header("Refs (Item)")]
    public Animator anim;
    public Animator bodyAnim;
    public Rigidbody2D playerRb;

    [Tooltip("SpriteRenderer ACTIVO que se usa para ordenar y como 'Source SR' del Muzzle (se actualiza con UseChildSpriteRenderer).")]
    public SpriteRenderer itemSR;
    [Tooltip("SpriteRenderer del hijo (p.ej. 'Sprite') para melee.")]
    public SpriteRenderer itemSRChild;
    [Tooltip("SpriteRenderer del root (Item) para firearms.")]
    public SpriteRenderer itemSRRoot;

    public GameObject handsGO;

    [Header("Melee")]
    [Tooltip("Hitbox de melee actualmente en uso (bat o puños).")]
    public MeleeHitbox2D melee;     // en escena (bat o fists)
    [Tooltip("Hitbox específico de puños (duplicado del MeleeHitbox, más pequeño).")]
    public MeleeHitbox2D fists;

    [Header("Config de manos / render")]
    public bool usesOwnHands = true;
    public bool manageHandsFromDriver = false;

    [Header("Sorting")]
    public bool useRelativeSortingToBody = true;
    public SpriteRenderer bodySR;
    public int relativeFrontDelta = +1;
    public int relativeBackDelta = -1;

    [Header("Offsets por dirección (localPosition)")]
    public Vector2 offsetDown = new(0f, -0.3f);
    public Vector2 offsetUp = new(0f, 0.18f);
    public Vector2 offsetLeft = Vector2.zero;
    public Vector2 offsetRight = Vector2.zero;
    public bool swapLeftRight = false;

    [Header("Auto-Offsets (opcional)")]
    public bool useAutoOffsets = false;
    [Range(0f, 1f)] public float downFracFromMid = 0.6f;
    [Range(0f, 1f)] public float upFracFromMid = 0.5f;

    [Header("Timing")]
    public bool offsetInLateUpdate = true;

    [Header("Pixel Perfect")]
    public bool snapToPixelGrid = true;
    public bool snapInWorldSpace = true;
    public int pixelsPerUnitOverride = 0;

    [Header("Arma / inventario")]
    public bool isShotgun = true;
    public bool isMelee = false;
    public int clipSize = 4;
    public int loaded = 0;
    public int reserve = 20; // ← ahora se sincroniza con inventario si está activo (ver abajo)

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
    public MuzzleFlash2D muzzle;
    public bool muzzleViaAnimEvent = false;

    // ---------- NUEVO: integración con inventario de munición ----------
    [Header("Ammo / Inventory Bridge (opcional)")]
    [Tooltip("Actívalo para que la recarga consuma balas desde InventoryRuntime.")]
    public bool useInventoryAmmo = true;
    [Tooltip("Inventario global en escena (o del jugador).")]
    public InventoryRuntime inventory;
    [Tooltip("Hotbar/bridge para leer el id del arma equipada (idPistol/idGun/idShotgun/idBat).")]
    public WeaponHotbarSimple hotbar;
    [Tooltip("ItemDef de 9mm.")]
    public ItemDef ammo9mm;
    [Tooltip("ItemDef de rifle.")]
    public ItemDef ammoRifle;
    [Tooltip("ItemDef de shells de escopeta.")]
    public ItemDef ammoShells;

    // ------------------------------------------------------
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
    }

    void Awake()
    {
        if (!anim) anim = GetComponent<Animator>();
        if (!itemSRRoot) itemSRRoot = GetComponent<SpriteRenderer>();
        if (!itemSR) itemSR = itemSRRoot ? itemSRRoot : GetComponent<SpriteRenderer>();
        if (muzzle && itemSR) muzzle.sourceSR = itemSR;

        if (!melee) melee = GetComponentInChildren<MeleeHitbox2D>(true);

        if (manageHandsFromDriver) ApplyHandsVisibility(true);
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
        anim.SetIntegerIfExists(P_Dir, dirForAnim);
        anim.SetFloatIfExists(P_Speed, GetSpeed());

        if (useAutoOffsets) ComputeAutoOffsets();

        // ---------- NUEVO: reflejar 'reserve' según inventario ----------
        if (useInventoryAmmo) SyncReserveFromInventory();

        if (!offsetInLateUpdate)
            ApplySortingAndOffset(dirForAnim);

        if (Input.GetKeyDown(KeyCode.J)) TryShootOrAttack();
        if (Input.GetKeyDown(KeyCode.R)) TryStartReload();
    }

    void LateUpdate()
    {
        if (!offsetInLateUpdate) return;
        int dir = GetCurrentDirFromAnim();
        ApplySortingAndOffset(dir);
    }

    // --- Helpers de combate ---
    public void EnableFists()
    {
        isMelee = true;
        if (fists) melee = fists; // usa la hitbox de puños
    }

    public void UseMeleeHitbox(MeleeHitbox2D hb)
    {
        isMelee = true;
        if (hb) melee = hb;       // usa la hitbox de un arma melee (bat, etc.)
    }

    public void DisableMelee()
    {
        isMelee = false;
        // no desreferenciamos 'melee' para que conserve la asignación en inspector
    }

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
            anim.SetTriggerIfExists(P_Attack);
            return;
        }

        if (loaded <= 0) return;
        loaded--;
        anim.SetTriggerIfExists(P_Shoot);
        if (!muzzleViaAnimEvent && muzzle) muzzle.Play(GetCurrentDirFromAnim(), bodySR);
    }

    // ---------- Recarga ----------
    void TryStartReload()
    {
        if (isMelee) return;

        // si usamos inventario y no hay balas → bloquear recarga
        if (useInventoryAmmo && GetCurrentAmmoDef() != null)
        {
            reserve = GetCurrentAmmoCountSafe(); // asegurar último conteo
            if (reserve <= 0) return;
        }

        if (!isShotgun)
        {
            if (loaded >= clipSize || reserve <= 0) return;
            wasEmpty = (loaded == 0);
            int need = clipSize - loaded;

            // si usamos inventario, vuelve a contar y limita por ese total
            int available = useInventoryAmmo ? GetCurrentAmmoCountSafe() : reserve;
            int add = Mathf.Min(need, available);
            if (add <= 0) return;

            loaded += add;
            reserve = Mathf.Max(0, available - add);

            // consumir del inventario real
            if (useInventoryAmmo) ConsumeAmmoFromInventory(add);

            anim.SetBoolIfExists(P_WasEmpty, wasEmpty);
            anim.SetTriggerIfExists(P_Reload);
            return;
        }

        if (isReloading || loaded >= clipSize || reserve <= 0) return;

        wasEmpty = (loaded == 0);

        // shotgun: preparar shellsToLoad limitado por inventario si aplica
        int canLoad = clipSize - loaded;
        int availableShells = useInventoryAmmo ? GetCurrentAmmoCountSafe() : reserve;
        shellsToLoad = Mathf.Min(canLoad, availableShells);
        if (shellsToLoad <= 0) return;

        isReloading = true;

        anim.SetBoolIfExists(P_IsReloading, true);
        anim.SetIntegerIfExists(P_ShellsToLoad, shellsToLoad);
        anim.SetBoolIfExists(P_WasEmpty, wasEmpty);
        anim.SetTriggerIfExists(P_Reload);
    }

    // ---------- Animation Events ----------
    public void AE_InsertShell()
    {
        if (!isShotgun || !isReloading) return;

        if (shellsToLoad > 0)
        {
            // verificar inventario justo antes de insertar
            if (useInventoryAmmo)
            {
                if (!ConsumeAmmoFromInventory(1)) // si no pudo consumir, aborta ciclo
                {
                    shellsToLoad = 0;
                    isReloading = false;
                    anim.SetIntegerIfExists(P_ShellsToLoad, 0);
                    anim.SetBoolIfExists(P_IsReloading, false);
                    return;
                }
                reserve = GetCurrentAmmoCountSafe(); // refresca lectura
            }
            else
            {
                if (reserve <= 0) return;
                reserve--;
            }

            shellsToLoad--; loaded++;
            anim.SetIntegerIfExists(P_ShellsToLoad, shellsToLoad);
        }

        if (shellsToLoad <= 0)
        {
            isReloading = false;
            anim.SetIntegerIfExists(P_ShellsToLoad, 0);
            anim.SetBoolIfExists(P_IsReloading, false);
            if (wasEmpty) anim.SetTriggerIfExists(P_Rack);
        }
    }

    public void AE_Muzzle()
    {
        if (muzzle) muzzle.Play(GetCurrentDirFromAnim(), bodySR);
    }

    // 👉 eventos de melee que llaman al hitbox
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

    // ---------- Utilidades ----------
    void ApplyHandsVisibility(bool enablePhase)
    {
        if (!handsGO) return;
        bool show = !usesOwnHands && enablePhase;
        handsGO.SetActive(show);
    }

    void ApplySortingAndOffset(int dir)
    {
        if (useRelativeSortingToBody && bodySR && itemSR)
        {
            itemSR.sortingLayerID = bodySR.sortingLayerID;
            itemSR.sortingOrder = (dir == DIR_UP)
                ? bodySR.sortingOrder + relativeBackDelta
                : bodySR.sortingOrder + relativeFrontDelta;
        }

        Vector2 off = dir switch
        {
            DIR_DOWN => offsetDown,
            DIR_UP => offsetUp,
            DIR_LEFT => (swapLeftRight ? offsetRight : offsetLeft),
            DIR_RIGHT => (swapLeftRight ? offsetLeft : offsetRight),
            _ => Vector2.zero
        };

        if (snapToPixelGrid)
        {
            if (snapInWorldSpace)
            {
                Vector2 parentPos = transform.parent ? (Vector2)transform.parent.position : Vector2.zero;
                Vector2 world = parentPos + off;
                Vector2 snapped = SnapToPixels(world);
                transform.position = new Vector3(snapped.x, snapped.y, transform.position.z);
            }
            else
            {
                Vector2 snappedLocal = SnapToPixels(off);
                transform.localPosition = new Vector3(snappedLocal.x, snappedLocal.y, transform.localPosition.z);
            }
        }
        else
        {
            transform.localPosition = new Vector3(off.x, off.y, transform.localPosition.z);
        }
    }

    void ComputeAutoOffsets()
    {
        if (!bodySR || !bodySR.sprite) return;
        float halfH = bodySR.sprite.bounds.extents.y;
        offsetDown.y = -halfH * downFracFromMid;
        offsetUp.y = halfH * upFracFromMid;
    }

    int GetDir()
    {
        if (bodyAnim && bodyAnim.HasParameter(P_Dir))
            return bodyAnim.GetInteger(P_Dir);

        Vector2 v = playerRb ? playerRb.linearVelocity : Vector2.zero;
        if (v.sqrMagnitude < 0.0001f)
            return anim ? anim.GetInteger(P_Dir) : DIR_DOWN;

        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            return (v.x >= 0f) ? DIR_RIGHT : DIR_LEFT;
        else
            return (v.y >= 0f) ? DIR_UP : DIR_DOWN;
    }

    int GetCurrentDirFromAnim()
    {
        if (bodyAnim && bodyAnim.HasParameter(P_Dir))
            return bodyAnim.GetInteger(P_Dir);
        return anim ? anim.GetInteger(P_Dir) : DIR_DOWN;
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
    }

    // ---------- Pixel helpers ----------
    float GetPPU()
    {
        if (pixelsPerUnitOverride > 0) return pixelsPerUnitOverride;
        if (itemSR && itemSR.sprite) return itemSR.sprite.pixelsPerUnit;
        if (bodySR && bodySR.sprite) return bodySR.sprite.pixelsPerUnit;
        return 16f;
    }

    Vector2 SnapToPixels(Vector2 worldPos)
    {
        float ppu = GetPPU();
        return new Vector2(
            Mathf.Round(worldPos.x * ppu) / ppu,
            Mathf.Round(worldPos.y * ppu) / ppu
        );
    }

    // ---------- NUEVO: helpers de munición / inventario ----------
    void SyncReserveFromInventory()
    {
        var def = GetCurrentAmmoDef();
        if (!inventory || !def) return;
        reserve = inventory.Count(def); // solo lectura; el consumo se hace en recarga
    }

    ItemDef GetCurrentAmmoDef()
    {
        if (!hotbar) return null;
        string wid = hotbar.CurrentItemId;
        if (string.IsNullOrEmpty(wid)) return null;

        if (wid == hotbar.idPistol) return ammo9mm;
        if (wid == hotbar.idGun) return ammoRifle;
        if (wid == hotbar.idShotgun) return ammoShells;
        // bat u otros -> sin munición
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
        // intenta remover; si no alcanza, retorna false
        int before = inventory.Count(def);
        if (before < amount) return false;
        bool ok = inventory.TryRemove(def, amount);
        if (ok) reserve = Mathf.Max(0, before - amount);
        return ok;
    }
}
