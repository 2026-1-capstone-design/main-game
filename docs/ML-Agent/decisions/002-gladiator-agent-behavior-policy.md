# GladiatorAgent Behavior Policy Spec

이 문서는 `GladiatorAgent`의 행동 정책을 Observation, Action, Reward, Curriculum, Personality Bias 관점에서 정의한다.

현재 문서는 구현 지시서가 아니라 정책 spec이다. 기존 코드의 `GladiatorActionSchema` contract version 13, `GladiatorObservationSchema`, `GladiatorRewardConfig`, `TrainingAgentBinder` 구조를 기준으로 작성한다.

Context7에서 `/unity-technologies/ml-agents` 라이브러리 ID는 확인했지만, 2026-05-22 현재 세션의 Context7 월간 quota 초과로 최신 문서 본문은 조회하지 못했다. 따라서 이 문서는 현재 코드와 기존 프로젝트 ADR을 우선 기준으로 삼는다.

## 목표

`GladiatorAgent`의 최종 목표는 관전 가능한 검투사 전투에서 팀 승리를 최대화하는 것이다. 팀 승리는 빠를수록 좋고, 승리 시 아군 체력이 많이 남을수록 좋다. 패배는 나쁘고, 타임아웃은 패배보다 더 나쁘다.

개별 Agent의 목표는 팀 승리에 기여하는 범위 안에서 생존하고, 의미 있는 피해를 주며, 불필요한 행동 변동을 줄이는 것이다. 생존 자체가 전투 회피로 학습되지 않도록, 생존 보상은 큰 per-step positive reward로 두지 않는다.

정책 설계 원칙은 다음과 같다.

- Action과 Reward는 가능한 한 단순하게 유지한다.
- Observation은 Agent가 전술 판단을 할 수 있도록 풍부하게 제공한다.
- 행동 일관성은 terminal team reward만으로 학습시키기 어렵기 때문에 약한 switch penalty와 movement smoothness shaping을 허용한다.
- 복합 전략은 이 정책에서 제한한다. 특히 "목표 적을 유지한 채 다른 anchor를 경유해서 우회"하는 Target/Anchor 분리는 현재 범위에서 제외한다.

## 설계 결정 요약

최종 정책은 다음과 같다.

- Anchor와 공격 Target은 같은 대상으로 취급한다.
- Anchor은 Enemy Unit만 허용한다.
- 교전 태세는 단일 Strategy 필드로 표현한다. 이는 현재 Strategy라는 이름으로 구현되어 있으며, Role은 삭제한다.
- 생존 시간 보상은 per-step positive reward로 두지 않는다.
- Personality Bias는 새 reward category를 대량 추가하지 않고 기존 reward weight를 섞는 방식으로 적용한다.

## Action Contract

Action은 2개의 continuous 값과 3개의 discrete branch로 구성한다.

```text
Continuous Actions
- 0: anchor strafe
- 1: anchor forward

Discrete Branches
- Branch 0: Command
- Branch 1: Strategy (Strategy)
- Branch 2: Anchor
```

### Movement

Movement는 anchor 기준 local vector다.

- `ContinuousAnchorStrafe`: anchor를 바라보는 축의 좌우 이동 성분이다.
- `ContinuousAnchorForward`: anchor를 향하거나 멀어지는 이동 성분이다.
- 입력 vector는 크기 1로 normalize한다.
- 이동 속도 크기는 action이 아니라 유닛 stat과 simulation이 결정한다.

Move command에서는 이 vector가 anchor 기준 전체 360도 이동 방향으로 해석된다.

Attack command에서는 simulation의 `MoveTowardsTarget` 경로가 target 접근을 우선한다. 이때 movement vector는 target 접근 방향 주변의 제한된 접근 각도를 만드는 보조 입력으로만 사용한다. 현재 `BattlePhysicsSystem`은 raw angle을 0.25배로 압축해 공격 접근 방향을 좁힌다.

### Command

Command는 행동의 즉시 실행 의도를 나타낸다.

```text
0: Move
1: Attack
```

`Move`는 전투 명령 없이 anchor 기준 이동만 수행한다. `Attack`은 유효한 enemy target이 있을 때 기본 공격을 시도하고, target이 사거리 밖이면 target까지 접근한다.

