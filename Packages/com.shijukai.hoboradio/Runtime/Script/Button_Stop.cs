
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Button_Stop : UdonSharpBehaviour
{
    GameObject Button_Stop_Collider;

    [SerializeField]
    private UdonSharpBehaviour TargetUdon;

    public override void Interact()
    {
        TargetUdon.SendCustomEvent("InteractButtonStop");
    }
}
