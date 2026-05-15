// BattleProjectileSystem.cs

using System;
using System.Collections.Generic;
using UnityEngine;

public class BattleProjectileState
{
    public BattleDamageRequest DamageRequest { get; }
    public Vector3 CurrentPosition { get; private set; }
    // 🌟 [수정] 방향이 실시간으로 바뀔 수 있도록 private set 추가
    public Vector3 Direction { get; private set; }
    public float Speed { get; }
    public float Delay { get; set; }

    // 🌟 [추가] 유도탄 여부
    public bool IsHoming { get; }

    public GameObject VisualObject { get; private set; }
    private GameObject _prefabToSpawn;

    public Action<BattleUnitCombatState, Vector3, IBattleEffectSink> OnHitCallback { get; }

    public BattleProjectileState(
        BattleDamageRequest request,
        Vector3 startPos,
        Vector3 direction,
        float speed,
        GameObject prefab,
        float delay,
        Action<BattleUnitCombatState, Vector3, IBattleEffectSink> onHitCallback = null,
        bool isHoming = false
    )
    {
        DamageRequest = request;
        CurrentPosition = startPos;
        Direction = direction.normalized;
        Speed = speed;
        _prefabToSpawn = prefab;
        Delay = delay;
        OnHitCallback = onHitCallback;
        IsHoming = isHoming;
    }

    public void SpawnVisual(Transform root)
    {
        if (_prefabToSpawn != null && VisualObject == null)
            VisualObject = GameObject.Instantiate(
                _prefabToSpawn,
                CurrentPosition,
                Quaternion.LookRotation(Direction),
                root
            );
    }

    public void TickMove(float deltaTime)
    {
        // 🌟 [핵심 추가] 유도(Homing) 로직
        // 타겟이 살아있고 유도 옵션이 켜져있다면 방향을 갱신합니다.
        if (IsHoming && DamageRequest.Target != null && !DamageRequest.Target.IsCombatDisabled)
        {
            Vector3 toTarget = DamageRequest.Target.Position - CurrentPosition;
            toTarget.y = 0f; // 평면 전투 기준 높이 무시 (원하시면 제거해도 됩니다)

            if (toTarget.sqrMagnitude > 0.001f)
            {
                Direction = toTarget.normalized; // 타겟을 향해 방향 강제 변경
            }
        }

        CurrentPosition += Direction * Speed * deltaTime;

        if (VisualObject != null)
        {
            VisualObject.transform.position = CurrentPosition;
            // 🌟 [추가] 날아가는 방향으로 투사체의 머리(화살촉)를 실시간으로 돌려줍니다.
            if (Direction.sqrMagnitude > 0.001f)
            {
                VisualObject.transform.rotation = Quaternion.LookRotation(Direction);
            }
        }
    }

    public void Destroy()
    {
        if (VisualObject != null)
            GameObject.Destroy(VisualObject);
    }
}

public class BattleProjectileSystem
{
    private readonly List<BattleProjectileState> _projectiles = new List<BattleProjectileState>();
    private IBattleEffectSink _effectSink;
    private Transform _projectileRoot;

    private IReadOnlyList<BattleRuntimeUnit> _units;

    public void Configure(IBattleEffectSink effectSink, Transform root, IReadOnlyList<BattleRuntimeUnit> units)
    {
        _effectSink = effectSink;
        _projectileRoot = root;
        _units = units;
    }

    public void Clear()
    {
        foreach (var p in _projectiles)
            p.Destroy();
        _projectiles.Clear();
    }

    // 🌟 [수정] Launch 인자에 isHoming 추가
    public void Launch(
        BattleDamageRequest request,
        Vector3 startPos,
        Vector3 direction,
        float speed,
        GameObject prefab,
        float delay,
        Action<BattleUnitCombatState, Vector3, IBattleEffectSink> onHitCallback = null,
        bool isHoming = false // 🌟 [추가]
    )
    {
        var proj = new BattleProjectileState(request, startPos, direction, speed, prefab, delay, onHitCallback, isHoming);
        if (delay <= 0f)
            proj.SpawnVisual(_projectileRoot);
        _projectiles.Add(proj);
    }

    public void Tick(float deltaTime)
    {
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var proj = _projectiles[i];

            if (proj.Delay > 0f)
            {
                proj.Delay -= deltaTime;
                if (proj.Delay <= 0f)
                    proj.SpawnVisual(_projectileRoot);
                else
                    continue;
            }

            proj.TickMove(deltaTime);

            if (proj.CurrentPosition.magnitude > 200f)
            {
                proj.Destroy();
                _projectiles.RemoveAt(i);
                continue;
            }

            bool hitOccurred = false;

            if (proj.IsHoming)
            {
                BattleUnitCombatState target = proj.DamageRequest.Target;
                if (target != null && !target.IsCombatDisabled)
                {
                    float hitRadius = target.BodyRadius + 1.0f;
                    if (Vector3.Distance(proj.CurrentPosition, target.Position) <= hitRadius)
                    {
                        ExecuteHit(proj, target, i);
                        hitOccurred = true;
                    }
                }
                else
                {
                    // 타겟이 사망하면 유도탄은 의미를 잃고 파괴됨
                    proj.Destroy();
                    _projectiles.RemoveAt(i);
                    continue;
                }
            }
            else
            {
                // [논타겟팅 OFF] 날아가는 궤적 중간에 적군이 서 있으면 '대신' 맞습니다! (바디블록 가능)
                if (_units != null)
                {
                    for (int j = 0; j < _units.Count; j++)
                    {
                        BattleRuntimeUnit unit = _units[j];
                        if (unit == null || unit.IsCombatDisabled) continue;

                        // 시전자와 같은 팀(아군)은 투사체가 통과하도록 무시합니다.
                        if (proj.DamageRequest.Source != null)
                        {
                            if (proj.DamageRequest.Source.TeamId.Value == unit.TeamId.Value)
                                continue;
                        }

                        float hitRadius = unit.BodyRadius + 1.0f;
                        if (Vector3.Distance(proj.CurrentPosition, unit.Position) <= hitRadius)
                        {
                            //충돌
                            ExecuteHit(proj, unit.State, i);
                            hitOccurred = true;
                            break; // 1개의 투사체는 1명만 맞추므로 루프 즉시 종료
                        }
                    }
                }
            }

            if (hitOccurred) continue;
        }
    }

    private void ExecuteHit(BattleProjectileState proj, BattleUnitCombatState hitTarget, int index)
    {
        // 원래 계획된 타겟 대신, 실제로 투사체에 맞은 녀석(hitTarget)으로 데미지 대상을 변경합니다.
        BattleDamageRequest finalRequest = proj.DamageRequest;
        finalRequest.Target = hitTarget;

        if (proj.OnHitCallback != null)
        {
            proj.OnHitCallback.Invoke(hitTarget, proj.CurrentPosition, _effectSink);
        }
        else
        {
            _effectSink?.DealDamage(finalRequest);
        }

        proj.Destroy();
        _projectiles.RemoveAt(index);
    }
}
