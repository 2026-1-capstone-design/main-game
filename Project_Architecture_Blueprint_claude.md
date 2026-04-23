# Project Architecture Blueprint

> Generated: 2026-04-23  
> Project: `main-game` — Sogang University Capstone 2026  
> Game: Turn-based Gladiator Management & Battle Simulation (Unity 3D / C#)

---

## Table of Contents

1. [Architectural Overview](#1-architectural-overview)
2. [Architecture Visualization](#2-architecture-visualization)
3. [Core Architectural Components](#3-core-architectural-components)
4. [Architectural Layers and Dependencies](#4-architectural-layers-and-dependencies)
5. [Data Architecture](#5-data-architecture)
6. [Cross-Cutting Concerns](#6-cross-cutting-concerns)
7. [Service Communication Patterns](#7-service-communication-patterns)
8. [Technology Stack](#8-technology-stack)
9. [Implementation Patterns](#9-implementation-patterns)
10. [Extension and Evolution Patterns](#10-extension-and-evolution-patterns)
11. [Architectural Pattern Examples](#11-architectural-pattern-examples)
12. [Architectural Decision Records](#12-architectural-decision-records)
13. [Blueprint for New Development](#13-blueprint-for-new-development)

---

## 1. Architectural Overview

### 1.1 Pattern

The project uses a **layered service/manager architecture with data-driven content**, implemented in Unity 3D using C#. The overarching pattern is not Clean Architecture or MVVM in its strict forms, but a **pragmatic game-oriented hybrid**:

```
Content Layer    → ScriptableObjects (templates, tuning)
Service Layer    → Singleton Managers (persistent cross-scene state)
Domain Layer     → Pure data models + logic classes (no MonoBehaviour)
Presentation Layer → MonoBehaviour wrappers, UI managers, visual units
```

Two primary scenes exist:
- **Main Scene** — gladiator management (recruit, equip, sell, research, day-end)
- **Battle Scene** — real-time unit simulation (AI decision loop, skills, visual rendering)

Cross-scene state is transferred via **payload objects** held by a persistent service.

### 1.2 Guiding Principles

1. **Data-Driven Content** — All game configuration lives in `ScriptableObject` assets. Adding a new weapon, trait, or gladiator class requires no code changes.
2. **Separation of State from Presentation** — `BattleUnitCombatState` (pure data, no `MonoBehaviour`) is wrapped by `BattleRuntimeUnit` (visual). Logic can be tested without a running scene.
3. **Immutable Cross-Scene Snapshots** — `BattleUnitSnapshot` freezes gladiator stats at the moment of entering battle. Post-battle, only aggregate results are written back.
4. **Registry-Based Extensibility** — Skills and action planners are registered by ID, allowing additions without modifying core loops.
5. **Factory Generation** — All runtime content instances (gladiators, weapons) are created by factories, never directly by consumers.
6. **Observer-Driven UI** — UI managers subscribe to events on service managers; they never poll state.

### 1.3 Architectural Boundaries

| Boundary | Enforcement Mechanism |
|---|---|
| Content vs. Runtime | ScriptableObjects are read-only templates; `OwnedGladiatorData` / `OwnedWeaponData` are the mutable instances |
| Main Scene vs. Battle Scene | `BattleStartPayload` goes in; `PendingBattleReward` comes back via `SessionManager` |
| State vs. Presentation | `BattleUnitCombatState` has no scene references; `BattleRuntimeUnit` owns all visual logic |
| AI Decision vs. Execution | `IBattleActionPlanner` produces a plan; `BattleSimulationManager` executes it |
| Skill Logic vs. Application | `IBattleSkill` defines effect; `ISkillEffectApplier` applies it to state |

---

## 2. Architecture Visualization

### 2.1 System Context (C4 Level 1)

```
┌─────────────────────────────────────────────────────────────┐
│                        Player                               │
│                 (manages gladiators, battles)               │
└───────────────────────┬─────────────────────────────────────┘
                        │
            ┌───────────▼───────────┐
            │     main-game         │
            │  (Unity 3D game)      │
            └───────────┬───────────┘
                        │
          ┌─────────────┼──────────────┐
          │             │              │
   ┌──────▼──────┐ ┌───▼───┐  ┌───────▼──────┐
   │ ContentDB   │ │  LLM  │  │  RandomManager│
   │ (SO assets) │ │ API   │  │ (seeded RNG)  │
   └─────────────┘ └───────┘  └──────────────┘
```

### 2.2 Container Diagram (C4 Level 2)

```
┌──────────────────────────────────── Unity Application ────────────────────────────────────┐
│                                                                                             │
│  ┌───────────────────────────────── Persistent Services (DDOL) ─────────────────────────┐ │
│  │  AppFlowController · SessionManager · SceneLoader · AudioManager                     │ │
│  │  RandomManager · ContentDatabaseProvider · BattleSessionManager                      │ │
│  └───────────────────────────────────────────────────────────────────────────────────────┘ │
│                                                                                             │
│  ┌──────────────── Main Scene ────────────────┐  ┌──────── Battle Scene ─────────────────┐ │
│  │                                            │  │                                       │ │
│  │  MainFlowManager (orchestrator)            │  │  BattleSceneFlowManager               │ │
│  │  ├─ GladiatorManager                       │  │  BattleSimulationManager (tick loop)  │ │
│  │  ├─ InventoryManager                       │  │  ├─ BattleFieldView                   │ │
│  │  ├─ MarketManager                          │  │  ├─ BattleSkillRegistry               │ │
│  │  │   ├─ RecruitFactory                     │  │  ├─ Action Planners (×7)              │ │
│  │  │   └─ EquipmentFactory                   │  │  ├─ BattleParameterComputer           │ │
│  │  ├─ ResourceManager                        │  │  └─ BattleActionScorer                │ │
│  │  ├─ ResearchManager                        │  │  BattleRuntimeUnit (×N, visual)       │ │
│  │  ├─ BattleManager                          │  │  BattleUnitCombatState (×N, pure data)│ │
│  │  └─ UI Managers (×6)                       │  │  BattleOrdersManager (LLM)            │ │
│  │                                            │  │  UI Managers (×2)                     │ │
│  └────────────────────────────────────────────┘  └───────────────────────────────────────┘ │
│                                                                                             │
│  ┌──────────────── Content Database (ScriptableObjects) ────────────────────────────────┐  │
│  │  ContentDatabaseSO (master)                                                           │  │
│  │  ├─ GladiatorClassSO[] · WeaponSO[] · WeaponSkillSO[] · TraitSO[]                   │  │
│  │  ├─ SynergySO[] · PerkSO[] · PersonalitySO[]                                         │  │
│  │  └─ BalanceSO · BattleAITuningSO                                                     │  │
│  └───────────────────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 2.3 Battle Simulation Tick (Data Flow)

```
FixedUpdate() tick
        │
        ▼
BattleParameterComputer
  Compute 9 raw params (0–1 each)
        │
        ▼
Apply currentActionParameterPercents (bias toward current action)
        │
        ▼
BattleActionScorer
  For each ActionType:
    score = baseBias + Σ(modifiedParam_i × weight_i)
    score × weaponTypePercent × personalityPercent
        │
        ▼
For each alive unit:
  IBattleActionPlanner.IsUsable(unit, field)
  Pick max-score usable planner
  IBattleActionPlanner.Build(unit, field, state) → BattleActionExecutionPlan
        │
        ▼
BattleSimulationManager executes plan:
  • Movement (transform.position)
  • Attack (deal damage, trigger skill)
  • ISkillEffectApplier.Apply(skill, caster, target, fieldView)
        │
        ▼
BattleUnitCombatState mutated
  → OnHealthChanged / OnDied events fire
  → UI updates via event handlers
        │
        ▼
TryFinishBattle() check
  → SessionManager.SetPendingBattleReward()
  → SceneLoader.LoadMainScene()
```

---

## 3. Core Architectural Components

### 3.1 Persistent Service Layer (`BootScripts/`)

| Class | Responsibility | Lifetime |
|---|---|---|
| `AppFlowController` | Master boot sequence, wires all singleton dependencies | App lifetime |
| `SessionManager` | Day counter, battle usage flag, pending reward, gladiator naming | App lifetime |
| `ContentDatabaseProvider` | Loads and exposes `ContentDatabaseSO` asset | App lifetime |
| `BattleSessionManager` | Carries `BattleStartPayload` between scenes | App lifetime |
| `RandomManager` | Seeded, reproducible RNG for all random values | App lifetime |
| `SceneLoader` | Wraps `SceneManager` behind a single API | App lifetime |
| `AudioManager` | SFX/music control | App lifetime |

All inherit `SingletonBehaviour<T>` and call `DontDestroyOnLoad`. `AppFlowController.ResolveDependencies()` injects inter-service references at startup.

### 3.2 Main Scene Domain (`MainScripts/`)

| Class | Responsibility |
|---|---|
| `MainFlowManager` | Scene orchestrator; initializes all sub-managers; handles end-of-day |
| `GladiatorManager` | Owns `OwnedGladiatorData[]`; equip/unequip weapons; XP/levelling |
| `InventoryManager` | Owns `OwnedWeaponData[]` |
| `MarketManager` | Daily offer generation; buy/sell transactions; uses `RecruitFactory`, `EquipmentFactory` |
| `ResourceManager` | Gold; `TrySpendGold`, `AddGold`; fires `GoldChanged` |
| `BattleManager` | Daily encounter list; selects and packages `BattleStartPayload` |
| `ResearchManager` | Upgrade system (details TBD) |
| `RecruitFactory` | Generates `OwnedGladiatorData` from templates + RNG |
| `EquipmentFactory` | Generates `OwnedWeaponData` from templates + RNG |

### 3.3 Battle Scene Domain (`BattleScene/`)

| Class | Responsibility |
|---|---|
| `BattleSceneFlowManager` | Initializes battle: instantiates `BattleRuntimeUnit`s, creates `BattleUnitCombatState`s |
| `BattleSimulationManager` | Main tick loop; owns planner registry; executes plans |
| `BattleFieldView` | Read-only spatial queries (unit positions, distances, team rosters) |
| `BattleParameterComputer` | Computes the 9 raw battle parameters per unit |
| `BattleActionScorer` | Scores all 7 action types using parameters and tuning weights |
| `BattleSkillRegistry` | Maps `WeaponSkillId` → `IBattleSkill`; fallback to `DefaultHealSkill` |
| `BattleRuntimeUnit` | `MonoBehaviour` visual wrapper around `BattleUnitCombatState` |
| `BattleUnitCombatState` | Pure-data unit state (HP, buffs, timers, AI results); no Unity dependency |
| `BattleOrdersManager` | HTTP client for LLM-based strategic order integration |

### 3.4 Content Layer (`SOScripts/` + `Content/`)

`ContentDatabaseSO` is the single root asset containing typed lists of every SO type. It is loaded once by `ContentDatabaseProvider` and never mutated at runtime.

---

## 4. Architectural Layers and Dependencies

### 4.1 Layer Map

```
Layer 4 — Presentation
  MonoBehaviour UI managers, BattleRuntimeUnit, camera, animation
  ↓ reads/subscribes to
Layer 3 — Application/Manager
  MainFlowManager, BattleSimulationManager, all domain managers
  ↓ uses
Layer 2 — Domain Model
  OwnedGladiatorData, BattleUnitCombatState, BattleActionExecutionPlan
  ↓ parameterized by
Layer 1 — Content / Configuration
  ScriptableObjects (GladiatorClassSO, BalanceSO, BattleAITuningSO, …)
```

**Dependency rule:** Upper layers may reference lower layers; lower layers must not reference upper layers. `BattleUnitCombatState` (Layer 2) has zero knowledge of `BattleRuntimeUnit` (Layer 4).

### 4.2 Cross-Layer Communication

| Direction | Mechanism |
|---|---|
| Down (trigger) | Direct method calls |
| Up (notification) | C# `event Action<T>` / `UnityEvent` |
| Cross-scene | `BattleStartPayload` (write) → `SessionManager.PendingBattleReward` (read) |

### 4.3 Known Coupling Points to Watch

- `BattleSimulationManager` is large — it owns tick, execution, and skill dispatch. The `ISkillEffectApplier` interface exists to allow extraction.
- `BattleRuntimeUnit` holds both visual components and references to planned targets. Target-tracking is pure-logic that could move to `BattleUnitCombatState`.

---

## 5. Data Architecture

### 5.1 Domain Model

```
OwnedGladiatorData
├─ Identity:   RuntimeId (Guid), DisplayName
├─ Progression: Level, Exp, Loyalty, Upkeep
├─ Templates: GladiatorClassSO, TraitSO, PersonalitySO, PerkSO (nullable)
├─ Equipment: OwnedWeaponData (nullable)
└─ Cached stats (recomputed on equip/level via RefreshDerivedStats()):
     CachedMaxHealth, CachedAttack, CachedAttackSpeed,
     CachedMoveSpeed, CachedAttackRange,
     FinalHealthVariancePercent, FinalAttackVariancePercent

OwnedWeaponData
├─ Identity:   RuntimeId (Guid), DisplayName
├─ Progression: Level
├─ Templates: WeaponSO, WeaponSkillSO
└─ Cached bonuses: CachedAttackBonus, CachedHealthBonus, …

BattleUnitSnapshot  (immutable; created from OwnedGladiatorData)
├─ SourceRuntimeId  (trace back to owner)
├─ All stat fields (copied; not references)
├─ WeaponType, WeaponSkillId
└─ Weapon visual prefabs

BattleUnitCombatState  (mutable runtime battle state)
├─ Identity: UnitNumber (int), IsEnemy (bool)
├─ Health: MaxHealth, CurrentHealth, IsCombatDisabled
├─ Stats: Attack, AttackSpeed, MoveSpeed, AttackRange
├─ Buff: ActiveBuff (type, level, remainingDuration)
├─ AI state: CurrentActionType, CurrentActionTimer
├─ Computed this tick: parameters, scores, planned action
└─ Events: OnHealthChanged(int,int), OnDied()
```

### 5.2 Stat Derivation Formula

```
FinalMaxHealth
  = class.baseHealth
  + class.healthGrowthPerLevel × (Level - 1)
  + weapon.CachedHealthBonus
  + trait.healthBonus (if any)
  × variance(±15% from RNG seeded to gladiator RuntimeId)
```

Same additive-then-multiply pattern applies to Attack, AttackSpeed, MoveSpeed, AttackRange.

### 5.3 Entity Relationships

```
ContentDatabaseSO ──has──► GladiatorClassSO (many)
                  ──has──► WeaponSO (many)
                  ──has──► WeaponSkillSO (many)
                  ──has──► TraitSO (many)
                  ──has──► PersonalitySO (many)
                  ──has──► PerkSO (many)
                  ──has──► BalanceSO (one)

OwnedGladiatorData ──references──► GladiatorClassSO
                   ──references──► TraitSO
                   ──references──► PersonalitySO
                   ──references──► PerkSO (nullable)
                   ──has──► OwnedWeaponData (nullable)

OwnedWeaponData ──references──► WeaponSO
                ──references──► WeaponSkillSO
```

### 5.4 Data Access Patterns

- **Read-only content access**: `ContentDatabaseProvider.Database.GladiatorClasses[i]` — direct indexed access, no abstraction layer.
- **Owned data mutation**: Always through a manager method (e.g., `GladiatorManager.TryEquipWeapon()`), never from UI directly.
- **No ORM or persistence layer** — the game does not currently persist save data across sessions (state is in-memory only during the session).

---

## 6. Cross-Cutting Concerns

### 6.1 Error Handling & Resilience

All transactional operations follow the **Try-pattern**:

```csharp
bool TryBuyGladiator(int slotIndex, out string failReason)
bool TrySpendGold(int amount, out string failReason)
bool TryEquipWeapon(OwnedGladiatorData g, OwnedWeaponData w, out string failReason)
```

Callers check the return value; UI displays `failReason` to the player. No exceptions are thrown for expected business rule failures.

The LLM integration (`BattleOrdersManager`) uses an HTTP client; network errors should be treated as non-fatal and allow the fallback AI scoring path to proceed.

### 6.2 Randomness

All random values go through `RandomManager`, which wraps a seeded `System.Random`. This ensures:
- Deterministic replays if the same seed is used.
- No direct use of `UnityEngine.Random` in logic code.

### 6.3 Configuration Management

| Concern | Mechanism |
|---|---|
| Game balance tuning | `BalanceSO` asset — edit in Unity Inspector |
| AI behavior tuning | `BattleAITuningSO` asset — per-action weights & radii |
| Content additions | New SO assets added to `ContentDatabaseSO` lists |
| LLM endpoint | `BattleOrdersHttpClient` — URL configurable via field |

### 6.4 Validation

- Market transactions validate: gold sufficiency, slot validity, sell preconditions (unequip first).
- Battle entry validates: at least one ally gladiator selected.
- ScriptableObject data is validated at edit time (Unity Inspector) — no runtime validation is needed since content is immutable.

### 6.5 Logging/Observability

- Unity's `Debug.Log` is used for diagnostic output.
- `BattleSceneTester` and `AfcBattleCheatCode` provide debug utilities and cheat-based testing in Play Mode.
- No structured logging or analytics integration currently.

---

## 7. Service Communication Patterns

### 7.1 Cross-Scene Communication

```
Main Scene                                     Battle Scene
─────────────────────────────────────────────────────────
BattleManager.PrepareAndLaunchBattle()
  → BattleSessionManager.SetPayload(payload)   ← carries BattleStartPayload
  → SceneLoader.LoadBattleScene()
                                               BattleSceneFlowManager.Initialize()
                                                 ← BattleSessionManager.GetPayload()
                                               BattleSimulationManager runs…
                                               TryFinishBattle()
                                                 → SessionManager.SetPendingBattleReward(n)
                                                 → SceneLoader.LoadMainScene()
MainFlowManager.OnMainSceneEnter()
  → SessionManager.ConsumePendingBattleReward()
  → ResourceManager.AddGold(reward)
  → GladiatorManager.GrantVictoryXpToAll()
```

### 7.2 Intra-Scene Communication

Services expose C# `event` delegates. UI managers subscribe in `Start()` / `Initialize()` and unsubscribe in `OnDestroy()`.

```
SessionManager.DayChanged          → MainUIManager (refresh day display)
ResourceManager.GoldChanged        → ResourceUIManager (refresh gold display)
BattleUnitCombatState.OnHealthChanged → BattleRuntimeUnit (update HP bar)
BattleUnitCombatState.OnDied       → BattleSimulationManager (unit removal)
```

### 7.3 LLM Communication

`BattleOrdersManager` → HTTP POST → External LLM endpoint  
Request: `BattleLlmPromptDtos` (serialized battle state)  
Response: `BattleLlmResponseDtos` (parsed order JSON)  
Integration point: orders are injected into `BattleSimulationManager` to bias or override planner scores.

---

## 8. Technology Stack

| Technology | Version | Role |
|---|---|---|
| Unity | 6 (URP 17.2.0) | Game engine, scene management, physics, animation |
| C# | 9+ (.NET Standard 2.1) | Primary language |
| Universal Render Pipeline | 17.2.0 | Rendering |
| Unity Input System | 1.14.2 | Player input handling |
| AI Navigation | — | NavMesh (potential use) |
| Unity Test Framework | — | Edit/Play mode tests |
| TextMeshPro | — | UI text rendering |
| Visual Studio | — | IDE (`.sln` / `.csproj`) |
| Context7 / Claude API | — | LLM battle orders integration |

---

## 9. Implementation Patterns

### 9.1 Singleton Service Pattern

```csharp
// Base class
public class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }
    protected virtual void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = (T)(object)this;
        DontDestroyOnLoad(gameObject);
    }
}

// Usage
public class SessionManager : SingletonBehaviour<SessionManager>
{
    public int CurrentDay { get; private set; }
    public event Action<int> DayChanged;

    public void AdvanceDay()
    {
        CurrentDay++;
        DayChanged?.Invoke(CurrentDay);
    }
}
```

### 9.2 Registry Pattern (Skills)

```csharp
public class BattleSkillRegistry
{
    private readonly Dictionary<WeaponSkillId, IBattleSkill> _skills = new();
    private readonly IBattleSkill _fallback = new DefaultHealSkill();

    public void Register(WeaponSkillId id, IBattleSkill skill) =>
        _skills[id] = skill;

    public IBattleSkill Get(WeaponSkillId id) =>
        _skills.TryGetValue(id, out var skill) ? skill : _fallback;
}
```

### 9.3 Strategy Pattern (Action Planners)

```csharp
public interface IBattleActionPlanner
{
    BattleActionType ActionType { get; }
    bool IsUsable(BattleUnitCombatState unit, BattleFieldView field);
    BattleActionExecutionPlan Build(BattleUnitCombatState unit,
                                   BattleFieldView field,
                                   BattleUnitCombatState[] allStates);
}

// Registration in BattleSimulationManager
_planners = new Dictionary<BattleActionType, IBattleActionPlanner>
{
    { BattleActionType.AssassinateIsolatedEnemy, new AssassinatePlanner(field) },
    { BattleActionType.EngageNearest,            new EngageNearestPlanner(field) },
    // ...
};
```

### 9.4 Try-Pattern for Transactions

```csharp
// Convention: return bool, out string failReason
public bool TryBuyGladiator(int slotIndex, out string failReason)
{
    if (slotIndex >= _gladiatorOffers.Count)
        { failReason = "Invalid slot"; return false; }
    var offer = _gladiatorOffers[slotIndex];
    if (!_resourceManager.TrySpendGold(offer.Price, out failReason))
        return false;
    _gladiatorManager.AddPurchasedGladiatorFromMarketPreview(offer);
    failReason = null;
    return true;
}
```

### 9.5 Snapshot / DTO Pattern for Cross-Scene Payloads

```csharp
// Immutable snapshot created once, passed across scene boundary
public class BattleUnitSnapshot
{
    public Guid SourceRuntimeId { get; }
    public int MaxHealth { get; }
    public int Attack { get; }
    // ... all other stats

    public static BattleUnitSnapshot FromOwnedGladiator(OwnedGladiatorData g)
        => new BattleUnitSnapshot(g.RuntimeId, g.CachedMaxHealth, g.CachedAttack, …);
}

public class BattleStartPayload
{
    public BattleUnitSnapshot[] AllyUnits { get; }
    public BattleUnitSnapshot[] EnemyUnits { get; }
    public PreviewReward ExpectedReward { get; }
}
```

### 9.6 Data-Driven Content Addition

Adding a new gladiator class requires only:
1. Create a new `GladiatorClassSO` asset in `Content/Classes/`.
2. Add it to `ContentDatabaseSO.GladiatorClasses`.
3. No code changes.

---

## 10. Extension and Evolution Patterns

### 10.1 Adding a New Skill

1. Create a class implementing `IBattleSkill`:
   ```csharp
   public interface IBattleSkill
   {
       WeaponSkillId SkillId { get; }
       bool CanActivate(BattleUnitCombatState caster, BattleUnitCombatState target, BattleFieldView field);
       void Apply(ISkillEffectApplier applier, BattleUnitCombatState caster, BattleUnitCombatState target, BattleFieldView field);
   }
   ```
2. Create a matching `WeaponSkillSO` asset with a unique `WeaponSkillId`.
3. Register the skill in `BattleSkillRegistry` initialization inside `BattleSimulationManager`.
4. Assign the `WeaponSkillSO` to a `WeaponSO` asset.

### 10.2 Adding a New Action Planner

1. Create a class implementing `IBattleActionPlanner`.
2. Add the new `BattleActionType` enum value.
3. Add a `BattleActionTuning` entry to `BattleAITuningSO` with tuned weights.
4. Register in `BattleSimulationManager._planners` dictionary.

### 10.3 Adding a New Manager to Main Scene

1. Create the manager class (optionally inheriting `SingletonBehaviour<T>` if cross-scene).
2. Inject dependencies via `AppFlowController.ResolveDependencies()` or `MainFlowManager.Initialize()`.
3. Subscribe UI manager to the new manager's events.
4. Call `InitializeDay()` from `MainFlowManager.HandleEodRequested()` if day-dependent.

### 10.4 Adding a New ScriptableObject Type

1. Create the SO class in `SOScripts/`.
2. Add a typed list to `ContentDatabaseSO`.
3. Create assets under `Content/<NewType>/`.
4. Access via `ContentDatabaseProvider.Database.<NewTypeList>`.

### 10.5 Backward-Compatibility Notes

- `OwnedGladiatorData` uses `Guid` `RuntimeId` as a stable identifier — safe to add new fields without breaking existing save format (once serialization is added).
- `BattleUnitSnapshot` is versioned by the battle session only; no persistence concern.

---

## 11. Architectural Pattern Examples

### 11.1 Stat Derivation and Caching

```csharp
// OwnedGladiatorData.RefreshDerivedStats()
public void RefreshDerivedStats(BalanceSO balance)
{
    float baseHealth = GladiatorClass.baseHealth
                     + GladiatorClass.healthGrowthPerLevel * (Level - 1);
    float weaponBonus = EquippedWeapon?.CachedHealthBonus ?? 0f;
    float variance    = 1f + FinalHealthVariancePercent / 100f;
    CachedMaxHealth   = Mathf.RoundToInt((baseHealth + weaponBonus) * variance);
    // same pattern for Attack, AttackSpeed, …
}
```

### 11.2 AI Scoring Loop

```csharp
// Simplified inner loop inside BattleSimulationManager
foreach (var unit in _aliveUnits)
{
    var rawParams  = _parameterComputer.Compute(unit, _fieldView, _radii);
    var modParams  = ApplyCurrentActionBias(rawParams, unit.CurrentActionType, _tuning);
    var bestAction = _actionScorer.GetBestScoredAction(modParams, unit, _tuning);

    if (_planners.TryGetValue(bestAction.ActionType, out var planner)
        && planner.IsUsable(unit, _fieldView))
    {
        var plan = planner.Build(unit, _fieldView, _allStates);
        ExecutePlan(unit, plan);
    }
}
```

### 11.3 Observer-Driven UI Update

```csharp
// ResourceUIManager
private void Start()
{
    _resourceManager.GoldChanged += OnGoldChanged;
    OnGoldChanged(_resourceManager.CurrentGold);  // initial render
}

private void OnDestroy() =>
    _resourceManager.GoldChanged -= OnGoldChanged;

private void OnGoldChanged(int newGold) =>
    _goldLabel.text = $"{newGold:N0} G";
```

### 11.4 Null Object / Fallback in Registry

```csharp
// BattleSkillRegistry never returns null
public IBattleSkill Get(WeaponSkillId id)
    => _skills.TryGetValue(id, out var s) ? s : _fallback;  // _fallback = DefaultHealSkill

// DefaultHealSkill — safe, always applicable
public class DefaultHealSkill : IBattleSkill
{
    public bool CanActivate(...) => true;
    public void Apply(ISkillEffectApplier applier, ...)
        => applier.HealUnit(caster, 10);
}
```

---

## 12. Architectural Decision Records

### ADR-001: ScriptableObjects as Content Layer

**Context:** The game has many configuration-heavy types (classes, weapons, traits, perks) that designers need to edit without touching code.  
**Decision:** All game-data templates are `ScriptableObject` assets, grouped under `Content/` and collected by `ContentDatabaseSO`.  
**Consequences:**  
+ Designer-friendly; editable in Unity Inspector without rebuilding.  
+ Clean separation between template data and runtime instances.  
− Requires a `ContentDatabaseSO` reference to be threaded through the service layer.

### ADR-002: Pure-Data Battle State (`BattleUnitCombatState`)

**Context:** Unity `MonoBehaviour` classes cannot be unit-tested without a running scene.  
**Decision:** All mutable battle state lives in a plain C# class. `BattleRuntimeUnit` (MonoBehaviour) wraps it for visual purposes only.  
**Consequences:**  
+ State logic can be tested in Edit Mode.  
+ Clear boundary between simulation and rendering.  
− Two objects per unit increases object-graph complexity.

### ADR-003: Immutable Cross-Scene Snapshot

**Context:** Gladiator stats must not change mid-battle if the main scene modifies them.  
**Decision:** `BattleUnitSnapshot.FromOwnedGladiator()` copies all stat values. The snapshot is sealed for the battle's lifetime.  
**Consequences:**  
+ Battle is isolated from main-scene mutations.  
+ Replay or determinism is easier.  
− Post-battle stat changes (e.g., equipment break) require explicit delta messages back.

### ADR-004: Registry for Skills (not inheritance)

**Context:** Skill behavior varies wildly; using a deep class hierarchy for 20+ skills would be brittle.  
**Decision:** `BattleSkillRegistry` maps `WeaponSkillId` → `IBattleSkill`. Skills are value-like objects registered at startup.  
**Consequences:**  
+ Adding a skill requires no changes to core classes.  
+ Fallback (`DefaultHealSkill`) eliminates null-checks everywhere.  
− Developer must remember to register new skills in `BattleSimulationManager`.

### ADR-005: Scoring-Based AI (not Behavior Trees / FSM)

**Context:** Action selection needs to be designer-tunable without code changes.  
**Decision:** Each action type has a score computed from 9 parameters × configurable weights in `BattleAITuningSO`. Best score wins.  
**Consequences:**  
+ Fully data-driven; designers can tweak behavior in the Inspector.  
+ Scales gracefully (add parameters, add action types).  
− Scoring interactions can be non-intuitive; emergent behavior is harder to predict.

### ADR-006: LLM Integration for Strategic Orders

**Context:** The capstone project explores AI-driven game behavior.  
**Decision:** `BattleOrdersManager` sends battle state to a Claude-based endpoint and injects high-level orders back into the planner selection.  
**Consequences:**  
+ Differentiates project; supports research angle.  
− Adds latency; requires fallback when LLM is unavailable.  
− Requires careful prompt/response DTO versioning.

---

## 13. Blueprint for New Development

### 13.1 Feature Addition Flowchart

```
New Feature?
│
├─ Content only (new class/weapon/trait)?
│    → Create SO assets + add to ContentDatabaseSO
│    → No code required
│
├─ New battle mechanic (skill/planner)?
│    → Implement IBattleSkill or IBattleActionPlanner
│    → Register in BattleSimulationManager
│    → Add SO definition + tuning entry
│
├─ New main-scene system (e.g., crafting)?
│    → Add Manager class (optionally SingletonBehaviour<T>)
│    → Wire dependencies in AppFlowController / MainFlowManager
│    → Add UI manager subscribing to manager events
│    → Add InitializeDay() call if day-sensitive
│
└─ New game-wide service (e.g., save system)?
     → Add to BootScripts/
     → SingletonBehaviour<T> with DDOL
     → Wire in AppFlowController.ResolveDependencies()
```

### 13.2 File Placement Guide

| New Component Type | Folder |
|---|---|
| ScriptableObject definition | `Assets/Scripts/SOScripts/` |
| ScriptableObject assets | `Assets/Content/<TypeName>/` |
| Persistent service | `Assets/Scripts/BootScripts/` |
| Main-scene manager | `Assets/Scripts/MainScripts/` |
| Main-scene UI manager | `Assets/Scripts/MainScripts/` |
| Battle manager / computer | `Assets/Scripts/BattleScene/` |
| Battle action planner | `Assets/Scripts/BattleScene/BattlePlanners/` |
| Battle skill | `Assets/Scripts/BattleScene/BattleSkills/` |
| Interface / contract | Same folder as primary consumer |
| Factory | `Assets/Scripts/MainScripts/` (if main-scene) |

### 13.3 Implementing a New Manager (Checklist)

- [ ] Create class, decide if persistent (inherits `SingletonBehaviour<T>`) or scene-local (`MonoBehaviour`)
- [ ] Define public `Initialize(...)` receiving required dependencies
- [ ] Expose events using `event Action<T>` for state changes
- [ ] Follow Try-pattern for all operations that can fail
- [ ] Register in `AppFlowController` (persistent) or `MainFlowManager.Initialize()` (main-scene)
- [ ] Create corresponding UI manager that subscribes to events
- [ ] Add `InitializeDay(int day)` if content refreshes per day

### 13.4 Common Pitfalls

| Pitfall | Correct Approach |
|---|---|
| Reading `ContentDatabaseSO` fields directly from a UI class | Access via manager methods, not raw SO references |
| Using `UnityEngine.Random` in logic code | Use `RandomManager.Instance.Next(...)` |
| Mutating gladiator stats from battle state | Write rewards back via `SessionManager`; apply on main scene re-enter |
| Returning `null` from `BattleSkillRegistry` | Never — always register a fallback and let registry handle missing IDs |
| Adding manager-to-manager direct event subscriptions | Use the orchestrator (`MainFlowManager`) as the mediator |
| Tight-coupling BattleRuntimeUnit to simulation logic | Logic belongs in `BattleUnitCombatState` or a planner; presentation belongs in `BattleRuntimeUnit` |
| Storing scene-lifetime objects in `BattleSessionManager` | `BattleSessionManager` holds cross-scene payload only; clear it after reading |

### 13.5 Testing Approach

| Layer | Test Type | Tool |
|---|---|---|
| `BattleUnitCombatState` | Unit tests | Unity Test Framework (Edit Mode) |
| `BattleParameterComputer` | Unit tests | Unity Test Framework (Edit Mode) |
| `BattleActionScorer` | Unit tests | Unity Test Framework (Edit Mode) |
| Manager logic (e.g., `ResourceManager`) | Unit tests | Unity Test Framework (Edit Mode) |
| Full battle sim | Play Mode integration test | `BattleSceneTester` + cheat codes |
| UI smoke | Manual or `BattleSceneTester` | Play Mode |

Because `BattleUnitCombatState` has no `MonoBehaviour` dependency, combat logic tests can run in headless Edit Mode — the fastest and most reliable tier.

---

## Appendix: Design Pattern Quick Reference

| Pattern | Class(es) |
|---|---|
| Singleton | `SingletonBehaviour<T>`, all persistent managers |
| Registry / Plugin | `BattleSkillRegistry` |
| Strategy | `IBattleActionPlanner` + 7 concrete planners |
| Factory | `RecruitFactory`, `EquipmentFactory` |
| Null Object | `DefaultHealSkill` |
| DTO / Snapshot | `BattleUnitSnapshot`, `BattleStartPayload` |
| Delegation | `BattleRuntimeUnit` → `BattleUnitCombatState` |
| Observer | `event Action<T>` on all service managers |
| Data-Driven | ScriptableObjects (`ContentDatabaseSO` hierarchy) |
| Template Method | `IBattleSkill.CanActivate` → `Apply` |

---

*Blueprint last updated: 2026-04-23. Re-run after significant architectural changes (new scene, new service layer, changes to the battle simulation loop, or changes to cross-scene communication).*
