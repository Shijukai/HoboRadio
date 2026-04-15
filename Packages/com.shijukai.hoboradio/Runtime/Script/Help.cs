using UdonSharp;
using UnityEngine;

public class Help : UdonSharpBehaviour
{
    [Header("Target Objects")]
    public GameObject[] targets;

    private bool isVisible = false;

    void Start()
    {
        ApplyVisibility();
    }

    public void OnButtonPressed()
    {
        isVisible = !isVisible;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        foreach (GameObject obj in targets)
        {
            if (obj != null)
            {
                obj.SetActive(isVisible);
            }
        }
    }
}