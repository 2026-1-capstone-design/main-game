using UnityEngine;

public enum SkinPart
{
    FullHead = 0, // 통짜 머리(투구 등)
    Nose = 1, // 코
    Hair = 2, // 헤어
    Face = 3, // 얼굴형
    Eyes = 4, // 눈
    Eyebrows = 5, // 눈썹
    Ears = 6, // 귀
    Chest = 7, // 가슴
    Arms = 8, // 팔
    Belt = 9, // 벨트
    Legs = 10, // 다리
    Feet = 11, // 발
    TotalCount = 12, // 배열의 총 크기
}

[DisallowMultipleComponent]
public sealed class GladiatorSkinManager : SingletonBehaviour<GladiatorSkinManager>
{
    // (선택) 만약 기획이 바뀌어 파츠 개수가 늘어나면 여기서 숫자만 바꾸시면 됩니다!
    [Header("Skin Part Counts")]
    public int fullHeadCount = 18;
    public int noseCount = 5;
    public int hairCount = 25;
    public int faceCount = 25;
    public int eyesCount = 25;
    public int eyebrowsCount = 25;
    public int earsCount = 2;

    public int chestCount = 18;
    public int armsCount = 18;
    public int beltCount = 18;
    public int legsCount = 18;
    public int feetCount = 18;

    /// <summary>
    /// 무작위로 스킨 파츠 인덱스 배열을 생성하여 반환합니다.
    /// </summary>
    public int[] GenerateRandomSkinIndicates()
    {
        // 12칸짜리 빈 배열 생성
        int[] skinIndices = new int[(int)SkinPart.TotalCount];

        // 1. 머리 vs 세부 얼굴 양자택일 (50% 확률)
        bool useFullHead = Random.value < 0.5f;

        if (useFullHead)
        {
            // 통짜 머리 당첨: 통짜 머리 번호 지정, 나머지 세부 파츠는 -1(장착 안함) 처리
            skinIndices[(int)SkinPart.FullHead] = Random.Range(0, fullHeadCount);

            skinIndices[(int)SkinPart.Nose] = -1;
            skinIndices[(int)SkinPart.Hair] = -1;
            skinIndices[(int)SkinPart.Face] = -1;
            skinIndices[(int)SkinPart.Eyes] = -1;
            skinIndices[(int)SkinPart.Eyebrows] = -1;
            skinIndices[(int)SkinPart.Ears] = -1;
        }
        else
        {
            // 세부 얼굴 당첨: 통짜 머리는 -1, 각 세부 파츠별로 번호 지정
            skinIndices[(int)SkinPart.FullHead] = -1;

            skinIndices[(int)SkinPart.Nose] = Random.Range(0, noseCount);
            skinIndices[(int)SkinPart.Hair] = Random.Range(0, hairCount);
            skinIndices[(int)SkinPart.Face] = Random.Range(0, faceCount);
            skinIndices[(int)SkinPart.Eyes] = Random.Range(0, eyesCount);
            skinIndices[(int)SkinPart.Eyebrows] = Random.Range(0, eyebrowsCount);
            skinIndices[(int)SkinPart.Ears] = Random.Range(0, earsCount);
        }

        // 2. 공통 바디 파츠 (무조건 장착)
        skinIndices[(int)SkinPart.Chest] = Random.Range(0, chestCount);
        skinIndices[(int)SkinPart.Arms] = Random.Range(0, armsCount);
        skinIndices[(int)SkinPart.Belt] = Random.Range(0, beltCount);
        skinIndices[(int)SkinPart.Legs] = Random.Range(0, legsCount);
        skinIndices[(int)SkinPart.Feet] = Random.Range(0, feetCount);

        return skinIndices;
    }
}
