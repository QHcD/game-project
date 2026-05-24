using System.Collections;
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
    private bool syncReleased;
    private bool loggedHold;
    private Vector3 holdPosition;
    private Quaternion holdRotation;
    private CharacterController characterController;

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
        characterController = GetComponent<CharacterController>();
        if (MultiplayerMode.IsMultiplayer && playerHealth != null)
            playerHealth.autoRegenEnabled = false;
        holdPosition = transform.position;
        holdRotation = transform.rotation;
        networkPosition = holdPosition;
        networkRotation = holdRotation;
    }

    private void Start()
    {
        EnsurePhotonNicknameFallback();
        RegisterForScoreboard();
        if (MultiplayerMode.IsMultiplayer)
            StartCoroutine(WaitForLocalMapBeforeSync());
    }

    private IEnumerator WaitForLocalMapBeforeSync()
    {
        int actor = GetActorNumber();
        float timeoutAt = Time.realtimeSinceStartup + 25f;
        bool loggedReady = false;

        while (Time.realtimeSinceStartup < timeoutAt)
        {
            bool ready = LevelBuilder.IsLocalClientMapReadyForSync();
            if (ready && !loggedReady)
            {
                loggedReady = true;
                Debug.Log("[MPBuild] local client map ready before sync = true");
            }

            if (ready)
            {
                if (TryWarpToSafeGround(out Vector3 safe))
                {
                    holdPosition = safe;
                    networkPosition = safe;
                    transform.position = safe;
                    Physics.SyncTransforms();
                    Debug.Log($"[MPSpawn] safe ground found actor={actor} position={safe}");
                }

                syncReleased = true;
                Debug.Log($"[MPSync] released transform sync actor={actor}");
#if PUN_2_OR_NEWER
                if (photonView != null && photonView.IsMine)
                    FreezeMovement(false);
#endif
                yield break;
            }

            if (!loggedHold)
            {
                loggedHold = true;
                Debug.Log($"[MPSync] holding transform until map ready actor={actor}");
                Debug.Log("[MPBuild] local client map ready before sync = false");
            }

            FreezeMovement(true);
            transform.position = holdPosition;
            transform.rotation = holdRotation;
            yield return null;
        }

        syncReleased = true;
        Debug.LogWarning($"[MPSync] released transform sync actor={actor} after timeout");
    }

    private void Update()
    {
#if PUN_2_OR_NEWER
        if (photonView != null && !photonView.IsMine)
        {
            if (!syncReleased)
            {
                transform.position = holdPosition;
                transform.rotation = holdRotation;
                return;
            }

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

                if (!ff) return;
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
        if (photonView != null && !photonView.IsMine)
            return;

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
        if (!syncReleased)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(holdPosition);
                stream.SendNext(holdRotation);
                stream.SendNext(playerHealth != null ? playerHealth.currentHealth : maxHealthFallback());
                stream.SendNext(playerController != null ? playerController.GetEquippedWeaponLevel() : 1);
                stream.SendNext(playerController != null ? playerController.equippedWeaponName : string.Empty);
            }
            else
            {
                networkPosition = (Vector3)stream.ReceiveNext();
                networkRotation = (Quaternion)stream.ReceiveNext();
                stream.ReceiveNext();
                stream.ReceiveNext();
                stream.ReceiveNext();
            }
            return;
        }

        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(playerHealth != null ? playerHealth.currentHealth : maxHealthFallback());
            stream.SendNext(playerController != null ? playerController.GetEquippedWeaponLevel() : 1);
            stream.SendNext(playerController != null ? playerController.equippedWeaponName : string.Empty);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            float syncedHealth = (float)stream.ReceiveNext();
            int weaponLevel = (int)stream.ReceiveNext();
            string weaponName = (string)stream.ReceiveNext();

            if (playerHealth != null && photonView != null && !photonView.IsMine)
                playerHealth.ApplySyncedHealth(syncedHealth, fromNetworkStream: true);
            if (playerController != null)
                playerController.ApplyNetworkWeaponState(weaponLevel, weaponName);
        }
    }

    private float maxHealthFallback()
    {
        return playerHealth != null ? playerHealth.maxHealth : 100f;
    }
#endif

    private void RegisterForScoreboard()
    {
        if (MatchStatsManager.Instance == null)
            return;

        string displayName = "PLAYER";
        int actorNumber = GetActorNumber();
#if PUN_2_OR_NEWER
        if (photonView != null && photonView.Owner != null)
        {
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
    }

    private int GetActorNumber()
    {
#if PUN_2_OR_NEWER
        if (photonView != null && photonView.Owner != null)
            return photonView.Owner.ActorNumber;
#endif
        return -1;
    }

    private void FreezeMovement(bool freeze)
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
        if (characterController != null)
            characterController.enabled = !freeze;

        if (playerController != null && freeze)
            playerController.enabled = false;
        else if (playerController != null && photonView != null && photonView.IsMine && syncReleased)
            playerController.enabled = true;
    }

    private bool TryWarpToSafeGround(out Vector3 safe)
    {
        safe = holdPosition;
        if (!LevelBuilder.TryGetMultiplayerMapReadiness(out LevelBuilder.MapReadinessInfo info) || info.Root == null)
            return false;

        Vector3 origin = new Vector3(holdPosition.x, info.Root.position.y + 80f, holdPosition.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 300f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            safe = hit.point + Vector3.up * 0.15f;
            Debug.Log($"[MPSpawn] prevented void fall actor={GetActorNumber()}");
            return true;
        }

        return false;
    }

    private void EnsurePhotonNicknameFallback()
    {
#if PUN_2_OR_NEWER
        if (!MultiplayerMode.IsMultiplayer || photonView == null || !photonView.IsMine)
            return;

        if (!MultiplayerShutdownGuard.CanWriteProperties("NetworkPlayerSync.EnsurePhotonNicknameFallback", requireRoom: true)) return;
        if (PhotonNetwork.LocalPlayer != null && string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
        {
            MultiplayerShutdownGuard.LogPropertyWrite("NetworkPlayerSync.EnsurePhotonNicknameFallback");
            PhotonNetwork.NickName = $"Player_{PhotonNetwork.LocalPlayer.ActorNumber}";
        }
#endif
    }
}
