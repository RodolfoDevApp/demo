using UnityEngine;

[DisallowMultipleComponent]
public class HandsPickProxy : MonoBehaviour
{
    [Header("Refs")]
    public Animator handsAnimator;            // arrastra el Animator de Hands
    public PickupController pickupController; // arrastra el PickupController del Player

    static readonly int P_Pick = Animator.StringToHash("Pick");

    public void PlayPick()
    {
        if (!handsAnimator) return;
        handsAnimator.ResetTrigger(P_Pick);
        handsAnimator.SetTrigger(P_Pick);
    }

    // <-- ESTE método es el que debe llamar el Animation Event en los clips de Hands
    public void AE_DoCollect()
    {
        if (pickupController) pickupController.AE_DoCollect();
    }
}
