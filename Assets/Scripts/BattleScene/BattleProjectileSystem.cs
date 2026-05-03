using System;
using System.Collections.Generic;
using UnityEngine;

public class BattleProjectileState
{
    public BattleDamageRequest DamageRequest { get; }
    public Vector3 CurrentPosition { get; private set; }
    public Vector3 Direction { get; }
    public float Speed { get; }
    public float Delay { get; set; }

    public GameObject VisualObject { get; private set; }
    private GameObject _prefabToSpawn;

    public Action<BattleUnitCombatState, Vector3, IBattleEffectSink> OnHitCallback { get; }

    public BattleProjectileState(BattleDamageRequest request, Vector3 startPos, Vector3 direction, float speed, GameObject prefab, float delay, Action<BattleUnitCombatState, Vector3, IBattleEffectSink> onHitCallback = null)
    {
        DamageRequest = request;
        CurrentPosition = startPos;
        Direction = direction.normalized;
        Speed = speed;
        _prefabToSpawn = prefab;
        Delay = delay;
        OnHitCallback = onHitCallback;
    }

    public void SpawnVisual(Transform root)
    {
        if (_prefabToSpawn != null && VisualObject == null)
            VisualObject = GameObject.Instantiate(_prefabToSpawn, CurrentPosition, Quaternion.LookRotation(Direction), root);
    }

    public void TickMove(float deltaTime)
    {
        CurrentPosition += Direction * Speed * deltaTime;
        if (VisualObject != null)
            VisualObject.transform.position = CurrentPosition;
    }

    public void Destroy()
    {
        if (VisualObject != null) GameObject.Destroy(VisualObject);
    }
}

public class BattleProjectileSystem
{
    private readonly List<BattleProjectileState> _projectiles = new List<BattleProjectileState>();
    private IBattleEffectSink _effectSink;
    private Transform _projectileRoot;

    public void Configure(IBattleEffectSink effectSink, Transform root)
    {
        _effectSink = effectSink;
        _projectileRoot = root;
    }

    public void Clear()
    {
        foreach (var p in _projectiles) p.Destroy();
        _projectiles.Clear();
    }

    public void Launch(BattleDamageRequest request, Vector3 startPos, Vector3 direction, float speed, GameObject prefab, float delay, Action<BattleUnitCombatState, Vector3, IBattleEffectSink> onHitCallback = null)
    {
        var proj = new BattleProjectileState(request, startPos, direction, speed, prefab, delay, onHitCallback);
        if (delay <= 0f) proj.SpawnVisual(_projectileRoot);
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
                if (proj.Delay <= 0f) proj.SpawnVisual(_projectileRoot);
                else continue;
            }

            proj.TickMove(deltaTime);

            if (proj.CurrentPosition.magnitude > 200f)
            {
                proj.Destroy();
                _projectiles.RemoveAt(i);
                continue;
            }

            BattleUnitCombatState target = proj.DamageRequest.Target;
            if (target != null && !target.IsCombatDisabled)
            {
                float hitRadius = target.BodyRadius + 1.0f;
                if (Vector3.Distance(proj.CurrentPosition, target.Position) <= hitRadius)
                {
                    if (proj.OnHitCallback != null)
                    {
                        proj.OnHitCallback.Invoke(target, proj.CurrentPosition, _effectSink);
                    }
                    else
                    {
                        _effectSink?.DealDamage(proj.DamageRequest);
                    }
                    proj.Destroy();
                    _projectiles.RemoveAt(i);
                }
            }
            else
            {
                proj.Destroy();
                _projectiles.RemoveAt(i);
            }
        }
    }
}
