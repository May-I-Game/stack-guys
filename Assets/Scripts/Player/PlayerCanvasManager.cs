using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerCanvasManager : NetworkBehaviour
{
    [Header("Name")]
    [SerializeField] private TMP_Text nameText;

    [Header("Sticker")]
    [SerializeField] private Image stickerImage; // 스티커 UI
    [SerializeField] private Sprite[] stickerSprites; // 스티커 Sprite 배열
    [SerializeField] private float stickerDuration = 3.0f; // 스티커 표시 시간
    [SerializeField] private AudioSource stickerAudioSource; // 이모티콘 효과음 소스
    [SerializeField] private AudioClip stickerSoundClip; // 이모티콘 효과음
    [Range(0f, 1f)][SerializeField] private float stickerVolume = 0.7f; // 이모티콘 효과음 볼륨

    [SerializeField] private GameObject playerArrow; // 플레이어를 가르키는 화살표

    private Coroutine runningStickerCoroutine = null;

    //자동 동기화
    private NetworkVariable<FixedString64Bytes> playerName =
        new NetworkVariable<FixedString64Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> arrowActivated =
        new NetworkVariable<bool>(true, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 플레이어를 가르키는 화살표 활성화
        if (IsOwner)
        {
            if (playerArrow != null)
            {
                playerArrow.SetActive(true);
            }
        }

        Debug.Log($"[PlayerNameSync] OnNetworkSpawn - Name: {playerName.Value}");

        UpdateNameDisplay(playerName.Value.ToString());

        //값 변경 감지
        playerName.OnValueChanged += OnNameChanged;
        arrowActivated.OnValueChanged += OnArrowActivatedChanged;
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnNameChanged;
        arrowActivated.OnValueChanged -= OnArrowActivatedChanged;
        base.OnNetworkDespawn();
    }

    [ServerRpc]
    public void RequestStickerServerRpc(int index)
    {
        // 모든 클라이언트에게 이모티콘 표시 명령
        ShowStickerClientRpc(index);
    }

    [ClientRpc]
    private void ShowStickerClientRpc(int index)
    {
        // 이모티콘 효과음 먼저 재생 (즉시 반응)
        if (stickerAudioSource != null && stickerSoundClip != null)
        {
            stickerAudioSource.PlayOneShot(stickerSoundClip, stickerVolume);
        }

        // 예전 코루틴이 있으면 중지
        if (runningStickerCoroutine != null)
        {
            StopCoroutine(runningStickerCoroutine);
        }
        // 이 코드는 모든 클라이언트에서 실행됨
        // 코루틴을 시작해서 이모티콘을 껐다 켬
        runningStickerCoroutine = StartCoroutine(ShowStickerCoroutine(index));
    }

    private IEnumerator ShowStickerCoroutine(int index)
    {
        // 선택된 이모티콘 활성화
        stickerImage.gameObject.SetActive(true);
        stickerImage.sprite = stickerSprites[index];

        // 정해진 시간(emoticonDuration)만큼 대기
        yield return new WaitForSeconds(stickerDuration);

        // 다시 비활성화
        stickerImage.gameObject.SetActive(false);
        runningStickerCoroutine = null;
    }

    //서버만 호출
    public void SetPlayerName(string name)
    {
        if (!IsServer) return;

        playerName.Value = name;
        Debug.Log($"[Server] PlayerName NetworkVariable set to: {name}");
    }

    private void UpdateNameDisplay(string name)
    {
        if (!IsClient) return;

        if (nameText != null)
        {
            nameText.text = name;
            Debug.Log($"[Client] Name displayed: {name}");
        }
        else
        {
            Debug.LogError("[Client] NameText is null!");
        }
    }

    public void ToggleArrow(bool activate)
    {
        arrowActivated.Value = activate;
    }

    private void OnNameChanged(FixedString64Bytes oldName, FixedString64Bytes newName)
    {
        Debug.Log($"[Client] name changed: {oldName} -> {newName}");
        UpdateNameDisplay(newName.ToString());
    }

    private void OnArrowActivatedChanged(bool oldValue, bool newValue)
    {
        if (!IsClient || !IsOwner) return;
        playerArrow.SetActive(newValue);
    }
}