유효한 target 없이 `Attack`이 선택되면 실행 시 `Move`로 canonicalize한다. 다만 이런 상황이 반복되면 action masking 또는 invalid action penalty로 제어해야 한다.

### Strategy

Strategy는 현재 anchor에 대한 단기 교전 태세다.

```text
0: Neutral
1: Pressure
2: KeepRange
3: Retreat
```

`Neutral`은 별도 shaping이 적은 기준 모드다. Agent가 명확한 압박, 거리 유지, 후퇴 조건을 선택하지 않을 때 사용한다.

`Pressure`는 anchor와 거리를 좁히고 유리한 교전을 만드는 모드다. 접근, 사거리 내 공격, 유리한 위협 교환을 보상한다.

`KeepRange`는 자신의 유효 사거리 근처 band를 유지하고, 너무 가까워졌을 때 거리를 회복하는 행동을 보상한다.

`Retreat`는 anchor와 거리를 벌리는 모드다. 체력이 낮거나 위협 교환이 불리할 때 유효하다. 단, Retreat는 생존 시간 보상과 결합되면 회피 성향을 키울 수 있으므로 독립적인 큰 reward를 주지 않는다.

### Anchor

Anchor는 이동 기준점이자 공격 Target이다.

```text
0~5: Enemy slot 0~5
```

살아있는 적 slot만 action mask로 허용한다.

Anchor로 Ally와 TeamCenter 등은 다음 이유로 허용하지 않는다.

- Agent가 한 덩어리로 뭉쳐 싸우는 local optimum을 만들 수 있다.
- 관전자가 기대하는 산개, 압박, 거리 유지 행동이 줄어들 수 있다.
- Ally Anchor는 공격 대상이 아니므로 아군 전용 Strategy를 만들어야 한다.

Ally Anchor가 unlock된 경우에는 combat-only branch를 다음처럼 정규화한다.

## Observation Contract

Observation은 행동 단순성을 보완하기 위해 충분히 풍부해야 한다. 현재 contract는 `GladiatorObservationSchema.TotalSize`를 기준으로 한다.

Observation Vector의 Total Size는 동적으로 주입하지 못하므로, GladiatorObservationSchema의 주석을 SSoT로 삼는다. 다른 파일과 문서에서 Observation Vector의 Total Size의 지정을 금지한다.

Self observation은 다음 범주를 포함한다.

- 경기장 중심에 대한 anchor-axis 상대 위치
- 체력 비율, 최대 체력 로그 비율, 공격력 로그 비율
- 사거리, 이동속도, 공격 쿨타임
- anchor와 자신의 상호 위협 비율
- anchor가 내 사거리 안인지, 내가 anchor 사거리 안인지 여부
- 좌우 lane 여유도
- 적 밀집 압박도
- 경계 압박도
- 타임아웃까지 남은 시간 비율
- 현재 Action의 4개 branch와 continuous 입력 (총 5)값
- anchor 지원 압박, focus 압박, 고립도, 후퇴 신호
- 성격 bias (collectivism, passiveness)

Teammate observation은 각 아군 slot에 대해 다음을 포함한다.

- anchor-axis 상대 위치
- 자신과의 거리 비율
- 체력 비율
- 최대 체력 로그 비율
- 공격력 로그 비율
- 사거리
- 이동속도
- 공격 쿨타임

Opponent observation은 teammate 공통 필드에 더해 다음을 포함한다.

- 해당 적이 자신을 공격적으로 target하고 있는지 여부

Observation의 좌표계는 world axis가 아니라 anchor-axis를 우선한다. 이렇게 해야 Agent가 target을 기준으로 "좌우로 돌기", "접근하기", "거리 벌리기"를 일반화하기 쉽다.

## Reward Policy

Reward는 세 층으로 나눈다.

```text
1. Team outcome reward
2. Individual combat reward
3. Small tactical shaping
```

### Team Outcome Reward

팀 보상은 MA-POCA group reward로 지급한다.

**중요**: 팀 보상은 다음과 같이 계산한 후, -100~100 범위로 normalize한다.

