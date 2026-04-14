using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayButton : MonoBehaviour
{
    [SerializeField] private string sceneName = "GridScene";

    public void Play()
    {
        if (InGameGenerationMenu.IsMenuVisible)
        {
            return;
        }

        InGameGenerationMenu.QueueCurrentSettingsForNextGridMap();

        SceneManager.LoadScene(sceneName);
    }
}
