// WeaponSkillSO 설명과 SOT 메타데이터를 일괄 적용함
// Assets/Content/WeaponSkills 아래의 55개 에셋을 대상으로 함
// Unity Editor 메뉴에서 1회 실행하는 용도임
// 실행 후 변경된 에셋을 Dirty 처리하고 저장함

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ApplyWeaponSkillSotMetadata
{
    private const int ExpectedSkillCount = 55;

    private static readonly string[] SearchFolders = { "Assets/Content/WeaponSkills" };

    private readonly struct SkillMetadata
    {
        public readonly string Path;
        public readonly string Description;
        public readonly bool IsSkillOnSelf;
        public readonly bool IsSkillOnOtherAlly;
        public readonly bool IsSkillAoe;
        public readonly bool CanSkillTargetDead;

        public SkillMetadata(
            string path,
            string description,
            bool isSkillOnSelf,
            bool isSkillOnOtherAlly,
            bool isSkillAoe,
            bool canSkillTargetDead
        )
        {
            Path = path;
            Description = description;
            IsSkillOnSelf = isSkillOnSelf;
            IsSkillOnOtherAlly = isSkillOnOtherAlly;
            IsSkillAoe = isSkillAoe;
            CanSkillTargetDead = canSkillTargetDead;
        }
    }

    private static readonly SkillMetadata[] Metadatas =
    {
        Skill(
            "Assets/Content/WeaponSkills/SwipeAttack.asset",
            "원형 범위의 적을 공격한다.",
            true,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Madness.asset",
            "자신에게 공격속도 및 데미지 버프를 적용한다.",
            true,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Longgrip.asset",
            "자신의 공격 사거리를 증가시킨다.",
            true,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Taunt.asset",
            "원형 범위의 적을 도발하여 자신을 공격하도록 만든다.",
            true,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/HeartAttack.asset",
            "단일 대상의 적에게 강력한 데미지를 준다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Warcry.asset",
            "아군 전체의 공격력을 증가시킨다.",
            true,
            true,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/ThroatSlit.asset",
            "적의 뒤로 순간이동한다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/RustyBlade.asset",
            "적에게 1레벨 출혈을 부여한다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Fireball.asset",
            "적에게 피해를 주는 화염구를 발사한다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Lightning.asset",
            "지정한 적을 기준으로 특정 사거리 이내의 모든 적에게 데미지를 주는 번개를 내리친다.",
            false,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Stimpack.asset",
            "자신의 공격속도를 증가시킨다.",
            true,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Fanning.asset",
            "지정한 적에게 6번 연속 사격한다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/BayonetCharge.asset",
            "공격속도, 공격력, 이동속도가 상승하고 사거리가 대폭 감소한다.",
            true,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/ShieldBash.asset",
            "지정한 대상을 강하게 넉백시키고 공격력을 감소시킨다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/HeadStrike.asset",
            "지정한 대상을 기절시킨다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/SpiralSlash.asset",
            "자신을 기준으로 일정 사거리 이내의 적에게 데미지를 주고 전부 넉백시킨다.",
            true,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/ContinuousSlash.asset",
            "지속적으로 적을 공격하여 이동속도를 감소시킨다. 사용하는 동안 공격 및 이동이 중지된다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/HighHeal.asset",
            "지정한 아군의 최대 체력 10%만큼 치유한다.",
            false,
            true,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/OmniousStar.asset",
            "자신에게 받는 피해 증가, 이동속도 증가, 공격력 증가 버프를 적용한다.",
            true,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/DarkShroud.asset",
            "시전 즉시 자신의 반경 n 이내의 모든 적에게 스킬 사용을 7초간 금지하는 디버프를 부여한다.",
            true,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Consecration.asset",
            "자신에게 걸린 모든 디버프를 즉시 해제하고 소량의 체력을 회복하며 모든 버프의 지속시간을 초기화한다.",
            true,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/HuntStart.asset",
            "8초간 지정한 대상이 아군으로부터 공격받을 때마다 최대 체력의 5%에 해당하는 피해를 추가로 받는다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/WarCommander.asset",
            "자신 주변 반경 n 이내의 적에게 이동속도와 공격력을 소폭 감소시키고, 아군의 이동속도와 공격력을 소폭 증가시킨다.",
            true,
            true,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Submersion.asset",
            "8초 동안 아군과 적군 모두에게 타겟으로 지정되지 않으며 이동속도가 증가한다. 단, 광역 피해는 그대로 받는다.",
            true,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/LeapOfFaith.asset",
            "체력이 가장 낮은 아군 곁으로 점프하여 보호 행동을 개시한다.",
            false,
            true,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/HookThrow.asset",
            "반경 n 이내에서 체력이 가장 낮은 적 또는 지정한 적을 자신에게 끌어온다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/ManaCollapse.asset",
            "10초간 반경 n 이내에서 공격하는 적은 최대 체력의 1%, 스킬을 시전하는 적은 최대 체력의 10%에 해당하는 피해를 입는다.",
            true,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/HolyBarrier.asset",
            "10초간 유지된다. 자신을 공격하는 적을 반경 n 거리만큼 넉백시킨다.",
            true,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Fear.asset",
            "대상을 5초간 자신의 반대 방향으로 강제 이동시킨다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/UnstoppableForce.asset",
            "지정한 지점으로 돌진하여 충돌한 적들을 공중으로 띄우고 광역 피해를 입힌다.",
            false,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Juggernaut.asset",
            "가장 멀리 있는 적 또는 지정한 대상에게 빠르게 이동하여 강력한 공격을 가한다. 이동 거리에 비례하여 데미지가 증가한다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Harvest.asset",
            "자신의 반경 n 이내의 적에게 공격력에 비례한 데미지를 주고, 가한 데미지의 30%를 체력으로 흡수한다.",
            true,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/HolyRevive.asset",
            "전투 불능 상태의 아군을 대상으로 최대 체력의 20%를 회복시켜 전투에 복귀시킨다. 쿨다운이 매우 길다.",
            false,
            true,
            false,
            true
        ),
        Skill(
            "Assets/Content/WeaponSkills/DeathWhirlPool.asset",
            "지속시간 동안 매 공격마다 반경 n 이내의 적 중 무작위 대상을 공격한다. 공격속도와 애니메이션 속도가 크게 증가한다.",
            true,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Retribution.asset",
            "5초간 방어 태세를 취하며 받은 피해를 누적한다. 지속시간 종료 시 주변 적에게 누적된 피해의 일정 비율로 공격한다.",
            true,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Surge.asset",
            "주변 반경 n 이내에서 체력이 가장 낮은 적 또는 지정한 대상에게 빠르게 이동하여 3회 참격 후 기절시킨다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/MagicExplosion.asset",
            "10초간 공격 시 공격 대상 주변 반경 n 이내의 적에게 50% 데미지의 연쇄 폭발을 일으킨다.",
            true,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Curse.asset",
            "대상에게 소량의 피해를 주고 이후 10초간 대상이 받는 피해를 30% 증가시킨다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/GlyphOfCounterattack.asset",
            "적에게 가장 많이 공격받는 아군 또는 지정한 아군에게 10초간 보호막을 부여하고, 피격 시 공격한 적에게 데미지를 반환한다.",
            false,
            true,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/NobleSacrifice.asset",
            "체력이 가장 낮거나 공격을 많이 받는 아군 또는 지정한 아군이 받을 피해를 10초간 대신 받는다.",
            false,
            true,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Duel.asset",
            "자신의 공격 대상 또는 지정한 적에게 주는 피해가 증가하고, 해당 적 이외의 적에게서 받는 피해가 감소한다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Freeze.asset",
            "8초간 지속. 반경 n 이내에서 이동을 시작하거나 이동기를 사용하는 모든 적에게 즉시 큰 데미지를 가한다.",
            true,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/MindControll.asset",
            "지정한 적 하나를 5초간 아군으로 편입시킨다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Oblivion.asset",
            "주변 반경 n 이내의 적에게 디버프를 부여한다. 이후 해당 적이 3회의 피해를 주는 행동을 할 때 데미지가 0이 된다.",
            true,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Respite.asset",
            "반경 n 이내의 모든 적과 아군의 체력이 5초간 1 미만으로 내려가지 않는다.",
            true,
            true,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/MoonlightDance.asset",
            "무작위 적 3명을 차례대로 고속 이동하며 공격한다. 기본적으로 유체화 상태이며 마지막 공격 대상 근처에 착지한다. 같은 대상은 연속 선택되지 않는다.",
            false,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/FanaticalObsession.asset",
            "15초간 지속. 공격 대상과의 거리가 공격 사거리를 초과하는 순간마다 즉시 대상 곁으로 고속 이동하여 1회 타격한다.",
            true,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/EvasiveManeuver.asset",
            "10초간 지속. 반경 n 이내에 적이 진입할 때마다 공격 사거리만큼의 거리가 확보되도록 뒤로 대쉬한다.",
            true,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/DimensionalRift.asset",
            "체력이 가장 낮은 아군과 즉시 위치를 교환한다. 자신은 최대 체력의 30% 피해를 입고, 착지 지점 주변 적에게 큰 피해와 2초 기절을 부여한다.",
            false,
            true,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Petrifying.asset",
            "주변 반경 n 이내에서 자신을 바라보는 적을 3초간 기절시킨다.",
            true,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/FallingStar.asset",
            "자신으로부터 가장 먼 적에게 돌진한다. 이동 궤적 상의 모든 적에게 큰 피해를 준다.",
            false,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Banishment.asset",
            "공격력이 가장 높은 적 하나를 경기장 가장자리로 즉시 이동시키고 이후 5초간 둔화를 적용한다.",
            false,
            false,
            false,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/Assemble.asset",
            "모든 아군을 자신 곁으로 빠르게 끌어당긴다. 모든 아군에게 5초간 받는 피해 감소 15%를 부여한다.",
            true,
            true,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/ScatteredStarlight.asset",
            "주변 반경 n 이내에서 가장 멀리 있는 적에게 돌진하며 피해를 준다. 최대 5회 반복하며 방금 공격한 적을 곧바로 다시 대상으로 지정하지 않는다.",
            false,
            false,
            true,
            false
        ),
        Skill(
            "Assets/Content/WeaponSkills/UnhloyBlessing.asset",
            "전투 불능 상태의 아군을 지정하여 모든 능력치의 30%만 가진 상태로 전투에 복귀시킨다. 복귀한 아군은 스킬 사용 및 명령 수행이 불가하다.",
            false,
            true,
            false,
            true
        ),
    };

    // 지정된 55개 WeaponSkillSO에 설명과 SOT 메타데이터를 적용함.
    [MenuItem("Tools/Battle/Apply WeaponSkill SOT Metadata")]
    public static void Apply()
    {
        int appliedCount = 0;
        int missingCount = 0;

        ValidateMetadataCount();
        WarnIfFolderHasUnmappedAssets();

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

            asset.description = metadata.Description;
            asset.isSkillOnSelf = metadata.IsSkillOnSelf;
            asset.isSkillOnOtherAlly = metadata.IsSkillOnOtherAlly;
            asset.isSkillAoe = metadata.IsSkillAoe;
            asset.canSkillTargetDead = metadata.CanSkillTargetDead;

            EditorUtility.SetDirty(asset);
            appliedCount++;
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[WeaponSkill SOT Metadata] Applied: {appliedCount}, Missing/Invalid: {missingCount}, Expected: {ExpectedSkillCount}"
        );
    }

    // 메타데이터 배열 크기가 예상 스킬 수와 다른 경우 즉시 로그로 드러냄.
    private static void ValidateMetadataCount()
    {
        if (Metadatas.Length != ExpectedSkillCount)
        {
            Debug.LogError(
                $"[WeaponSkill SOT Metadata] Metadata count mismatch. Actual: {Metadatas.Length}, Expected: {ExpectedSkillCount}"
            );
        }
    }

    // 폴더 안에 메타데이터 배열에 없는 WeaponSkillSO가 있으면 경로를 출력함.
    private static void WarnIfFolderHasUnmappedAssets()
    {
        HashSet<string> mappedPaths = new HashSet<string>();

        foreach (SkillMetadata metadata in Metadatas)
        {
            if (!mappedPaths.Add(metadata.Path))
            {
                Debug.LogError($"[WeaponSkill SOT Metadata] Duplicate metadata path: {metadata.Path}");
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:WeaponSkillSO", SearchFolders);

        if (guids.Length != ExpectedSkillCount)
        {
            Debug.LogWarning(
                $"[WeaponSkill SOT Metadata] Folder asset count differs from expected count. Actual: {guids.Length}, Expected: {ExpectedSkillCount}"
            );
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (!mappedPaths.Contains(path))
            {
                Debug.LogError($"[WeaponSkill SOT Metadata] Unmapped WeaponSkillSO asset: {path}");
            }
        }
    }

    private static SkillMetadata Skill(
        string path,
        string description,
        bool isSkillOnSelf,
        bool isSkillOnOtherAlly,
        bool isSkillAoe,
        bool canSkillTargetDead
    )
    {
        return new SkillMetadata(
            path,
            description,
            isSkillOnSelf,
            isSkillOnOtherAlly,
            isSkillAoe,
            canSkillTargetDead
        );
    }
}
