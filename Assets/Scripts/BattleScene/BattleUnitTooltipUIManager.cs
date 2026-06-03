using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// BattleScene의 3D 전투 유닛 정보 팝업을 관리한다.
// MainScene BattlePanel의 툴팁 구성을 재사용하되, 월드 모델 우클릭으로 고정 위치 팝업을 연다.
[DisallowMultipleComponent]
public sealed class BattleUnitTooltipUIManager : MonoBehaviour
{
    private enum DetailTab
    {
        Personality,
        Weapon,
        WeaponSkill,
    }

    [Header("Click")]
    [SerializeField]
    private Camera raycastCamera;

    [SerializeField]
    private LayerMask unitLayerMask = ~0;

    [SerializeField]
    private BattleSceneUIManager battleSceneUIManager;

    [SerializeField]
    [Min(1f)]
    private float maxRaycastDistance = 500f;

    [SerializeField]
    [Min(0.1f)]
    private float fallbackHoverColliderHeight = 2.4f;

    [SerializeField]
    [Min(0.05f)]
    private float fallbackHoverColliderRadius = 0.6f;

    [Header("Tooltip")]
    [SerializeField]
    private RectTransform tooltipRoot;

    [SerializeField]
    private RawImage tooltipIcon;

    [SerializeField]
    private GladiatorModelPreviewView tooltipModelPreviewView;

    [SerializeField]
    private TMP_Text personalityNameText;

    [SerializeField]
    private TMP_Text personalityText;

    [SerializeField]
    private TMP_Text levelText;

    [SerializeField]
    private RawImage levelIcon;

    [SerializeField]
    private TMP_Text attackText;

    [SerializeField]
    private RawImage attackIcon;

    [SerializeField]
    private TMP_Text attackSpeedText;

    [SerializeField]
    private RawImage attackSpeedIcon;

    [SerializeField]
    private TMP_Text moveSpeedText;

    [SerializeField]
    private RawImage moveSpeedIcon;

    [SerializeField]
    private TMP_Text rangeText;

    [SerializeField]
    private RawImage rangeIcon;

    [Header("Health Bar")]
    [SerializeField]
    private GameObject healthBarRoot;

    [SerializeField]
    private Image healthBarBlackBackground;

    [SerializeField]
    private Image healthBarRedFillImage;

    [SerializeField]
    private TMP_Text healthBarText;

    [Header("Details")]
    [SerializeField]
    private Button personalityDetailImage;

    [SerializeField]
    private Button weaponImageIcon;

    [SerializeField]
    private Button weaponSkillImageIcon;

    [SerializeField]
    private TMP_Text selectedTitleText;

    [SerializeField]
    private TMP_Text selectedDetailText;

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog;

    private readonly List<BattleRuntimeUnit> _runtimeUnits = new List<BattleRuntimeUnit>();
    private BattleRuntimeUnit _selectedUnit;
    private bool _initialized;
    private Vector3 _healthBarRedFillBaseScale = Vector3.one;
    private bool _hasHealthBarRedFillBaseScale;
    private ContentDatabaseProvider _contentDatabaseProvider;
    private Coroutine _temporaryHideCoroutine;
    private bool _isTemporarilyHidden;

    private void Awake()
    {
        WireDetailButtons();
        ConfigureHealthBarFillImage();
        HideTooltip();
    }

    private void OnDestroy()
    {
        UnwireDetailButtons();
    }

    private void Update()
    {
        if (!_initialized)
        {
            return;
        }

        if (IsBattleEndUiOpen())
        {
            HideTooltip();
            return;
        }

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            BattleRuntimeUnit clickedUnit = IsPointerOverUi() ? null : ResolvePointedUnit();
            if (clickedUnit != null)
            {
                ShowTooltip(clickedUnit);
            }
            else
            {
                HideTooltip();
            }
        }

