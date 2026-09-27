using UdonSharp;
using UnityEngine;

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
}

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class HoboRadio_Button : UdonSharpBehaviour
{
    [SerializeField]
    private UdonSharpBehaviour targetUdon;

    [SerializeField]
    private RadioButtonType buttonType;

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
        }
    }
}
