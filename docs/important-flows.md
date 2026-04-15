
목차)
1. 날짜 진행
2. 시장 검투사 생성
3. 시장 무기 생성
4. 검투사 구매
5. 무기 구매
6. 검투사 판매
7. 무기 판매 차단 및 판매
8. 장비 장착
9. 장비 해제
10. 하루 전투 후보 생성
11. 적 편성 생성
12. 전투 준비 패널 오픈
13. 전투 후보 선택
14. 전투 시작 payload 생성
15. 전투 진입 직전 payload 저장 + 오늘 전투 사용 처리
16. 전투 보상 pending 저장
17. 메인 복귀 후 보상 수령
18. 메인 복귀 후 승리 XP 지급







1. 날짜 진행

MainUIManager의 날짜 종료 버튼
→ MainFlowManager.HandleEodRequested()
→ SessionManager.AdvanceDay()
→ 내부에서 CurrentDay++, ResetBattleUsageForNewDay()
→ DayChanged?.Invoke(CurrentDay) 호출
→ 같은 시점에 marketManager.InitializeDay(CurrentDay), battleManager.InitializeDay(CurrentDay)로 시장/전투 후보 재생성


2. 시장 검투사 생성

MainFlowManager 쪽에서 marketManager.InitializeDay(CurrentDay)
→ RecruitFactory.CreateMarketGladiatorOffer(...)
→ 내부에서 CreatePreviewGladiatorForDay(...)
→ OwnedGladiatorData 형태의 preview 생성
→ MarketGladiatorOffer에 담겨 시장 슬롯에 올라감

시장에서 보이는 검투사는 실제 보유 검투사가 아니라 preview임


3. 검투사 구매

시장 슬롯 클릭
→ MarketUIManager에서 구매 요청
→ MarketManager.TryBuyGladiator(slotIndex, out failReason)
→ ResourceManager.TrySpendGold(...)로 먼저 골드 차감
→ GladiatorManager.AddPurchasedGladiatorFromMarketPreview(...)
→ preview 데이터를 복사해서 새 RuntimeId를 가진 실제 owned gladiator 생성
→ 성공 시 offer.MarkSold()


4. 시장 무기 생성

marketManager.InitializeDay(CurrentDay)
→ EquipmentFactory.CreateMarketWeaponOffer(...)
→ 내부에서 CreateRandomWeaponPreviewForDay(...)
→ OwnedWeaponData 형태의 preview 생성
→ MarketWeaponOffer에 담겨 시장 슬롯에 올라감


5. 무기 구매

시장 무기 슬롯 클릭
→ MarketUIManager에서 구매 요청
→ MarketManager.TryBuyWeapon(slotIndex, out failReason)
→ ResourceManager.TrySpendGold(...)
→ InventoryManager.AddPurchasedWeaponFromMarketPreview(...)
→ preview를 복사해서 새 RuntimeId를 가진 실제 owned weapon 생성
→ 성공 시 offer.MarkSold()


6. 검투사 판매

판매 대상 선택
→ MarketUIManager에서 판매 요청
→ MarketManager.TrySellGladiator(gladiator, out sellPrice, out failReason)
→ GladiatorManager.RemoveOwnedGladiator(gladiator)
→ 내부에서 UnequipWeaponIfAny(gladiator)로 장착 무기 먼저 해제
→ 보유 목록에서 제거 성공 시 ResourceManager.AddGold(sellPrice)


7. 무기 판매 차단 및 판매

판매 대상 무기 선택
→ MarketManager.TrySellWeapon(weapon, out sellPrice, out failReason)
→ 먼저 GladiatorManager.FindOwnerOfEquippedWeapon(weapon) 검사
→ owner가 있으면 판매 거부
→ owner가 없으면 InventoryManager.RemoveOwnedWeapon(weapon)
→ 성공 시 ResourceManager.AddGold(sellPrice)


8. 장비 장착

검투사 UI에서 장착 요청
→ GladiatorManager.TryEquipWeapon(gladiator, weapon, out failReason)
→ FindOwnerOfEquippedWeapon(weapon)로 이미 다른 검투사가 쓰는 무기인지 검사
→ 중복 장착 아니면 gladiator.EquippedWeapon = weapon
→ RefreshDerivedStats(gladiator, false) 실행


9. 장비 해제