승리 팀은 `groupWin`, 패배 팀은 `groupLoss`를 받는다. 승리 reward에는 남은 시간과 승리 팀 남은 HP 비율을 multiplier로 반영한다.

```text
winReward = groupWin * speedMultiplier * hpMultiplier
lossReward = groupLoss * speedMultiplier * hpMultiplier

speedMultiplier = 1 + (winSpeedBonus - 1) * timeRemainingRatio
hpMultiplier = 1 + (winHpBonus - 1) * winnerHpRatio
```

타임아웃은 양 팀 모두에게 패배보다 나쁜 reward를 준다.

```text
timeoutReward =
    groupLoss
    * winSpeedBonus
    * winHpBonus
    * timeoutMultiplier
    * timeoutHpMultiplier
```

타임아웃 reward는 남아 있는 적 HP 비율이 높을수록 더 나빠진다. 이 설계는 "끝내지 못한 경기"를 명확히 나쁜 결과로 만든다.

### Individual Combat Reward

개인 보상은 전투 기여를 중심으로 둔다.

공격 명중 시 다음을 지급한다.

```text
attackReward = attackLanded + actualDamage / targetMaxHealth * damageDealtRatio
```

damageDealtRatio는 기본적으로 1이고, 학습 경과에 의해 조정해야 하는 hyperparameter이다.

처치 시 `kill` reward를 추가한다.

피격 시 다음을 지급한다.

```text
damageTakenPenalty = actualDamage / selfMaxHealth * damageTakenRatio
```

damageTakenRatio는 기본적으로 1이고, 학습 경과에 의해 조정해야 하는 hyperparameter이다.

사망 시 `death` penalty를 지급한다.

생존 시간은 매 step positive reward로 지급하지 않는다. 이미 `damageTakenRatio`, `death`, team HP multiplier가 생존의 가치를 반영한다. 별도 생존 보상이 필요하면 episode 종료 시 작은 terminal survival bonus로 제한한다.

### Tactical Shaping

전술 shaping은 행동을 설명할 수 있을 정도로만 둔다.

`step` penalty는 두지 않는다. 이미 팀 보상에서 빠른 승리를 보상하고, timeout을 패배보다 나쁘게 처리한다. 시간 끌기를 방지하려면 step penalty가 아니라 Episode 종료 시 개인의 timeout penalty로 처리한다.

Movement smoothness penalty는 직전 command와 현재 command가 모두 `Move`일 때만 적용한다. Attack 중에는 게임 시스템이 target 방향으로 facing과 접근을 강제하므로 smoothness penalty를 적용하지 않는다.

Switch penalty는 다음 branch에 적용한다.

- Command switch
- Strategy switch
- Anchor switch

Switch penalty는 행동 고정을 강제할 정도로 커서는 안 된다. 목적은 전략 변경 자체를 금지하는 것이 아니라, 한 전략 안에서 불필요하게 흔들리는 행동을 줄이는 것이다.

Commitment reward는 기본값 0을 권장한다. 필요하면 switch penalty보다 약하게 두고, 평균 유지 step이 너무 짧을 때만 켠다.

### Strategy Reward

`Pressure`는 target 접근과 유리한 공격 range를 보상한다.

```text
if approachDelta > 0:
    reward += approachDelta * pressureApproachReward
```

`KeepRange`는 적정 거리 band에 있을 때 보상하고, 너무 가까운 상태에서 거리를 회복할 때 보상한다.

```text
distanceRatio = targetDistance / ownEffectiveAttackRange
reward if keepRangeBandMin <= distanceRatio <= keepRangeBandMax
```

`Retreat`는 target과 거리 증가, 사거리 이탈 순간을 보상한다. 단, 무조건 target과 반대편으로 멀어지는 것을 최대 보상으로 설정하지 말고, Command: Move / ContinuousAnchorForward가 음수 방향일 때 같은 값의 보상을 준다. 이렇게 하면 Retreat가 target과 반대편으로 멀어지는 행동을 선호하긴 하지만, target과 반대편으로 완전히 도망치는 행동이 최적해가 되는 것을 방지한다.

