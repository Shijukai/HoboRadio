
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Button_Debug : UdonSharpBehaviour
{
    GameObject Button_Power_Collider;

    [SerializeField]
    private UdonSharpBehaviour TargetUdon;

    public override void Interact()
    {
        TargetUdon.SendCustomEvent("InteractButtonDebug");
    }
}
