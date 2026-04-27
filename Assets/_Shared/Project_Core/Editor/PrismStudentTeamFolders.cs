using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Team / student content roots expected under <c>Assets/</c>. Unity already
/// includes all assets in the project; this utility validates paths exist and
/// logs them for onboarding.
/// </summary>
public static class PrismStudentTeamFolders
{
    public static readonly string[] StudentAssetRoots =
    {
        "Assets/Hamed",
        "Assets/Murtadha",
        "Assets/MohamedAltajer",
        "Assets/AliAlhawaj",
        "Assets/MohamedAman",
    };

    [MenuItem("PRISM-7/Validate Student Team Folders")]
    public static void ValidateFolders()
    {
        foreach (string path in StudentAssetRoots)
        {
            bool ok = Directory.Exists(path);
            Debug.Log(ok ? $"[PRISM-7] OK: {path}" : $"[PRISM-7] MISSING: {path}");
        }
    }
}
