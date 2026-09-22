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
    [Tooltip("スナップ移動の対象とするTransform（未指定の場合は親のTransform）")]
    public Transform targetTransform;
    public VRC_Pickup pickup;
    public Collider tapeCollider;
    public Rigidbody tapeRigidbody;

    private void Start()
    {
        if (targetTransform == null && transform.parent != null) targetTransform = transform.parent;
        else if (targetTransform == null) targetTransform = transform;

        if (pickup == null) pickup = (VRC_Pickup)targetTransform.GetComponentInChildren(typeof(VRC_Pickup));
        if (tapeCollider == null) tapeCollider = targetTransform.GetComponentInChildren<Collider>();
        if (tapeRigidbody == null) tapeRigidbody = targetTransform.GetComponentInChildren<Rigidbody>();
    }
}
