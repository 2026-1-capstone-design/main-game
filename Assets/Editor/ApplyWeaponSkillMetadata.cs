// WeaponSkillSO SOT 메타데이터를 일괄 적용함
// Assets/Content/WeaponSkills 아래의 지정된 에셋만 수정
// Unity Editor 메뉴에서 1회 실행하는 용도임
// 실행 후 변경된 에셋을 Dirty 처리하고 저장


using UnityEditor;
using UnityEngine;

public static class ApplyWeaponSkillSotMetadata
{
    private readonly struct SkillMetadata
    {
        public readonly string Path;
        public readonly bool IsSkillOnSelf;
        public readonly bool IsSkillOnOtherAlly;
        public readonly bool IsSkillAoe;
        public readonly bool CanSkillTargetDead;

        public SkillMetadata(
            string path,
            bool isSkillOnSelf,
            bool isSkillOnOtherAlly,
            bool isSkillAoe,
            bool canSkillTargetDead
        )
        {
            Path = path;
            IsSkillOnSelf = isSkillOnSelf;
            IsSkillOnOtherAlly = isSkillOnOtherAlly;
            IsSkillAoe = isSkillAoe;
            CanSkillTargetDead = canSkillTargetDead;
        }
    }

    private static readonly SkillMetadata[] Metadatas =
    {
        new SkillMetadata("Assets/Content/WeaponSkills/SwipeAttack.asset", true, false, false, false),
        new SkillMetadata("Assets/Content/WeaponSkills/Madness.asset", true, false, false, false),
        new SkillMetadata("Assets/Content/WeaponSkills/Longgrip.asset", true, false, false, false),
        new SkillMetadata("Assets/Content/WeaponSkills/Taunt.asset", true, false, false, false),
        new SkillMetadata("Assets/Content/WeaponSkills/HeartAttack.asset", false, false, false, false),
        new SkillMetadata("Assets/Content/WeaponSkills/Warcry.asset", true, false, true, false),
        new SkillMetadata("Assets/Content/WeaponSkills/ThroatSlit.asset", false, false, false, false),
        new SkillMetadata("Assets/Content/WeaponSkills/RustyBlade.asset", false, false, false, false),
        new SkillMetadata("Assets/Content/WeaponSkills/Fireball.asset", false, false, false, false),
        new SkillMetadata("Assets/Content/WeaponSkills/Lightning.asset", false, false, true, false),
        new SkillMetadata("Assets/Content/WeaponSkills/Stimpack.asset", true, false, false, false),
        new SkillMetadata("Assets/Content/WeaponSkills/Fanning.asset", false, false, false, false),
        new SkillMetadata("Assets/Content/WeaponSkills/BayonetCharge.asset", true, false, false, false),
        new SkillMetadata("Assets/Content/WeaponSkills/ShieldBash.asset", false, false, false, false),
        new SkillMetadata("Assets/Content/WeaponSkills/HeadStrike.asset", false, false, false, false),
        new SkillMetadata("Assets/Content/WeaponSkills/SpiralSlash.asset", true, false, true, false),
    };

    [MenuItem("Tools/Battle/Apply WeaponSkill SOT Metadata")]
    public static void Apply()
    {
        int appliedCount = 0;
        int missingCount = 0;

        foreach (SkillMetadata metadata in Metadatas)
        {
            WeaponSkillSO asset = AssetDatabase.LoadAssetAtPath<WeaponSkillSO>(metadata.Path);

            if (asset == null)
            {
                Debug.LogError($"[WeaponSkill SOT Metadata] Asset not found or invalid: {metadata.Path}");
                missingCount++;
                continue;
            }

            Undo.RecordObject(asset, "Apply WeaponSkill SOT Metadata");

            asset.isSkillOnSelf = metadata.IsSkillOnSelf;
            asset.isSkillOnOtherAlly = metadata.IsSkillOnOtherAlly;
            asset.isSkillAoe = metadata.IsSkillAoe;
            asset.canSkillTargetDead = metadata.CanSkillTargetDead;

            EditorUtility.SetDirty(asset);
            appliedCount++;
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[WeaponSkill SOT Metadata] Applied: {appliedCount}, Missing/Invalid: {missingCount}"
        );
    }
}