검투사 UI에서 해제 요청
→ GladiatorManager.TryUnequipWeapon(gladiator, out failReason)
→ gladiator.EquippedWeapon = null
→ RefreshDerivedStats(gladiator, false)


10. 하루 전투 후보 생성

MainFlowManager.InitializeScene() 또는 HandleEodRequested()
→ BattleManager.InitializeDay(CurrentDay)
→ RecruitFactory.CreateBattleEncounterPreviewsForDay(CurrentDay)
→ 난이도별 BattleEncounterPreview 생성
→ BattleManager._dailyEncounters에 캐시



11. 적 편성 생성

RecruitFactory.CreateBattleEncounterPreviewsForDay(...)
→ 난이도별로 CreateBattleEncounterPreviewForDifficulty(...)
→ 각 적 유닛에 대해 CreatePreviewGladiatorAtLevel(...)
→ TryEquipRandomWeaponForBattlePreview(...)로 랜덤 무기 장착
→ BattleUnitSnapshot.FromOwnedGladiator(preview, true)
→ BattleEncounterPreview 완성



12. 전투 준비 패널 오픈

메인 전투 버튼 클릭
→ MainFlowManager.HandleBattleMenuRequested()
→ BattleManager.TryOpenBattlePreparation(out failReason)
→ 성공 시 BattleUIManager.OpenBattlePanel(battleManager.DailyEncounters, battleManager.SelectedEncounterIndex)


13. 전투 후보 선택

전투 준비 화면에서 특정 row 클릭
→ BattleUIManager.OnVeryLow/Low/Medium/HighRowClicked()
→ MainFlowManager.HandleBattleEncounterSelected(index)
→ BattleManager.TrySelectEncounter(index)
→ BattleUIManager.RefreshSelection(...)



14. 전투 시작 payload 생성

전투 시작 버튼 클릭
→ BattleUIManager.OnStartClicked()
→ MainFlowManager.HandleBattleStartRequested()
→ TryBuildBattleStartPayload(out payload)
→ 내부에서 BattleManager.TryGetSelectedEncounterForBattle(...)
→ TryBuildAllySnapshotsForBattle(...)
→ TryBuildEnemySnapshotsForBattle(...)
→ new BattleStartPayload(...)

일반적으론 오늘 캐시된 전투 후보 그대로 사용
치트코드 사용시 그 시점에 적 팀을 다시 생성해서 사용


15. 전투 진입 직전 처리

MainFlowManager.HandleBattleStartRequested()
→ StartCoroutine(LoadBattleSceneRoutine(payload))
→ BattleSessionManager.StorePayload(payload)
→ 배틀씬 로드 시작
→ SessionManager.MarkBattleUsed() 호출


16. 전투 보상 저장

전투 진행 중
→ BattleSimulationManager.TryFinishBattle()
→ 아군/적군 생존 여부로 승패 판정
→ 승리면 CalculateVictoryReward(currentDay)로 보상 계산, 패배면 0
→ SessionManager.Instance.SetPendingBattleReward(pendingReward)
→ BattleResolution.Create(wasWin, pendingReward, currentDay)
→ BattleSceneUIManager.ShowBattleEndPanel(resolution)

전투 보상은 배틀씬에서 즉시 골드로 지급되지 않고, 먼저 SessionManager에 pending reward로 저장.
즉 배틀씬은 보상 계산 + pending 저장 + 결과 UI 표시까지만 맡고,
실제 골드 지급은 메인씬 복귀 후 처리. 바로 아래 17번 참고.


17. 보상 수령

메인씬 진입
→ MainFlowManager.InitializeScene() 안에서 TryGrantPendingBattleRewardOnMainSceneEnter() 호출
→ SessionManager.HasPendingBattleReward 검사
→ 있으면 ResourceManager.GrantPendingBattleReward(sessionManager)
→ 내부에서 sessionManager.ConsumePendingBattleReward()
→ 실제 골드 지급

전투 보상은 메인씬 !!재진입 시점!!에 정산된다.
즉, 유저의 특정 입력에 반응하는 것이 아니라
배틀씬에서 메인씬으로 돌아올 때 트리거됨.


18. 승리 XP 지급

보상 수령 플로우 중
→ ResourceManager.GrantPendingBattleReward(...) 결과 paidGold > 0
→ GladiatorManager.GrantVictoryXpToAllOwnedGladiators()




