using UnityEngine;

public class QuitButton : MonoBehaviour
{
    public void Quit()
    {
        if (InGameGenerationMenu.IsSceneActionBlocked())
        {
            return;
        }

        Debug.Log("Quit button clicked. Exiting application.");
        Application.Quit();
    }
}
