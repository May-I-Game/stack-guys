using Unity.Netcode;
using UnityEngine;

// 봇을 식별하기 위한 네트워크 컴포넌트
public class NetworkBotIdentity : NetworkBehaviour
{
    // 캐릭터가 봇인지 여부
    //[Header("Bot Settings")]
    //public bool IsBot = false;

    // 모든 인스턴스가 공유하는 카운터
    private static int botCounter = 0;

    // 봇 이름 생성
    public static string GenerateBotName()
    {
        string[] botNames = new string[]
        {
        "김민준","김서준","김도윤","김하준","김지호",
        "김유준","김승우","김현우","김지후","김시우",
        "이서연","이지우","이서윤","이하은","이지민",
        "이다현","이지윤","이채원","이민서","이윤서",
        "박민준","박시우","박도윤","박지호","박예준",
        "박하윤","박서연","박지우","박민서","박연우",
        "최민재","최서준","최지후","최예린","최수아",
        "최하은","최유나","최지안","최다은","최윤아",
        "정민규","정도윤","정하준","정지원","정서윤",
        "정예린","정지후","정시우","정수현","정유진",
        "조민준","조하율","조지원","조예은","조서윤",
        "조태윤","조하린","조은우","조현우","조유나",
        "강민수","강지훈","강서연","강하늘","강예원",
        "강도윤","강시윤","강현서","강태민","강수아",
        "윤서준","윤도현","윤하율","윤지우","윤채원",
        "윤민재","윤지안","윤수빈","윤서영","윤하람",
        "장민호","장도윤","장서연","장예나","장지후",
        "장유진","장하린","장수현","장태영","장민서",
        "한서준","한지우","한도윤","한채원","한유진",
        "한수민","한하윤","한민서","한예린",
        };

        int nameIndex = botCounter % botNames.Length;
        botCounter++;

        // return $"{botNames[nameIndex]}_{botCounter:D2}";
        return $"{botNames[nameIndex]}";
    }

    // 다른 스크립트에서 봇 여부를 확인하는 헬퍼 함수
    //public static bool IsPlayerBot(GameObject player)
    //{
    //    if (player == null) return false;

    //    var botIdentity = player.GetComponent<NetworkBotIdentity>();
    //    return botIdentity != null && botIdentity.IsBot;
    //}
}