using UnityEngine;
using UnityEngine.UI;

// 배치 화면의 공격 사거리를 내부가 투명하고 두께가 일정한 UI 원형 테두리로 표시한다.
[AddComponentMenu("UI/Deployment Attack Range Ring")]
public sealed class DeploymentAttackRangeRing : MaskableGraphic
{
    private const int MinSegments = 12;
    private const int MaxSegments = 256;

    [SerializeField]
    [Min(0.1f)]
    private float ringThickness = 2f;

    [SerializeField]
    [Range(MinSegments, MaxSegments)]
    private int segments = 96;

    public float RingThickness
    {
        get => ringThickness;
        set
        {
            float nextThickness = Mathf.Max(0.1f, value);
            if (Mathf.Approximately(ringThickness, nextThickness))
            {
                return;
            }

            ringThickness = nextThickness;
            SetVerticesDirty();
        }
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        float outerRadiusX = rect.width * 0.5f;
        float outerRadiusY = rect.height * 0.5f;
        if (outerRadiusX <= 0f || outerRadiusY <= 0f)
        {
            return;
        }

        float thickness = Mathf.Min(ringThickness, outerRadiusX, outerRadiusY);
        float innerRadiusX = Mathf.Max(0f, outerRadiusX - thickness);
        float innerRadiusY = Mathf.Max(0f, outerRadiusY - thickness);
        int segmentCount = Mathf.Clamp(segments, MinSegments, MaxSegments);
        Vector2 center = rect.center;
        Color32 vertexColor = color;
        float angleStep = Mathf.PI * 2f / segmentCount;
        Vector2 outerRadius = new Vector2(outerRadiusX, outerRadiusY);
        Vector2 innerRadius = new Vector2(innerRadiusX, innerRadiusY);
        Vector2 currentDirection = Vector2.right;

        for (int i = 0; i < segmentCount; i++)
        {
            float nextAngle = angleStep * (i + 1);
            Vector2 nextDirection = new Vector2(Mathf.Cos(nextAngle), Mathf.Sin(nextAngle));

            int vertexStart = vertexHelper.currentVertCount;
            vertexHelper.AddVert(center + Vector2.Scale(currentDirection, outerRadius), vertexColor, Vector2.zero);
            vertexHelper.AddVert(center + Vector2.Scale(nextDirection, outerRadius), vertexColor, Vector2.zero);
            vertexHelper.AddVert(center + Vector2.Scale(nextDirection, innerRadius), vertexColor, Vector2.zero);
            vertexHelper.AddVert(center + Vector2.Scale(currentDirection, innerRadius), vertexColor, Vector2.zero);

            vertexHelper.AddTriangle(vertexStart, vertexStart + 1, vertexStart + 2);
            vertexHelper.AddTriangle(vertexStart, vertexStart + 2, vertexStart + 3);

            currentDirection = nextDirection;
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        ringThickness = Mathf.Max(0.1f, ringThickness);
        segments = Mathf.Clamp(segments, MinSegments, MaxSegments);
        raycastTarget = false;
        SetVerticesDirty();
    }
#endif
}
