using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LevelSelectManager : MonoBehaviour
{
    public Button[] levelButtons; // Assign 20 buttons in Inspector

    void Start()
    {
        int unlockedLevels = PlayerPrefs.GetInt("UnlockedLevels", 1);

        for (int i = 0; i < levelButtons.Length; i++)
        {
            bool isUnlocked = (i + 1) <= unlockedLevels;
            levelButtons[i].interactable = isUnlocked;

            // Gray out locked buttons
            ColorBlock colors = levelButtons[i].colors;
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            levelButtons[i].colors = colors;
        }
    }

    public void LoadLevel(int levelIndex)
    {
        SceneManager.LoadScene("Level_" + levelIndex.ToString("D2"));
    }

    public void GoBack()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
