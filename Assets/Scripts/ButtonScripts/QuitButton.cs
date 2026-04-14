using UnityEngine;

public class QuitButton : MonoBehaviour
{
    public void Quit()
    {
        if (InGameGenerationMenu.IsMenuVisible)
        {
            return;
        }

        Debug.Log("Quit button clicked. Exiting application.");
        Application.Quit();
    }
}
