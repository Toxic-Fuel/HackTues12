using UnityEngine;

public class QuitButton : MonoBehaviour
{
    public void Quit()
    {
        Debug.Log("Quit button clicked. Exiting application.");
        Application.Quit();
    }
}
