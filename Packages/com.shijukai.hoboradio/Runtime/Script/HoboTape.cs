using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class HoboTape : UdonSharpBehaviour
{
    [Header("Tape Info")]
    public VRCUrl tapeUrl;
    public string tapeTitle;
    [Tooltip("制作団体・サークル名")]
    public string tapeArtist;

    [Header("Components")]
    public VRC_Pickup pickup;
    public Collider tapeCollider;

    private void Start()
    {
        if (pickup == null) pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
        if (tapeCollider == null) tapeCollider = GetComponent<Collider>();
    }
}
