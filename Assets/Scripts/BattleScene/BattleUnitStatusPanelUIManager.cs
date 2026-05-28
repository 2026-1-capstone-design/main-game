using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// BattleUnitStatusPanelUIManager는 실전용 ally/enemy status root UI를 갱신한다.
// 기존 BattleStatusGridUIManager는 디버그 그리드 전용이므로, 전투 화면용 카드 UI와 분리해서 관리한다.
[DisallowMultipleComponent]
public sealed class BattleUnitStatusPanelUIManager : MonoBehaviour
{
    [Header("Ally Status Roots")]
    [SerializeField]
    private UnitStatusView[] allyStatusViews = new UnitStatusView[BattleTeamConstants.MaxUnitsPerTeam];

    [Header("Enemy Status Roots")]
    [SerializeField]
    private UnitStatusView[] enemyStatusViews = new UnitStatusView[BattleTeamConstants.MaxUnitsPerTeam];

    private ContentDatabaseProvider _contentDatabaseProvider;

    private void Awake()
    {
        _contentDatabaseProvider = ContentDatabaseProvider.Instance;
        Clear();
    }

    private void OnDestroy()
    {
        UnbindAll();
    }

    public void Initialize(IReadOnlyList<BattleRuntimeUnit> runtimeUnits, BattleStartPayload payload)
    {
        UnbindAll();

        _contentDatabaseProvider = ContentDatabaseProvider.Instance;
        ClearViews(allyStatusViews);
        ClearViews(enemyStatusViews);

        if (runtimeUnits == null || payload == null)
        {
            return;
        }

        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = runtimeUnits[i];
            if (unit == null)
            {
                continue;
            }

            UnitStatusView[] targetViews = unit.IsPlayerOwned ? allyStatusViews : enemyStatusViews;
            if (!TryResolveSlotIndex(unit, payload, out int slotIndex))
            {
                slotIndex = FindFirstEmptyViewIndex(targetViews);
            }

            if (slotIndex < 0 || slotIndex >= targetViews.Length)
            {
                continue;
            }

            targetViews[slotIndex]?.Bind(unit, ResolveSkillIcon(unit.Snapshot));
        }
    }

    public void Clear()
    {
        UnbindAll();
        ClearViews(allyStatusViews);
        ClearViews(enemyStatusViews);
    }

    private static bool TryResolveSlotIndex(BattleRuntimeUnit unit, BattleStartPayload payload, out int slotIndex)
    {
        slotIndex = -1;
        if (unit == null || payload == null)
        {
            return false;
        }

        return payload.TryGetTeamSlotIndex(unit.TeamId, unit.UnitNumber, out slotIndex);
    }

    private static int FindFirstEmptyViewIndex(UnitStatusView[] views)
    {
        if (views == null)
        {
            return -1;
        }

        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && !views[i].HasBoundUnit)
            {
                return i;
            }
        }

        return -1;
    }

    private Sprite ResolveSkillIcon(BattleUnitSnapshot snapshot)
    {
        if (snapshot == null || snapshot.WeaponSkillId == WeaponSkillId.None || _contentDatabaseProvider == null)
        {
            return null;
        }

        IReadOnlyList<WeaponSkillSO> skills = _contentDatabaseProvider.WeaponSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            WeaponSkillSO skill = skills[i];
            if (skill != null && skill.skillId == snapshot.WeaponSkillId)
            {
                return skill.icon;
            }
        }

        return null;
    }

    private static void ClearViews(UnitStatusView[] views)
    {
        if (views == null)
        {
            return;
        }

        for (int i = 0; i < views.Length; i++)
        {
            views[i]?.Clear();
        }
    }

    private void UnbindAll()
    {
        UnbindViews(allyStatusViews);
        UnbindViews(enemyStatusViews);
    }

    private static void UnbindViews(UnitStatusView[] views)
    {
        if (views == null)
        {
            return;
        }

        for (int i = 0; i < views.Length; i++)
        {
            views[i]?.Unbind();
        }
    }

    // UnitStatusView는 status root 하나의 텍스트, 아이콘, 모델 프리뷰, 체력바 참조를 묶는다.
    [Serializable]
    private sealed class UnitStatusView
    {
        [SerializeField]
        private GameObject statusRoot;

        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private RawImage weaponIcon;

        [SerializeField]
        private WeaponModelPreviewView weaponPreviewView;

        [SerializeField]
        private RawImage skillIcon;

        [SerializeField]
        private RawImage modelView;

        [SerializeField]
        private GladiatorModelPreviewView modelPreviewView;

        [SerializeField]
        private GameObject healthBarRoot;

        [SerializeField]
        private Image blackBackground;

        [SerializeField]
        private Image redFillImage;

        [NonSerialized]
        private BattleRuntimeUnit _boundUnit;

        [NonSerialized]
        private Action<float> _healthChangedHandler;

        public bool HasBoundUnit => _boundUnit != null;

        public void Bind(BattleRuntimeUnit unit, Sprite skillIconSprite)
        {
            Unbind();
            _boundUnit = unit;
            SetRootActive(true);

            if (unit == null)
            {
                Clear();
                return;
            }

            BattleUnitSnapshot snapshot = unit.Snapshot;
            if (nameText != null)
            {
                nameText.text = BuildPersonalityNameText(unit, nameText.color);
            }

            SetWeaponPreview(snapshot);
            SetSkillIcon(skillIconSprite);
            SetModelPreview(snapshot);
            UpdateHealthBar();

            if (unit.State != null)
            {
                _healthChangedHandler = _ => UpdateHealthBar();
                unit.State.OnHealthChanged += _healthChangedHandler;
            }
        }

        public void Unbind()
        {
            if (_boundUnit != null && _boundUnit.State != null && _healthChangedHandler != null)
            {
                _boundUnit.State.OnHealthChanged -= _healthChangedHandler;
            }

            _boundUnit = null;
            _healthChangedHandler = null;
        }

        public void Clear()
        {
            Unbind();

            if (nameText != null)
            {
                nameText.text = string.Empty;
            }

            if (weaponPreviewView != null)
            {
                weaponPreviewView.Clear();
            }

            SetRawImageSprite(skillIcon, null);

            if (modelPreviewView != null)
            {
                modelPreviewView.Clear();
            }

            SetHealthRatio(0f);
            SetRootActive(false);
        }

        private void SetWeaponPreview(BattleUnitSnapshot snapshot)
        {
            if (weaponPreviewView != null)
            {
                weaponPreviewView.Show(snapshot?.LeftWeaponPrefab, snapshot?.RightWeaponPrefab);
            }

            if (weaponIcon != null)
            {
                bool hasWeapon =
                    snapshot != null && (snapshot.LeftWeaponPrefab != null || snapshot.RightWeaponPrefab != null);
                weaponIcon.enabled = hasWeapon;
            }
        }

        private void SetSkillIcon(Sprite icon)
        {
            SetRawImageSprite(skillIcon, icon);
        }

        private void SetModelPreview(BattleUnitSnapshot snapshot)
        {
            GameObject modelPrefab =
                snapshot?.GladiatorClass != null ? snapshot.GladiatorClass.previewModelPrefab : null;
            if (modelPreviewView != null && modelPrefab != null)
            {
                modelPreviewView.Show(
                    modelPrefab,
                    snapshot.CustomizeIndicates,
                    snapshot.LeftWeaponPrefab,
                    snapshot.RightWeaponPrefab
                );
            }
            else if (modelPreviewView != null)
            {
                modelPreviewView.Clear();
            }

            if (modelView != null)
            {
                modelView.enabled = modelPrefab != null;
            }
        }

        private void UpdateHealthBar()
        {
            if (_boundUnit == null || _boundUnit.MaxHealth <= 0f)
            {
                SetHealthRatio(0f);
                return;
            }

            SetHealthRatio(Mathf.Clamp01(_boundUnit.CurrentHealth / _boundUnit.MaxHealth));
        }

        private void SetHealthRatio(float ratio)
        {
            if (healthBarRoot != null)
            {
                healthBarRoot.SetActive(true);
            }

            if (blackBackground != null)
            {
                blackBackground.enabled = true;
            }

            if (redFillImage == null)
            {
                return;
            }

            redFillImage.enabled = true;
            redFillImage.fillAmount = ratio;

            RectTransform fillRect = redFillImage.rectTransform;
            if (fillRect != null)
            {
                Vector2 anchorMax = fillRect.anchorMax;
                anchorMax.x = ratio;
                fillRect.anchorMax = anchorMax;
            }
        }

        private void SetRootActive(bool active)
        {
            if (statusRoot != null)
            {
                statusRoot.SetActive(active);
            }
        }

        private static void SetRawImageSprite(RawImage image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            if (sprite == null)
            {
                image.texture = null;
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                image.enabled = false;
                return;
            }

            Texture texture = sprite.texture;
            Rect textureRect = sprite.textureRect;
            image.texture = texture;
            image.uvRect = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height
            );
            image.enabled = true;
        }

        private static string BuildPersonalityNameText(BattleRuntimeUnit unit, Color nameColor)
        {
            if (unit == null)
            {
                return string.Empty;
            }

            BattleUnitSnapshot snapshot = unit.Snapshot;
            string personalityName =
                snapshot != null
                && snapshot.Personality != null
                && !string.IsNullOrWhiteSpace(snapshot.Personality.personalityName)
                    ? snapshot.Personality.personalityName
                    : "성격 없음";
            string nameColorHtml = ColorUtility.ToHtmlStringRGB(nameColor);

            return $"<size=18><color=#FFFFFF>{personalityName}</color></size> <color=#{nameColorHtml}>{unit.DisplayName}</color>";
        }
    }
}
