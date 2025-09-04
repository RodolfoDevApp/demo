using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HealthBarUI : MonoBehaviour
{
    [Header("Refs")]
    public PlayerHealth player;
    public Image barFill;            // Image con Fill Method = Horizontal
    public Image heartIcon;          // Image encima de la barra

    [Header("Sprites")]
    public Sprite heartFull;
    public Sprite heartHalf;
    public Sprite heartEmpty;

    void Awake()
    {
        // si no está asignado, intenta buscar en padres
        if (!player) player = GetComponentInParent<PlayerHealth>();
    }

    void OnEnable()
    {
        if (player != null)
            player.OnHPChanged.AddListener(UpdateUI);
    }

    void Start()
    {
        if (player != null)
            UpdateUI(player.HP, player.maxHP);
    }

    void OnDisable()
    {
        if (player != null)
            player.OnHPChanged.RemoveListener(UpdateUI);
    }

    public void UpdateUI(int hp, int maxHP)
    {
        if (maxHP <= 0) maxHP = 1;
        float percent = Mathf.Clamp01((float)hp / maxHP);

        if (barFill != null)
            barFill.fillAmount = percent;

        if (heartIcon != null)
        {
            if (percent > 0.80f) heartIcon.sprite = heartFull;
            else if (percent > 0.00f) heartIcon.sprite = heartHalf;
            else heartIcon.sprite = heartEmpty;
        }
    }
}
