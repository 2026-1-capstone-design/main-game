using UnityEngine;

public readonly struct BattleAnchor
{
    public readonly BattleAnchorKind Kind;
    public readonly int SlotIndex;
    public readonly BattleUnitCombatState Unit;
    public readonly Vector3 Position;
    public readonly bool HasUnit;

    public BattleAnchor(
        BattleAnchorKind kind,
        int slotIndex,
        BattleUnitCombatState unit,
        Vector3 position,
        bool hasUnit
    )
    {
        Kind = kind;
        SlotIndex = slotIndex;
        Unit = unit;
        Position = position;
        HasUnit = hasUnit;
    }
}

// TrainingUnitCombatOverlay는 TrainingScene에서 ML policy가 고른 anchor와 공격 사거리를
// 전투 모델 위에 직접 표시하는 훈련 전용 디버그 비주얼이다.
[DisallowMultipleComponent]
public sealed class TrainingUnitCombatOverlay : MonoBehaviour
{
    private const int CircleSegments = 72;
    private const float GroundYOffset = 0.08f;
    private const float ArrowYOffset = 0.18f;
    private const float ArrowHeadLength = 2.2f;
    private const float ArrowHeadAngle = 22f;

    [SerializeField]
    private Color enemyAnchorColor = new Color(1f, 0.12f, 0.08f, 0.9f);

    [SerializeField]
    private Color allyAnchorColor = new Color(0.1f, 0.95f, 0.25f, 0.9f);

    [SerializeField]
    private Color attackRangeColor = new Color(1f, 0.92f, 0.16f, 0.55f);

    [SerializeField]
    private float arrowWidth = 0.15f;

    [SerializeField]
    private float rangeWidth = 0.1f;

    [SerializeField]
    private Vector3 labelWorldOffset = new Vector3(0f, 2.35f, 0f);

    [SerializeField]
    private Vector2 hpBarSize = new Vector2(64f, 6f);

    [SerializeField]
    private Vector2 modeLabelSize = new Vector2(96f, 22f);

    private BattleRuntimeUnit _unit;
    private LineRenderer _arrowShaft;
    private LineRenderer _arrowHeadLeft;
    private LineRenderer _arrowHeadRight;
    private LineRenderer _attackRange;
    private Material _lineMaterial;
    private GUIStyle _modeLabelStyle;

    private void Awake()
    {
        if (!Application.isEditor)
        {
            enabled = false;
            return;
        }

        _unit = GetComponent<BattleRuntimeUnit>();
        _lineMaterial = CreateLineMaterial();
        _arrowShaft = CreateLine("AnchorArrow", 2, false, arrowWidth);
        _arrowHeadLeft = CreateLine("AnchorArrowHeadLeft", 2, false, arrowWidth);
        _arrowHeadRight = CreateLine("AnchorArrowHeadRight", 2, false, arrowWidth);
        _attackRange = CreateLine("AttackRange", CircleSegments + 1, true, rangeWidth);
    }

    private void LateUpdate()
    {
        if (_unit == null || _unit.State == null || _unit.IsCombatDisabled)
        {
            SetArrowVisible(false);
            SetRangeVisible(false);
            return;
        }

        RefreshAttackRange();
        RefreshAnchorArrow();
    }

    private void OnDestroy()
    {
        if (_lineMaterial != null)
        {
            Destroy(_lineMaterial);
        }
    }

    private void OnGUI()
    {
        if (_unit == null || _unit.State == null || _unit.IsCombatDisabled)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 screenPoint = camera.WorldToScreenPoint(_unit.Position + labelWorldOffset);
        if (screenPoint.z <= 0f)
        {
            return;
        }

        EnsureGuiStyle();

        float screenX = screenPoint.x;
        float screenY = Screen.height - screenPoint.y;
        DrawHpBar(screenX, screenY - 12f);
        DrawStrategy(screenX, screenY + 2f);
    }

    private void RefreshAttackRange()
    {
        float radius = Mathf.Max(0f, _unit.BodyRadius + _unit.AttackRange);
        if (radius <= 0f)
        {
            SetRangeVisible(false);
            return;
        }

        Vector3 center = _unit.Position + Vector3.up * GroundYOffset;
        for (int i = 0; i <= CircleSegments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / CircleSegments;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            _attackRange.SetPosition(i, point);
        }

        SetLineColor(_attackRange, attackRangeColor);
        SetRangeVisible(true);
    }

