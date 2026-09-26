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

    [Header("Auto Respawn Settings")]
    [Tooltip("手放してから初期位置に自動で戻るまでの時間（秒）。0以下の場合は自動で戻りません")]
    public float autoRespawnDelay = 30f;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private bool isTimerScheduled = false;

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
        initialLocalPosition = targetTransform.localPosition;
        initialLocalRotation = targetTransform.localRotation;
    }

    public override void OnPickup()
    {
        isTimerScheduled = false;
    }

    public override void OnDrop()
    {
        if (autoRespawnDelay > 0f)
        {
            isTimerScheduled = true;
            SendCustomEventDelayedSeconds(nameof(_ResetToInitialPosition), autoRespawnDelay);
        }
    }

    public void _ResetToInitialPosition()
    {
        if (!isTimerScheduled) return;
        isTimerScheduled = false;

        // スロットに挿入されている場合はリセットしない
        if (targetTransform.parent != originalParent && targetTransform.parent != null) return;

        targetTransform.SetParent(originalParent, true);
        targetTransform.localPosition = initialLocalPosition;
        targetTransform.localRotation = initialLocalRotation;

        if (tapeRigidbody != null)
        {
            tapeRigidbody.velocity = Vector3.zero;
            tapeRigidbody.angularVelocity = Vector3.zero;
            tapeRigidbody.isKinematic = true;
        }
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
