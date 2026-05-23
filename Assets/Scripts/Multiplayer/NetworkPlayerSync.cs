using UnityEngine;

#if PUN_2_OR_NEWER
using Photon.Pun;
#endif

#if PUN_2_OR_NEWER
public class NetworkPlayerSync : MonoBehaviourPun, IPunObservable
#else
public class NetworkPlayerSync : MonoBehaviour
#endif
{
    public float remoteLerpSpeed = 14f;
    public float remoteSnapDistance = 6f;

    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private bool networkPoseInitialised;
    private PlayerHealth playerHealth;
    private PlayerController playerController;
    private bool deathRecorded;

    private string CombatantId
    {
        get
        {
#if PUN_2_OR_NEWER
            return photonView != null && photonView.Owner != null
                ? $"photon:{photonView.Owner.ActorNumber}"
                : MatchStatsManager.BuildCombatantId(this);
#else
            return MatchStatsManager.BuildCombatantId(this);
#endif
        }
    }

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerController = GetComponent<PlayerController>();
        if (MultiplayerMode.IsMultiplayer && playerHealth != null)
            playerHealth.autoRegenEnabled = false;
        networkPosition = transform.position;
        networkRotation = transform.rotation;
    }

    private void Start()
    {
        EnsurePhotonNicknameFallback();
        RegisterForScoreboard();
    }

    private void Update()
    {
#if PUN_2_OR_NEWER
        if (photonView != null && !photonView.IsMine)
        {
            // First serialization snap: stops the new joiner from "rubber
            // banding" from (0,0,0) to the real position over a second.
            if (!networkPoseInitialised ||
                Vector3.Distance(transform.position, networkPosition) > remoteSnapDistance)
            {
                transform.position = networkPosition;
                transform.rotation = networkRotation;
                networkPoseInitialised = true;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, networkPosition, remoteLerpSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, remoteLerpSpeed * Time.deltaTime);
            }
        }
#endif
    }

    public void ApplyDamageToNetworkPlayer(int damage, GameObject attackerRoot)
    {
#if PUN_2_OR_NEWER
        if (photonView == null)
            return;

        if (MultiplayerMode.IsMultiplayer)
        {
            bool isFriendly = attackerRoot != null && attackerRoot.GetComponentInParent<PlayerHealth>() != null && attackerRoot.GetComponentInParent<PlayerHealth>().gameObject != gameObject;
            if (isFriendly)
            {
                bool ff = true;
                if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MpRoomConfig.KeyFriendlyFire, out object ffRaw))
                    ff = (bool)ffRaw;
                if (MultiplayerMode.ActiveMode == MpGameMode.CoopSurvival)
                    ff = false;

                if (!ff) return; // Block friendly fire damage RPC
            }
        }

        PhotonView attackerView = attackerRoot != null ? attackerRoot.GetComponentInParent<PhotonView>() : null;
        int attackerActorNumber = attackerView != null && attackerView.Owner != null ? attackerView.Owner.ActorNumber : -1;
        string attackerName = attackerView != null && attackerView.Owner != null ? attackerView.Owner.NickName : "PLAYER";
        photonView.RPC(nameof(RpcApplyDamage), RpcTarget.All, Mathf.Max(1, damage), attackerActorNumber, attackerName);
#endif
    }

#if PUN_2_OR_NEWER
    [PunRPC]
    private void RpcApplyDamage(int damage, int attackerActorNumber, string attackerName)
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null || !playerHealth.IsAlive)
            return;

        GameObject attackerRoot = ResolveAttackerRoot(attackerActorNumber);
        playerHealth.ReceiveDamage(damage, attackerRoot);

        if (!playerHealth.IsAlive && !deathRecorded)
        {
            deathRecorded = true;
            if (playerController != null)
                playerController.enabled = false;
        }
    }

    private static GameObject ResolveAttackerRoot(int attackerActorNumber)
    {
        if (attackerActorNumber <= 0)
            return null;

        PhotonView[] views = Object.FindObjectsByType<PhotonView>(FindObjectsSortMode.None);
        for (int i = 0; i < views.Length; i++)
        {
            PhotonView view = views[i];
            if (view == null || view.Owner == null || view.Owner.ActorNumber != attackerActorNumber)
                continue;
            return view.gameObject;
        }

        return null;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(playerHealth != null ? playerHealth.currentHealth : 100f);
            stream.SendNext(playerController != null ? playerController.GetEquippedWeaponLevel() : 1);
            stream.SendNext(playerController != null ? playerController.equippedWeaponName : string.Empty);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            // Force the next Update on a remote ghost to teleport rather
            // than lerp the very first time we ever hear from this owner.
            // (We still snap on big future jumps via remoteSnapDistance.)
            float syncedHealth = (float)stream.ReceiveNext();
            int weaponLevel = (int)stream.ReceiveNext();
            string weaponName = (string)stream.ReceiveNext();

            if (playerHealth != null)
                playerHealth.currentHealth = syncedHealth;
            if (playerController != null)
                playerController.ApplyNetworkWeaponState(weaponLevel, weaponName);
        }
    }
#endif

    private void RegisterForScoreboard()
    {
        if (MatchStatsManager.Instance == null)
            return;

        string displayName = "PLAYER";
        int actorNumber = -1;
#if PUN_2_OR_NEWER
        if (photonView != null && photonView.Owner != null)
        {
            actorNumber = photonView.Owner.ActorNumber;
            displayName = string.IsNullOrWhiteSpace(photonView.Owner.NickName)
                ? $"Player_{actorNumber}"
                : photonView.Owner.NickName;
        }
        else
#endif
        if (PlayerProfile.HasUsername)
            displayName = PlayerProfile.Username;

        MatchStatsManager.Instance.RegisterCombatant(CombatantId, displayName, isPlayer: true, transform: transform);
        Debug.Log($"[MPHUD] registered player actor={actorNumber} name={displayName}");

#if PUN_2_OR_NEWER
        if (MultiplayerMode.IsMultiplayer && photonView != null && !photonView.IsMine && playerHealth != null)
            Debug.Log($"[MPHUD] remote player ignored for local HP actor={actorNumber}");
#endif
    }

    private void EnsurePhotonNicknameFallback()
    {
#if PUN_2_OR_NEWER
        if (!MultiplayerMode.IsMultiplayer || photonView == null || !photonView.IsMine)
            return;

        if (!MultiplayerShutdownGuard.CanWriteProperties()) return;
        if (PhotonNetwork.LocalPlayer != null && string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
            PhotonNetwork.NickName = $"Player_{PhotonNetwork.LocalPlayer.ActorNumber}";
#endif
    }
}