    private void RefreshAnchorArrow()
    {
        BattleAnchor anchor = _unit.State.PlannedAnchor;
        if (!anchor.HasUnit || anchor.Unit == null || anchor.Unit.IsCombatDisabled)
        {
            SetArrowVisible(false);
            return;
        }

        switch (anchor.Kind)
        {
            case BattleAnchorKind.Ally:
            case BattleAnchorKind.Enemy:
                break;
            default:
                SetArrowVisible(false);
                return;
        }

        Color color = _unit.IsEnemy ? enemyAnchorColor : allyAnchorColor;

        Vector3 start = _unit.Position + Vector3.up * ArrowYOffset;
        Vector3 end = anchor.Unit.Position + Vector3.up * ArrowYOffset;
        Vector3 toAnchor = end - start;
        toAnchor.y = 0f;
        if (toAnchor.sqrMagnitude <= 0.0001f)
        {
            SetArrowVisible(false);
            return;
        }

        Vector3 direction = toAnchor.normalized;
        Vector3 left = Quaternion.AngleAxis(180f - ArrowHeadAngle, Vector3.up) * direction;
        Vector3 right = Quaternion.AngleAxis(180f + ArrowHeadAngle, Vector3.up) * direction;
        float headLength = Mathf.Min(ArrowHeadLength, toAnchor.magnitude * 0.14f);

        SetLineColor(_arrowShaft, color);
        SetLineColor(_arrowHeadLeft, color);
        SetLineColor(_arrowHeadRight, color);
        _arrowShaft.SetPosition(0, start);
        _arrowShaft.SetPosition(1, end);
        _arrowHeadLeft.SetPosition(0, end);
        _arrowHeadLeft.SetPosition(1, end + left * headLength);
        _arrowHeadRight.SetPosition(0, end);
        _arrowHeadRight.SetPosition(1, end + right * headLength);

        SetArrowVisible(true);
    }

    private LineRenderer CreateLine(string lineName, int positionCount, bool loop, float width)
    {
        var child = new GameObject(lineName);
        child.transform.SetParent(transform, false);
        LineRenderer line = child.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = positionCount;
        line.loop = loop;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.material = _lineMaterial;
        line.enabled = false;
        return line;
    }

    private static Material CreateLineMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        return new Material(shader);
    }

    private void DrawHpBar(float centerX, float topY)
    {
        float ratio = _unit.MaxHealth > 0f ? Mathf.Clamp01(_unit.CurrentHealth / _unit.MaxHealth) : 0f;
        Rect backgroundRect = new Rect(centerX - hpBarSize.x * 0.5f, topY, hpBarSize.x, hpBarSize.y);
        Rect fillRect = new Rect(
            backgroundRect.x + 1f,
            backgroundRect.y + 1f,
            (hpBarSize.x - 2f) * ratio,
            hpBarSize.y - 2f
        );
        Color fillColor = _unit.IsEnemy ? enemyAnchorColor : allyAnchorColor;

        DrawRect(backgroundRect, new Color(0f, 0f, 0f, 0.72f));
        DrawRect(fillRect, fillColor);
    }

    private void DrawStrategy(float centerX, float topY)
    {
        Rect labelRect = new Rect(centerX - modeLabelSize.x * 0.5f, topY, modeLabelSize.x, modeLabelSize.y);
        GUI.Label(labelRect, _unit.State.AgentStrategy.ToString(), _modeLabelStyle);
    }

    private void EnsureGuiStyle()
    {
        if (_modeLabelStyle != null)
        {
            return;
        }

        _modeLabelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private static void SetLineColor(LineRenderer line, Color color)
    {
        if (line == null)
        {
            return;
        }

        line.startColor = color;
        line.endColor = color;
    }

    private void SetArrowVisible(bool visible)
    {
        SetLineVisible(_arrowShaft, visible);
        SetLineVisible(_arrowHeadLeft, visible);
        SetLineVisible(_arrowHeadRight, visible);
    }

    private void SetRangeVisible(bool visible)
    {
        SetLineVisible(_attackRange, visible);
    }

    private static void SetLineVisible(LineRenderer line, bool visible)
    {
        if (line != null && line.enabled != visible)
        {
            line.enabled = visible;
        }
    }
}
