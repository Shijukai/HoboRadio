using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

public class VolumeSlider_ForUI : UdonSharpBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private HoboRadio_Controller controller;

    void Start()
    {
        if (controller != null && slider != null)
        {
            slider.value = controller.masterVolume;
        }
    }
    public void UpdateVolume()
    {
        if (controller != null && slider != null)
        {
            controller.UpdateMasterVolume(slider.value);
        }
    }
}