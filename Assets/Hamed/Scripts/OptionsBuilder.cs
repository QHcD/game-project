using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Deprecated: Options menu merged into Settings. Redirects to Settings scene.</summary>
public class OptionsBuilder : MonoBehaviour
{
    void Awake()
    {
        SceneManager.LoadScene("Settings");
    }
}