        if (_selectedUnit != null)
        {
            RefreshHealthBar(_selectedUnit);
        }
    }

    public void Initialize(IReadOnlyList<BattleRuntimeUnit> runtimeUnits)
    {
        CancelTemporaryHide();
        _runtimeUnits.Clear();

        if (runtimeUnits != null)
        {
            for (int i = 0; i < runtimeUnits.Count; i++)
            {
                BattleRuntimeUnit unit = runtimeUnits[i];
                if (unit == null)
                {
                    continue;
                }

                _runtimeUnits.Add(unit);
                EnsureHoverCollider(unit);
            }
        }

        if (tooltipModelPreviewView == null && tooltipIcon != null)
        {
            tooltipModelPreviewView = tooltipIcon.GetComponentInChildren<GladiatorModelPreviewView>(true);
        }

        _selectedUnit = null;
        _contentDatabaseProvider = ContentDatabaseProvider.Instance;
        _initialized = true;
        HideTooltip();
    }

    public void Clear()
    {
        CancelTemporaryHide();
        _runtimeUnits.Clear();
        _initialized = false;
        HideTooltip();
    }

    private bool IsBattleEndUiOpen()
    {
        if (battleSceneUIManager == null)
        {
            battleSceneUIManager = FindFirstObjectByType<BattleSceneUIManager>(FindObjectsInactive.Include);
        }

        return battleSceneUIManager != null && battleSceneUIManager.IsBattleEndPanelOpen;
    }

    private BattleRuntimeUnit ResolvePointedUnit()
    {
        if (Mouse.current == null)
        {
            return null;
        }

        Camera camera = ResolveRaycastCamera();
        if (camera == null)
        {
            return null;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Ray ray = camera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRaycastDistance, unitLayerMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            BattleRuntimeUnit unit = hitCollider != null ? hitCollider.GetComponentInParent<BattleRuntimeUnit>() : null;
            if (unit != null)
            {
                return unit;
            }
        }

        return null;
    }

    private Camera ResolveRaycastCamera()
    {
        if (raycastCamera != null)
        {
            return raycastCamera;
        }

        raycastCamera = Camera.main;
        return raycastCamera;
    }

    private void ShowTooltip(BattleRuntimeUnit unit)
    {
        _selectedUnit = unit;

        if (tooltipRoot == null)
        {
            if (verboseLog)
            {
                Debug.LogWarning("[BattleUnitTooltipUIManager] Tooltip Root is not assigned.", this);
            }

            return;
        }

        if (_isTemporarilyHidden)
        {
            return;
        }

        SetUnitPreview(unit);
        RefreshTooltipStats(unit);
        RefreshDetailIcons(unit);
        RefreshSelectedDetail(DetailTab.Personality);
        SetTooltipStatIconsActive(true);
        tooltipRoot.gameObject.SetActive(true);
        tooltipRoot.SetAsLastSibling();
    }

    public void HideTemporarily(float seconds)
    {
        if (_temporaryHideCoroutine != null)
        {
            StopCoroutine(_temporaryHideCoroutine);
        }

        _isTemporarilyHidden = true;
        HideTooltipVisuals();
        _temporaryHideCoroutine = StartCoroutine(RestoreTooltipAfterDelay(Mathf.Max(0f, seconds)));
    }

    private void CancelTemporaryHide()
    {
        if (_temporaryHideCoroutine != null)
        {
            StopCoroutine(_temporaryHideCoroutine);
            _temporaryHideCoroutine = null;
        }

        _isTemporarilyHidden = false;
    }

    private IEnumerator RestoreTooltipAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _temporaryHideCoroutine = null;
        _isTemporarilyHidden = false;

        if (_selectedUnit != null && !IsBattleEndUiOpen())
        {
            ShowTooltip(_selectedUnit);
        }
    }

    private void HideTooltip()
    {
        _selectedUnit = null;
        HideTooltipVisuals();
    }

    private void HideTooltipVisuals()
    {
        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(false);
        }

        if (tooltipModelPreviewView != null)
        {
            tooltipModelPreviewView.Clear();
        }

        if (
            tooltipIcon != null
            && (tooltipModelPreviewView == null || !tooltipModelPreviewView.UsesTargetImage(tooltipIcon))
        )
        {
            tooltipIcon.texture = null;
            tooltipIcon.enabled = false;
        }

        SetText(selectedTitleText, string.Empty);
        SetText(selectedDetailText, string.Empty);
    }

    private void SetUnitPreview(BattleRuntimeUnit unit)
    {
        BattleUnitSnapshot snapshot = unit != null ? unit.Snapshot : null;
        GameObject modelPrefab =
            snapshot != null && snapshot.GladiatorClass != null ? snapshot.GladiatorClass.previewModelPrefab : null;
        bool useModelPreview = tooltipModelPreviewView != null && modelPrefab != null;

        if (tooltipModelPreviewView != null)
        {
            if (useModelPreview)
            {
                tooltipModelPreviewView.Show(
                    modelPrefab,
                    snapshot.CustomizeIndicates,
                    snapshot.LeftWeaponPrefab,
                    snapshot.RightWeaponPrefab
                );
            }
            else
            {
                tooltipModelPreviewView.Clear();
            }
        }

        if (tooltipIcon == null)
        {
            return;
        }

        if (useModelPreview)
        {
            if (!tooltipModelPreviewView.UsesTargetImage(tooltipIcon))
            {
                tooltipIcon.texture = null;
                tooltipIcon.enabled = false;
            }

            return;
        }

        Sprite fallbackSprite = snapshot != null ? snapshot.PortraitSprite : null;
        if (fallbackSprite == null || fallbackSprite.texture == null)
        {
            tooltipIcon.texture = null;
            tooltipIcon.enabled = false;
            return;
        }

        Rect textureRect = fallbackSprite.textureRect;
        tooltipIcon.texture = fallbackSprite.texture;
        tooltipIcon.uvRect = new Rect(
            textureRect.x / fallbackSprite.texture.width,
            textureRect.y / fallbackSprite.texture.height,
            textureRect.width / fallbackSprite.texture.width,
            textureRect.height / fallbackSprite.texture.height
        );
        tooltipIcon.enabled = true;
    }

    private void RefreshTooltipStats(BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        SetText(personalityNameText, BuildUnitNameText(unit));
        SetText(personalityText, ResolvePersonalityName(unit));
        SetText(levelText, unit.Level.ToString());
        SetText(attackText, FormatStat(unit.Attack));
        RefreshHealthBar(unit);
        SetText(attackSpeedText, FormatStat(unit.AttackSpeed));
        SetText(moveSpeedText, FormatStat(unit.MoveSpeed));
        SetText(rangeText, FormatStat(unit.AttackRange));
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void SetTooltipStatIconsActive(bool value)
    {
        SetComponentActive(levelIcon, value);
        SetComponentActive(attackIcon, value);
        SetComponentActive(attackSpeedIcon, value);
        SetComponentActive(moveSpeedIcon, value);
        SetComponentActive(rangeIcon, value);
    }

    private void EnsureHoverCollider(BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        Collider existingCollider = unit.GetComponent<Collider>();
        if (existingCollider != null)
        {
            return;
        }

        CapsuleCollider collider = unit.gameObject.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.radius = Mathf.Max(fallbackHoverColliderRadius, unit.BodyRadius);
        collider.height = Mathf.Max(fallbackHoverColliderHeight, collider.radius * 2f);
        collider.center = new Vector3(0f, collider.height * 0.5f, 0f);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null && target.text != value)
        {
            target.text = value;
        }
    }

    private static void SetComponentActive(Behaviour component, bool value)
    {
        if (component != null)
        {
            component.gameObject.SetActive(value);
        }
    }

    private void RefreshHealthBar(BattleRuntimeUnit unit)
    {
        float currentHealth = unit != null ? Mathf.Max(0f, unit.CurrentHealth) : 0f;
        float maxHealth = unit != null ? ResolveDisplayedMaxHealth(unit) : 0f;
        bool hasHealth = unit != null && maxHealth > 0f;
        if (healthBarRoot != null)
        {
            healthBarRoot.SetActive(hasHealth);
        }

        if (healthBarBlackBackground != null)
        {
            healthBarBlackBackground.enabled = hasHealth;
        }

        if (!hasHealth)
        {
            SetHealthRatio(0f);
            SetText(healthBarText, string.Empty);
            return;
        }

        SetHealthRatio(Mathf.Clamp01(currentHealth / maxHealth));
        SetText(healthBarText, $"{FormatHealth(currentHealth)}/{FormatHealth(maxHealth)}");
    }

    private static float ResolveDisplayedMaxHealth(BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, unit.MaxHealth, unit.CurrentHealth);
    }

    private void SetHealthRatio(float ratio)
    {
        if (healthBarRedFillImage == null)
        {
            return;
        }

        healthBarRedFillImage.enabled = ratio > 0f;
        healthBarRedFillImage.fillAmount = ratio;

        if (!_hasHealthBarRedFillBaseScale)
        {
            CacheHealthBarRedFillBaseScale();
        }

        Transform fillTransform = healthBarRedFillImage.transform;
        Vector3 scale = _healthBarRedFillBaseScale;
        scale.x *= ratio;
        fillTransform.localScale = scale;
    }

    private void ConfigureHealthBarFillImage()
    {
        if (healthBarRedFillImage == null)
        {
            return;
        }

        healthBarRedFillImage.type = Image.Type.Filled;
        healthBarRedFillImage.fillMethod = Image.FillMethod.Horizontal;
        healthBarRedFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        CacheHealthBarRedFillBaseScale();

        RectTransform fillRect = healthBarRedFillImage.rectTransform;
        if (fillRect != null)
        {
            Vector2 pivot = fillRect.pivot;
            pivot.x = 0f;
            fillRect.pivot = pivot;
        }
    }

    private void CacheHealthBarRedFillBaseScale()
    {
        if (healthBarRedFillImage == null)
        {
            _healthBarRedFillBaseScale = Vector3.one;
            _hasHealthBarRedFillBaseScale = false;
            return;
        }

        _healthBarRedFillBaseScale = healthBarRedFillImage.transform.localScale;
        _hasHealthBarRedFillBaseScale = true;
    }

    private void WireDetailButtons()
    {
        if (personalityDetailImage != null)
            personalityDetailImage.onClick.AddListener(ShowPersonalityDetail);

        if (weaponImageIcon != null)
            weaponImageIcon.onClick.AddListener(ShowWeaponDetail);

        if (weaponSkillImageIcon != null)
            weaponSkillImageIcon.onClick.AddListener(ShowWeaponSkillDetail);
    }

    private void UnwireDetailButtons()
    {
        if (personalityDetailImage != null)
            personalityDetailImage.onClick.RemoveListener(ShowPersonalityDetail);

        if (weaponImageIcon != null)
            weaponImageIcon.onClick.RemoveListener(ShowWeaponDetail);

        if (weaponSkillImageIcon != null)
            weaponSkillImageIcon.onClick.RemoveListener(ShowWeaponSkillDetail);
    }

    private void ShowPersonalityDetail() => RefreshSelectedDetail(DetailTab.Personality);

    private void ShowWeaponDetail() => RefreshSelectedDetail(DetailTab.Weapon);

    private void ShowWeaponSkillDetail() => RefreshSelectedDetail(DetailTab.WeaponSkill);

    private void RefreshDetailIcons(BattleRuntimeUnit unit)
    {
        BattleUnitSnapshot snapshot = unit != null ? unit.Snapshot : null;
        SetButtonSprite(weaponImageIcon, snapshot != null ? snapshot.WeaponIconSprite : null);

        WeaponSkillSO skill = ResolveWeaponSkill(snapshot);
        SetButtonSprite(weaponSkillImageIcon, skill != null ? skill.icon : null);
    }

    private void RefreshSelectedDetail(DetailTab tab)
    {
        BattleRuntimeUnit unit = _selectedUnit;
        BattleUnitSnapshot snapshot = unit != null ? unit.Snapshot : null;

        if (snapshot == null)
        {
            SetText(selectedTitleText, string.Empty);
            SetText(selectedDetailText, string.Empty);
            return;
        }

        switch (tab)
        {
            case DetailTab.Personality:
                PersonalitySO personality = snapshot.Personality;
                SetText(selectedTitleText, ResolvePersonalityName(unit));
                SetText(selectedDetailText, ResolvePersonalityDetailText(personality));
                break;
            case DetailTab.Weapon:
                WeaponSO weapon = ResolveWeapon(snapshot);
                SetText(selectedTitleText, ResolveWeaponName(snapshot, weapon));
                SetText(selectedDetailText, BuildWeaponDetailText(weapon));
                break;
            case DetailTab.WeaponSkill:
                WeaponSkillSO skill = ResolveWeaponSkill(snapshot);
                SetText(selectedTitleText, skill != null ? skill.skillName : "스킬 없음");
                SetText(selectedDetailText, skill != null ? skill.description : string.Empty);
                break;
        }
    }

    private WeaponSO ResolveWeapon(BattleUnitSnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.WeaponName))
        {
            return null;
        }

        ContentDatabaseProvider provider = ResolveContentDatabaseProvider();
        IReadOnlyList<WeaponSO> weapons = provider != null ? provider.Weapons : null;
        if (weapons == null)
        {
            return null;
        }

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponSO weapon = weapons[i];
            if (weapon != null && string.Equals(weapon.weaponName, snapshot.WeaponName, StringComparison.Ordinal))
            {
                return weapon;
            }
        }

        return null;
    }

    private WeaponSkillSO ResolveWeaponSkill(BattleUnitSnapshot snapshot)
    {
        if (snapshot == null || snapshot.WeaponSkillId == WeaponSkillId.None)
        {
            return null;
        }

        ContentDatabaseProvider provider = ResolveContentDatabaseProvider();
        IReadOnlyList<WeaponSkillSO> skills = provider != null ? provider.WeaponSkills : null;
        if (skills == null)
        {
            return null;
        }

        for (int i = 0; i < skills.Count; i++)
        {
            WeaponSkillSO skill = skills[i];
            if (skill != null && skill.skillId == snapshot.WeaponSkillId)
            {
                return skill;
            }
        }

        return null;
    }

    private ContentDatabaseProvider ResolveContentDatabaseProvider()
    {
        if (_contentDatabaseProvider == null)
        {
            _contentDatabaseProvider = ContentDatabaseProvider.Instance;
        }

        return _contentDatabaseProvider;
    }

    private static void SetButtonSprite(Button button, Sprite sprite)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image == null)
        {
            image = button.targetGraphic as Image;
        }

        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
        button.interactable = sprite != null;
    }

    private static string ResolveWeaponName(BattleUnitSnapshot snapshot, WeaponSO weapon)
    {
        if (weapon != null && !string.IsNullOrWhiteSpace(weapon.weaponName))
        {
            return weapon.weaponName;
        }

        return snapshot != null && !string.IsNullOrWhiteSpace(snapshot.WeaponName) ? snapshot.WeaponName : "무기 없음";
    }

    private static string BuildWeaponDetailText(WeaponSO weapon)
    {
        if (weapon == null)
        {
            return string.Empty;
        }

        return $"체력 : {FormatSignedStat(weapon.baseHealthBonus)}\n"
            + $"공격력 : {FormatSignedStat(weapon.baseAttackBonus)}\n"
            + $"공격속도 : {FormatSignedStat(weapon.baseAttackSpeedBonus)}\n"
            + $"이동속도 : {FormatSignedStat(weapon.baseMoveSpeedBonus)}\n"
            + $"사거리 : {FormatSignedStat(weapon.baseAttackRangeBonus)}";
    }

    private static string ResolvePersonalityDetailText(PersonalitySO personality)
    {
        if (personality == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(personality.dialogPersonalityDescription))
        {
            return personality.dialogPersonalityDescription;
        }

        if (!string.IsNullOrWhiteSpace(personality.detailText))
        {
            return personality.detailText;
        }

        return string.IsNullOrWhiteSpace(personality.description) ? string.Empty : personality.description;
    }

    private static string FormatStat(float value)
    {
        return value.ToString("0.#");
    }

    private static string FormatHealth(float value)
    {
        return Mathf.RoundToInt(value).ToString();
    }

    private static string FormatSignedStat(float value)
    {
        if (Mathf.Approximately(value, 0f))
        {
            return "0";
        }

        return value > 0f ? $"+{value:0.#}" : value.ToString("0.#");
    }

    private static string BuildUnitNameText(BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return string.Empty;
        }

        const string nameColor = "#FFFFFF";

        return $"<color={nameColor}>{unit.DisplayName}</color>";
    }

    private static string ResolvePersonalityName(BattleRuntimeUnit unit)
    {
        BattleUnitSnapshot snapshot = unit != null ? unit.Snapshot : null;
        return
            snapshot != null
            && snapshot.Personality != null
            && !string.IsNullOrWhiteSpace(snapshot.Personality.personalityName)
            ? snapshot.Personality.personalityName
            : "성격 없음";
    }
}