`Neutral`은 별도 shaping을 두지 않는 기준 모드로 유지한다.

## Personality Bias

성격은 reward category의 가중치로 표현한다.

두 축을 사용한다.

```text
Collectivism <-> Individualism
Passive <-> Aggressive
```

Collectivism은 MA-POCA group reward 자체를 직접 변경하지 않는다. team outcome reward는 모든 agent가 공유하는 학습 신호이므로, 성격별로 이 값을 다르게 스케일하면 MA-POCA의 credit assignment를 흐릴 수 있다. 대신 Collectivism은 개인 보상의 상대 크기만 낮춰 team reward가 더 크게 보이도록 만들고, Individualism은 개인 보상의 상대 크기만 높인다.

Passive는 damage taken, death, Retreat/KeepRange 관련 penalty 회피를 더 중시한다. Aggressive는 damage dealt, attack landed, Pressure, kill을 더 중시한다.

Personality Bias는 기본 reward semantics를 바꾸지 않는다. 같은 전투 결과에 대해 어떤 reward category를 더 크게 볼지 조정하는 layer로만 둔다.

Strategy reward도 개인 보상에 포함된다. 따라서 Strategy shaping에는 Personality Bias를 반드시 적용한다. `Pressure` shaping은 공격적 개인 기여에 가까우므로 `modifiedDamageWeight`를 사용하고, `KeepRange`와 `Retreat` shaping은 생존/위험 회피 성향에 가까우므로 `modifiedSurvivalWeight`를 사용한다. `Neutral`은 별도 shaping이 없으므로 성격 weight를 적용할 reward도 없다.

권장 mixing 방식은 다음과 같다. `teamWeight`는 team reward에 직접 곱하지 않고, individual reward scale의 분모로만 사용한다.

```text
teamWeight = lerp(0.8, 1.2, collectivism)
individualWeight = lerp(1.2, 0.8, collectivism)
survivalWeight = lerp(0.8, 1.2, passiveness)
damageWeight = lerp(1.2, 0.8, passiveness)

collectivismIndividualScale =
    clamp(
        originalIndividualWeight
        * (individualWeight / max(teamWeight, epsilon)),
        personalityCategoryWeightMin,
        personalityCategoryWeightMax
    )

modifiedDamageWeight = collectivismIndividualScale * damageWeight
modifiedSurvivalWeight = collectivismIndividualScale * survivalWeight
modifiedIndividualReward =
    originalIndividualReward
    * categoryModifiedWeight

modifiedPressureStrategyReward =
    originalPressureStrategyReward * modifiedDamageWeight

modifiedKeepRangeStrategyReward =
    originalKeepRangeStrategyReward * modifiedSurvivalWeight

modifiedRetreatStrategyReward =
    originalRetreatStrategyReward * modifiedSurvivalWeight
```

즉 `modified individual reward := original individual reward * (weighted individual reward / weighted team reward)` 형태로 개인 보상만 재가중한다. 여기서 `weighted team reward`는 실제 MA-POCA group reward 값이 아니라 `teamWeight`로 표현되는 기준 가중치다. 실제 team outcome reward 계산과 지급은 Personality Bias에 의해 바뀌지 않는다.

이 값은 초기 후보일 뿐이며, category별 episode total이 team outcome reward를 압도하지 않도록 clamp한다.

## No Curriculum

현재까지의 실험 결과에서 Curriculum은 오히려 학습을 방해하는 것으로 나타났다.

## Metrics

커스텀 Tensorboard 매트릭(StatsRecorder)을 기록할 때 성능 저하를 최소화하기 위해 다음 최적화 방법을 사용한다.

1. 문자열 동적 생성 최소화

메트릭 키로 사용할 문자열은 `Start`, `Initialize`, static readonly field 등 초기화 시점에 미리 생성하여 재사용한다. `OnActionReceived`, simulation tick, episode flush 같은 hot path에서는 `$"{...}"`, 문자열 `+`, enum 이름 기반 동적 key 생성을 하지 않는다.

동일한 metric family를 여러 enum 값으로 기록해야 할 때도 key 목록을 미리 만든다. 예를 들어 `Combat/StrategyShare/Pressure` 같은 문자열은 매 step 생성하지 않고, Strategy enum index로 캐싱된 문자열 배열에서 꺼내 쓴다.

