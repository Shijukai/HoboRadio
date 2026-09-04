using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class RadioVideoPlayerRelay : UdonSharpBehaviour
{
    [SerializeField] public HoboRadio_Controller controller;

    public override void OnVideoReady()
    {
        if (controller != null)
        {
            controller.OnVideoReady();
        }
    }

    public override void OnVideoError(VideoError videoError)
    {
        if (controller != null)
        {
            controller.OnVideoError(videoError);
        }
    }
}
