
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Switch_Channel : UdonSharpBehaviour
{
    GameObject Switch_Channel_Collider;

    [SerializeField]
    private UdonSharpBehaviour TargetUdon;

    public override void Interact()
    {
        TargetUdon.SendCustomEvent("InteractSwitchChannel");
    }
}
