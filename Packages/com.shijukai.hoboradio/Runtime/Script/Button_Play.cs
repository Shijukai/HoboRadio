
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Button_Play : UdonSharpBehaviour
{
    GameObject Button_Play_Collider;

    [SerializeField]
    private UdonSharpBehaviour TargetUdon;

    public override void Interact()
    {
        TargetUdon.SendCustomEvent("InteractButtonPlay");
    }
}
