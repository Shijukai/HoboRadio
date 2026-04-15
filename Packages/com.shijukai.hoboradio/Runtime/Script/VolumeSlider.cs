using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class VolumeSlider : UdonSharpBehaviour
{
    [Header("References")]
    public Transform knob;
    public AudioSource targetAudio;

    [Header("Slider Range (Z Axis)")]
    public float minZ = 0.005f;
    public float maxZ = 0.0575f;

    private bool isOperating;
    private VRCPlayerApi.TrackingDataType activeHand;

    void Update()
    {
        if (!isOperating) return;

        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null) return;

        Vector3 inputLocal;

        if (player.IsUserInVR())
        {
            float rTrigger = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryIndexTrigger");
            float lTrigger = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryIndexTrigger");

            // use した手を確定
            if (
                (activeHand != VRCPlayerApi.TrackingDataType.RightHand && activeHand != VRCPlayerApi.TrackingDataType.LeftHand)
            )
            {
                if (rTrigger > 0.1f)
                {
                    activeHand = VRCPlayerApi.TrackingDataType.RightHand;
                }
                else if (lTrigger > 0.1f)
                {
                    activeHand = VRCPlayerApi.TrackingDataType.LeftHand;
                }
                else
                {
                    return;
                }
            }

            // トリガーを離したら終了
            if (
                (activeHand == VRCPlayerApi.TrackingDataType.RightHand && rTrigger < 0.1f) ||
                (activeHand == VRCPlayerApi.TrackingDataType.LeftHand && lTrigger < 0.1f)
            )
            {
                isOperating = false;
                activeHand = 0;
                return;
            }

            Vector3 handWorld = player.GetTrackingData(activeHand).position;
            inputLocal = transform.InverseTransformPoint(handWorld);
        }
        else
        {
            if (!Input.GetKey(KeyCode.Mouse0))
            {
                isOperating = false;
                return;
            }

            var head = player.GetTrackingData(
                VRCPlayerApi.TrackingDataType.Head
            );

            Vector3 point =
                head.position +
                (head.rotation * Vector3.forward * 0.6f);

            inputLocal = transform.InverseTransformPoint(point);
        }

        // Z方向のみスライド
        Vector3 localPos = knob.localPosition;     
        localPos.z = Mathf.Clamp(inputLocal.x, minZ, maxZ);                         // Z軸範囲で制限
        knob.localPosition = localPos; 

        // 音量へ変換
        float volume = Mathf.InverseLerp(minZ, maxZ, localPos.z);
        targetAudio.volume = volume;
    }

    // Desktop USE
    public override void Interact()
    {
        isOperating = true;
    }

    // VR：手が入ったら操作可能
    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!player.isLocal) return;
        if (!player.IsUserInVR()) return;

        isOperating = true;
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player.isLocal)
        {
            isOperating = false;
        }
    }
}
