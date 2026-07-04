using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeSceneIndex : MonoBehaviour
{
    public void SceneChanger(int newIndex)
    {
        LoadingScreenController.sceneIndex = newIndex;
        SceneManager.LoadScene("Mode_Load"); 
    }
}
