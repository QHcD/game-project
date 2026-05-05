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

    private Vector3 networkPosition;
    private Quaternion networkRotation;
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
        RegisterForScoreboard();
    }

    private void Update()
    {
#if PUN_2_OR_NEWER
        if (photonView != null && !photonView.IsMine)
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition, remoteLerpSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, remoteLerpSpeed * Time.deltaTime);
        }
#endif
    }

    public void ApplyDamageToNetworkPlayer(int damage, GameObject attackerRoot)
    {
#if PUN_2_OR_NEWER
        if (photonView == null)
            return;

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
        if (playerHealth == null || playerHealth.currentHealth <= 0f)
            return;

        playerHealth.currentHealth = Mathf.Max(0f, playerHealth.currentHealth - Mathf.Max(1, damage));

        if (photonView.IsMine && HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateHealth(playerHealth.currentHealth, playerHealth.maxHealth);
            HUDManager.Instance.ShowDamageFlash(damage);
        }

        if (playerHealth.currentHealth <= 0f && !deathRecorded)
        {
            deathRecorded = true;
            if (MatchStatsManager.Instance != null)
            {
                MatchStatsManager.Instance.MarkEliminated(CombatantId);
                if (attackerActorNumber > 0)
                {
                    string killerId = $"photon:{attackerActorNumber}";
                    MatchStatsManager.Instance.RegisterCombatant(killerId, attackerName, isPlayer: true);
                    MatchStatsManager.Instance.RecordKill(killerId);
                }
            }

            if (playerController != null)
                playerController.enabled = false;
        }
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
#if PUN_2_OR_NEWER
        if (photonView != null && photonView.Owner != null && !string.IsNullOrWhiteSpace(photonView.Owner.NickName))
            displayName = photonView.Owner.NickName;
        else
#endif
        if (PlayerProfile.HasUsername)
            displayName = PlayerProfile.Username;

        MatchStatsManager.Instance.RegisterCombatant(CombatantId, displayName, isPlayer: true, transform: transform);
    }
}
