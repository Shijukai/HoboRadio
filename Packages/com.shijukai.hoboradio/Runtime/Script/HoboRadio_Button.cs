using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public enum RadioButtonType
{
    Power,
    SwitchChannel,
    Play,
    Pause,
    Stop,
    FastForward,
    Rewind,
    Debug,
    SignageInteract,
    SignageTrigger,
}

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class HoboRadio_Button : UdonSharpBehaviour
{
    [SerializeField]
    private UdonSharpBehaviour targetUdon;

    [SerializeField]
    private RadioButtonType buttonType;

    private void Start()
    {
        switch (buttonType)
        {
            case RadioButtonType.SignageTrigger:
                DisableInteractive = true;
                break;
            default:
                DisableInteractive = false;
                break;
        }
    }

    public override void Interact()
    {
        if (targetUdon == null) return;

        switch (buttonType)
        {
            case RadioButtonType.Power:
                targetUdon.SendCustomEvent("InteractButtonPower");
                break;
            case RadioButtonType.SwitchChannel:
                targetUdon.SendCustomEvent("InteractSwitchChannel");
                break;
            case RadioButtonType.Play:
                targetUdon.SendCustomEvent("InteractButtonPlay");
                break;
            case RadioButtonType.Pause:
                targetUdon.SendCustomEvent("InteractButtonPause");
                break;
            case RadioButtonType.Stop:
                targetUdon.SendCustomEvent("InteractButtonStop");
                break;
            case RadioButtonType.FastForward:
                targetUdon.SendCustomEvent("InteractButtonFastForward");
                break;
            case RadioButtonType.Rewind:
                targetUdon.SendCustomEvent("InteractButtonRewind");
                break;
            case RadioButtonType.Debug:
                targetUdon.SendCustomEvent("InteractButtonDebug");
                break;
            case RadioButtonType.SignageInteract:
                targetUdon.SendCustomEvent("OnSignageInteract");
                break;
            case RadioButtonType.SignageTrigger:
                break;
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (targetUdon == null || player == null || !player.isLocal) return;

        switch (buttonType)
        {
            case RadioButtonType.SignageTrigger:
                targetUdon.SendCustomEvent("OnPlayerApproachEnter");
                break;
            default:
                break;
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (targetUdon == null || player == null || !player.isLocal) return;

        switch (buttonType)
        {
            case RadioButtonType.SignageTrigger:
                targetUdon.SendCustomEvent("OnPlayerApproachExit");
                break;
            default:
                break;
        }
    }
}
