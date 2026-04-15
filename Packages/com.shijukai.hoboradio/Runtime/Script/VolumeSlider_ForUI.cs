using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

public class VolumeSlider_ForUI : UdonSharpBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private AudioSource audioSource;

    void Start()
    {
        slider.value = audioSource.volume;
    }
    public void UpdateVolume()
    {
        audioSource.volume = slider.value;
    }
}