using UnityEngine;

/// <summary>
/// Placeholder for menu lifetime coordination. Per PRISM cleanup, loading-screen and
/// main-menu motion graphics are disabled; see <see cref="RuntimeMenuBuilder"/> and <see cref="LoadingScreenUI"/>.
/// </summary>
public sealed class MenuManager : MonoBehaviour
{
    // No loading-screen animation hooks — flat / static UI only.
}