2. `summary_freq` 조정

trainer 설정의 `summary_freq`를 너무 작게 두지 않는다. custom metric이 많거나 병렬 환경 수가 많을수록 summary 작성과 Unity-Python 통신 부담이 커질 수 있으므로, 학습 진단에 필요한 해상도를 유지하는 범위에서 `summary_freq`를 늘린다.

```yaml
behaviors:
  GladiatorBehavior:
    summary_freq: 50000
```

`summary_freq`는 trainer가 TensorBoard summary를 쓰는 빈도를 조절하는 값이다. Unity 내부에서 metric을 어떻게 평균낼지 결정하는 기준으로 사용하지 않는다.

3. 10,000 step 단위 로컬 평균 전송

각 `GladiatorAgent`는 자기 `GladiatorAgentEpisodeMetrics` 인스턴스에 metric 원천값을 누적한다. `StatsRecorder.Add`는 매 step 호출하지 않고, agent별로 10,000 decision step마다 누적값을 flush한다.

평균 metric은 단순히 10,000으로 나누지 않는다. metric마다 실제 기록 조건이 다르기 때문에, 각 metric의 누적합을 해당 metric의 기록 발생 횟수로 나눈 자체 평균을 `StatsRecorder`에 전달한다.

```text
reportedMetric = metricValueSum / max(metricSampleCount, 1)
```

예를 들어 `AttackOpportunityUseRate`는 attack opportunity가 있었던 step만 sample count로 삼고, `MeanEnemyRangeOffset`은 유효 enemy anchor가 있었던 step만 sample count로 삼는다. 조건을 만족한 sample이 하나도 없으면 해당 flush 주기에는 그 metric을 기록하지 않는다.

이 방식은 `StatsRecorder` 호출 빈도를 낮추면서도, 조건부 metric의 평균이 "조건이 맞았던 순간들의 평균"이라는 의미를 유지한다. 다만 Python trainer의 summary window와 Unity의 10,000-step flush window는 같은 개념이 아니므로, 이 정책은 raw per-step 기록과 완전히 동일한 TensorBoard 곡선을 보장하지 않는다. 성능과 진단 해상도의 균형을 위한 최종 기록 정책으로 채택한다.

다음 metric을 기록한다.

- `Combat/BattleFinished`
- `Combat/FinalBattleRemainingHealthRatio`
- `Combat/DamageDealtRatio`
- `Combat/AttackOpportunityUseRate`
- `Combat/CommandSwitch`
- `Combat/AnchorSwitch`
- `Combat/StrategySwitch`
- `Combat/CommandMaintenance`
- `Combat/AnchorMaintenance`
- `Combat/RoleMaintenance`
- `Combat/StrategyMaintenance`
- `Combat/MeanEnemyRangeOffset`
- `Combat/StrategyAnchorRangeOffset/*`
- `Combat/CommandShare/*`
- `Combat/StrategyShare/*`
- `Combat/AnchorKindShare/*`

정책이 좋은 방향으로 가고 있는지 판단하는 기준은 다음과 같다.

- timeout 비율이 낮아진다.
- 승률이 유지되거나 오른다.
- 평균 episode 길이가 불필요하게 늘지 않는다.
- Attack opportunity use rate가 충분히 높다.
- Damage dealt ratio가 증가한다.
- Strategy가 하나로 붕괴하지 않는다.
- Anchor와 Role 유지 step이 너무 짧지 않다.
- Ally/TeamCenter unlock 후 AnchorKindShare가 한쪽으로 붕괴하지 않는다.

## Known Risks

Retreat와 KeepRange reward는 회피 성향을 강화할 수 있다. damage dealt, attack opportunity, timeout metric과 함께 봐야 한다.

Switch penalty가 너무 크면 상황 변화에 대응하지 못한다. 유지 step metric과 승률을 함께 보고 조정한다.

생존 reward가 너무 크면 전투 기여보다 도망이 안정적인 최적해가 된다. 생존은 damage taken 감소와 death penalty 회피로 주로 학습시킨다.
