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

    private Coroutine runningStickerCoroutine = null;

    //자동 동기화
    private NetworkVariable<FixedString64Bytes> playerName =
        new NetworkVariable<FixedString64Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"[PlayerNameSync] OnNetworkSpawn - Name: {playerName.Value}");

        UpdateNameDisplay(playerName.Value.ToString());

        //값 변경 감지
        playerName.OnValueChanged += OnNameChanged;
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

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnNameChanged;
        base.OnNetworkDespawn();
    }

    private void OnNameChanged(FixedString64Bytes oldName, FixedString64Bytes newName)
    {
        Debug.Log($"[Client] name changed: {oldName} -> {newName}");
        UpdateNameDisplay(newName.ToString());
    }
}
