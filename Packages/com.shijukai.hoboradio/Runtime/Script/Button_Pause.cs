
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Button_Pause : UdonSharpBehaviour
{
    GameObject Button_Pause_Collider;

    [SerializeField]
    private UdonSharpBehaviour TargetUdon;

    public override void Interact()
    {
        TargetUdon.SendCustomEvent("InteractButtonPause");
    }
}
