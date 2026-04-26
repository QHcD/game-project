using UnityEngine;

/// <summary>
/// Anything in the world that can be activated via the player's [E] key.
/// The <see cref="PlayerInteractor"/> raycasts forward from the active
/// camera and dispatches <see cref="Interact"/> when the player presses E
/// while pointed at this collider.
/// </summary>
public interface IInteractable
{
    /// <summary>Short label rendered in the interaction reticle ("OPEN DOOR", etc.).</summary>
    string GetPrompt();

    /// <summary>Called when the player presses [E] while looking at this object.</summary>
    void Interact(GameObject by);

    /// <summary>If false, the interaction reticle stays hidden even when targeting this object.</summary>
    bool CanInteract { get; }
}
