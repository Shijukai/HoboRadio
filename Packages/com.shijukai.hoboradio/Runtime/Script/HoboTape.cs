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

    [Tooltip("スナップ移動の対象とするTransform（未指定の場合は親のTransform）")]
    public Transform targetTransform;
    public VRC_Pickup pickup;
    public Collider tapeCollider;
    public Rigidbody tapeRigidbody;

    [HideInInspector] public Transform originalParent;

    [Header("Animation Settings")]
    public Transform hubLeft;
    public Transform hubRight;
    public SkinnedMeshRenderer tapeMeshRenderer;
    [Tooltip("進捗0%の時の左ブレンドシェイプの値")]
    public float leftWeightAtStart = 0f;
    [Tooltip("進捗100%の時の左ブレンドシェイプの値")]
    public float leftWeightAtEnd = 100f;
    [Tooltip("進捗0%の時の右ブレンドシェイプの値")]
    public float rightWeightAtStart = 0f;
    [Tooltip("進捗100%の時の右ブレンドシェイプの値")]
    public float rightWeightAtEnd = 100f;
    [Tooltip("左テープ量ブレンドシェイプのインデックス")]
    public int blendShapeIndexLeft = 0;
    [Tooltip("右テープ量ブレンドシェイプのインデックス")]
    public int blendShapeIndexRight = 1;

    private void Start()
    {
        if (targetTransform == null && transform.parent != null) targetTransform = transform.parent;
        else if (targetTransform == null) targetTransform = transform;

        if (pickup == null) pickup = (VRC_Pickup)targetTransform.GetComponentInChildren(typeof(VRC_Pickup));
        if (tapeCollider == null) tapeCollider = targetTransform.GetComponentInChildren<Collider>();
        if (tapeRigidbody == null) tapeRigidbody = targetTransform.GetComponentInChildren<Rigidbody>();

        originalParent = targetTransform.parent;
    }

    public void UpdateTapeProgress(float progress)
    {
        if (tapeMeshRenderer != null)
        {
            float lWeight = Mathf.Lerp(leftWeightAtStart, leftWeightAtEnd, progress);
            float rWeight = Mathf.Lerp(rightWeightAtStart, rightWeightAtEnd, progress);
            tapeMeshRenderer.SetBlendShapeWeight(blendShapeIndexLeft, lWeight);
            tapeMeshRenderer.SetBlendShapeWeight(blendShapeIndexRight, rWeight);
        }
    }
}
