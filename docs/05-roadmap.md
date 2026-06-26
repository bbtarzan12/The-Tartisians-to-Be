# 05. 로드맵 (상세 실행 스펙)

자율 실행(goal)의 **실행 가능한 단일 스펙**이다. 각 마일스톤은 ⟨목표 → 산출물(스크립트/SO/데이터/씬·프리팹) → 테스트 → 검증 게이트 → 결정 포인트⟩로 정의된다. 게이트를 통과해야 다음 단계로 넘어간다.

> 표기: 🟩 합의된 기본값(필요 시 조정) · ❓ 착수 전 확정 필요한 결정 포인트

---

## 접근법: 수직 슬라이스

작지만 처음부터 끝까지 굴러가는 한 줄기를 먼저 완성하고 살을 붙인다. 각 단계마다 MCP 하네스로 컴파일·테스트·플레이모드·프로파일링을 실측 검증한다.

| # | 단계 | 한 줄 요약 |
|---|------|-----------|
| 1 | 기반 골격 | asmdef·EventBus·풀·FSM·부트스트랩·URP 설정 |
| 2 | 플레이어 | Input System 이동·카메라·체력 |
| 3 | 적 + 스폰 + 풀링 | 추적 AI·스포너·SpatialGrid·200체 성능 |
| 4 | 자동 전투 | 무기 자동발사·투사체·데미지·사망 VFX |
| 5 | 진행 루프 | XP젬·레벨업·업그레이드 3택 |
| 6 | HUD + 생존 | UI Toolkit HUD·5분 승패·게임 상태 연결 |

---

## 마일스톤 1 — 기반 골격

**목표:** 컴파일되는 빈 골격 + 핵심 인프라(이벤트/풀/상태기계/부트스트랩) + 렌더링 설정.

**산출물 — 스크립트 (`Tartisians.*` namespace, C# 9, 블록 namespace)**
- **Core**
  - `GameBootstrap` — `[RuntimeInitializeOnLoadMethod]` 진입점. ServiceLocator 초기화, 상태기계 시작.
  - `EventBus` 패키지 — `IEvent`(마커), `EventBinding<T>`, `EventBus<T>`(register/deregister/raise), 씬 전환 시 자동 클리어.
  - `ServiceLocator` — 전역 서비스 등록/조회 (싱글톤 대체).
  - `GameStateMachine` + `IGameState` — 상태: `BootState`, `MenuState`, `PlayState`, `LevelUpState`, `GameOverState`(빈 골격).
- **Systems**
  - `IPoolable` — `OnSpawned()`, `OnDespawned()`.
  - `PrefabPool` — `UnityEngine.Pool.ObjectPool<T>` 래핑. Get/Release, 사전 워밍, 부모 트랜스폼.
- **asmdef 6개**: Game.Core, Game.Data, Game.Systems, Game.Gameplay, Game.UI, Game.Tests (의존성 방향은 `docs/02`).

**산출물 — 씬**
- `Bootstrap.unity` — 최소 씬(진입점 검증용).

**산출물 — 렌더링 설정**
- URP 에셋: Deferred+ · GPU Resident Drawer(Instanced) · GPU Occlusion Culling · SRP Batcher · BatchRendererGroup Variants = Keep All.

**테스트**
- `EventBusTests`(EditMode) — register→raise→수신, deregister 후 미수신.
- `PrefabPoolTests`(PlayMode) — Get/Release 재사용, IPoolable 콜백 호출.
- `GameStateMachineTests`(EditMode) — 전이·Enter/Exit 호출.

**검증 게이트:** 컴파일 에러 0 · 위 테스트 통과 · 부트스트랩 진입 로그 확인.

**결정 포인트:** 없음 (인프라만).

---

## 마일스톤 2 — 플레이어

**목표:** WASD로 움직이고 카메라가 따라오며 체력을 가진 플레이어.

**산출물 — 입력**
- `Controls.inputactions` — Action Map `Gameplay`: `Move`(Vector2, WASD/좌스틱), `Pause`(Button). 생성 C# 래퍼 사용.
- `InputReader` — 입력 이벤트/값을 게임플레이로 노출 (`MoveInput` 등).

**산출물 — 스크립트 (Gameplay)**
- `PlayerController` — 입력 → 이동. 🟩 평면(XZ) 이동, Rigidbody(키네마틱 X, 물리 이동) 기반.
- `Health` — **공용 컴포넌트**(플레이어·적 재사용). `IDamageable` 구현, 데미지/사망 이벤트.
- `PlayerStats` ← `PlayerDefinition`(SO): maxHP, moveSpeed, pickupRadius 등.
- `CameraRig` — 플레이어 추종. 🟩 탑다운/쿼터뷰.

**산출물 — 데이터/씬/프리팹**
- `PlayerDefinition.asset`, `Player.prefab`(캡슐 플레이스홀더 + MeshRenderer), `Game.unity` 씬(플레이어 배치).

**테스트**
- `PlayerMovementTests`(PlayMode) — Move 입력 주입 → 위치 변화.
- `HealthTests`(EditMode) — 데미지 적용·0 이하 사망 이벤트·과회복 클램프.

**검증 게이트:** 플레이모드에서 WASD 이동 + 카메라 추종(스크린샷 확인) · 테스트 통과.

**결정 포인트:**
- ❓ **카메라 시점** — 탑다운(수직) / 쿼터뷰(45°) / 3인칭 등.
- ❓ **Cinemachine 도입 여부** — 무료·표준. 추종/흔들림 권장. (패키지 추가)
- ❓ **이동 방식** — Rigidbody vs CharacterController.

---

## 마일스톤 3 — 적 + 스폰 + 풀링

**목표:** 플레이어를 추적하는 적이 시간에 따라 풀에서 스폰. 200체에서 안정.

**산출물 — 스크립트**
- **Data**: `EnemyDefinition`(SO) — maxHP, moveSpeed, 접촉 데미지, XP 보상, 메시/머티리얼. `WaveDefinition`(SO) — 시간대별 스폰 종류·밀도·간격.
- **Systems**: `SpatialHashGrid` — 셀 기반 이웃 질의(분리 행동·히트 판정용, 물리 대체).
- **Gameplay**:
  - `Enemy`(`IPoolable`, `IDamageable`) — seek(플레이어) + separation(이웃, SpatialGrid) 스티어링. 접촉 시 플레이어에 데미지. 사망 시 풀 반환.
  - `EnemySpawner` — `WaveDefinition` 기반 시간 진행 스폰. 플레이어 주변 링에 PrefabPool로 생성.
  - `EnemyRegistry` — 활성 적 추적(스포너/타게팅용, 경량).

**산출물 — 데이터/프리팹**
- 🟩 적 3종: `Chaser`(기본), `Swift`(빠르고 약함), `Brute`(느리고 단단). `Enemy.prefab`(캡슐, MeshRenderer). `Wave_Default.asset`.

**테스트**
- `EnemySpawnerTests`(PlayMode) — N체 스폰, 풀 재사용(사망→풀 복귀→재스폰 시 새 할당 없음).
- `EnemySteeringTests`(PlayMode) — 적이 플레이어로 수렴, 과밀 시 분리.

**검증 게이트:** **200체 스폰 시 목표 프레임 유지(프로파일러)** · 풀 재사용 확인(GC 스파이크 없음) · 테스트 통과.

**결정 포인트:**
- ❓ **성능 목표** — 200체에서 60fps / 30fps / 120fps.
- 🟩 적 종류·스탯은 기본값으로 시작, 밸런싱은 데이터로 조정.

---

## 마일스톤 4 — 자동 전투

**목표:** 무기가 자동 발사 → 적 처치 → 사망 VFX(풀) + XP젬(풀) 드랍.

**산출물 — 스크립트**
- **Data**: `WeaponDefinition`(SO) — 발사 간격, 데미지, 투사체 속도, 관통 수, 동시 발사 수, 사거리.
- **Systems**: `DamageSystem` — 데미지 적용 중앙화. `IDamageable` 경유.
- **Gameplay**:
  - `Weapon` / `WeaponInventory` — 자동 발사 타이머(Awaitable), 최근접 적 조준(SpatialGrid 질의).
  - `Projectile`(`IPoolable`) — 이동·명중·데미지·관통 카운트·수명.
  - `ProjectilePool`, `VfxPool` — VFX Graph 인스턴스 풀링.
  - `EnemyDeathHandler` — 적 사망 → VFX·XP젬 스폰, `EnemyDiedEvent` 발행.

**산출물 — 데이터/프리팹/VFX**
- 🟩 시작 무기 1종(`MagicBolt` — 최근접 자동 조준 투사체). `Projectile.prefab`. `WeaponDefinition.asset`.
- VFX Graph: 사망 폭발 1종(GPU 파티클), 풀링.

**테스트**
- `WeaponFireTests`(PlayMode) — 간격마다 발사, 최근접 타겟팅.
- `ProjectileTests`(PlayMode) — 명중 시 데미지·관통 감소·수명 후 풀 복귀.
- `DamageSystemTests`(EditMode) — 데미지→사망→이벤트.

**검증 게이트:** 적이 죽고 VFX가 풀에서 재생(스크린샷) · 대량 사망 시 GC 스파이크 없음 · 테스트 통과.

**결정 포인트:**
- ❓ **VFX Graph 패키지 추가**(`com.unity.visualeffectgraph`).
- 🟩 무기 종류 확장(체인/장판/근접 등)은 후속, 1종으로 루프 검증.

---

## 마일스톤 5 — 진행 루프

**목표:** XP젬 흡수 → 레벨업 → 업그레이드 3택 → 강해짐.

**산출물 — 스크립트**
- **Data**: `UpgradeDefinition`(SO) — 종류(무기 신규/무기 강화/스탯 업), 효과 값, 아이콘/설명.
- **Gameplay**:
  - `XpGem`(`IPoolable`) — 사망 위치 드랍, `pickupRadius` 내 자석 이동, 흡수 시 XP 부여.
  - `ExperienceSystem` / `PlayerLevel` — XP 누적, 레벨 곡선 임계값, `LevelUpEvent` 발행.
  - `UpgradeSystem` — 레벨업 시 후보 3개 무작위 추출, 선택 효과 적용(스탯/무기).
  - 레벨업 중 `Time.timeScale = 0`(LevelUpState 연동).

**산출물 — 데이터**
- 🟩 업그레이드 8종: 무기 데미지↑·발사속도↑·관통↑·투사체수↑, 이동속도↑·최대HP↑·자석범위↑·체력재생.

**테스트**
- `XpGemTests`(PlayMode) — 반경 내 자석·흡수·XP 부여.
- `ExperienceTests`(EditMode) — 임계값 도달 시 레벨업·잉여 XP 이월.
- `UpgradeSystemTests`(EditMode) — 3택 추출·효과 적용·중복 방지.

**검증 게이트:** 젬 수집 → 레벨업 → 업그레이드 선택 → 수치 반영 확인 · 테스트 통과.

**결정 포인트:**
- ❓ **레벨 곡선 / 업그레이드 구성** — 게임 디자인(재미) 분기. 기본값 제시 후 확정.

---

## 마일스톤 6 — HUD + 5분 생존

**목표:** UI로 상태 표시, 5분 생존 승리 / 사망 패배, 게임 상태기계 완성.

**산출물 — 스크립트/UI (UI Toolkit)**
- `HudController` + UXML/USS — HP 바, XP 바, 생존 타이머, 킬 수, 레벨.
- `LevelUpScreen` — 업그레이드 3택 카드(일시정지), 선택 처리.
- `GameOverScreen` / `VictoryScreen`.
- `GameTimer` — 경과 시간, 5분 도달 시 `VictoryEvent`.
- `GameStateMachine` 완성 — Play↔LevelUp(정지), Play→GameOver(HP 0)/Victory(5분).
- 🟩 (폴리시) 풀링된 월드스페이스 데미지 숫자.

**테스트**
- `GameTimerTests`(EditMode) — 시간 누적·5분 승리 트리거.
- `GameFlowTests`(PlayMode) — HP 0 패배, 레벨업 정지/재개.
- `HudBindingTests`(PlayMode) — HP/XP/타이머 값 바인딩.

**검증 게이트:** 시작→플레이→레벨업→생존/사망까지 **한 판 완결**(스크린샷·플레이스루) · 테스트 통과.

**결정 포인트:**
- 🟩 UI 비주얼은 기능 우선(플레이스홀더), 폴리시는 후속.

---

## 착수 전 확정할 결정 (✅ 확정 완료)

자율(goal) 실행 시작 게이트. 전부 확정됨 — 상세는 [06. 자율 실행 헌장](06-autonomy-charter.md).

1. ✅ 카메라 시점 (M2) — **쿼터뷰 45°**
2. ✅ Cinemachine 도입 (M2) — **예**
3. ✅ 이동 방식 (M2) — **Rigidbody(키네마틱)**
4. ✅ 성능 목표 (M3) — **60fps @200체**
5. ✅ VFX Graph 추가 (M4) — **예**
6. ✅ 업그레이드 구성 (M5) — **8종 기본값으로 시작**, 데이터로 밸런싱

## 현재 위치
- ✅ 설계·기술 조사·문서화 완료
- ✅ 6단계 상세 실행 스펙 작성 완료
- ✅ 자율 실행 헌장 + 착수 결정 6건 확정
- ✅ **M1 기반 골격 완료** — asmdef(Core/Systems/Tests) · EventBus · ServiceLocator · GameStateMachine · PrefabPool · GameBootstrap · Bootstrap 씬 · URP Deferred+/GPU Resident Drawer. 테스트 10/10 통과, 컴파일·런타임 에러 0.
- ✅ **M2 플레이어 완료** — Data(PlayerDefinition)·Gameplay asmdef 신설, InputReader(New Input System), PlayerController(Rigidbody 이동, 순수 PlayerMovement 분리), Health/HealthState(IDamageable), Cinemachine 쿼터뷰 CameraRig, Player 프리팹·Game 씬. 테스트 22/22(EditMode 18+PlayMode 4), WASD 이동·카메라 추종 실측.
- ✅ **M3 적+스폰+풀링 완료** — EnemyDefinition·WaveDefinition(SO), SpatialHashGrid(Systems), EnemySteering(순수 seek+분리), Enemy(IPoolable·IDamageable), EnemyRegistry, EnemySpawner(풀링), EnemySimulation(중앙 grid+steering+접촉데미지), 적3종·Wave·Enemy 프리팹·씬 배선. 테스트 30/30(EditMode 25+PlayMode 5). **성능: 210체 @193fps(5.2ms)** — 60fps 게이트 통과.
- ✅ **M4 자동 전투 완료** — WeaponDefinition(SO), DamageSystem(Systems), Targeting(순수), Projectile(IPoolable·트리거 데미지·관통), WeaponController(자동발사·최근접), VfxService(VFX Graph 풀+Awaitable), EnemyDiedEvent. VFX Graph 패키지 추가·템플릿(Simple_Burst) 복사. 테스트 37/37(EditMode 31+PlayMode 6). 런타임 검증: 사망→VFX·이벤트, 투사체→치명상.
- ✅ **M5 진행 루프 완료** — ExperienceState(순수 곡선), RunStats(런타임 스탯), UpgradePicker(순수 3택), ProgressionSystem, UpgradeDefinition 8종, XpGem(자석)·GemSpawner(EnemyDiedEvent 구독). PlayerController/WeaponController가 RunStats 소비하도록 리팩터(업그레이드로 강해짐). 테스트 47/47.
- ✅ **M6 HUD + 5분 생존 완료** — Game.UI asmdef, SurvivalClock(순수), GameDirector(상태기계 Playing/LevelUp/GameOver/Victory·시간정지·킬카운트·레벨업 큐), HudController(UI Toolkit 코드구성: HP/XP/타이머/킬/레벨 + 레벨업 3택 카드 + 승리/패배 패널), 기본 테마+PanelSettings+UIDocument 배선. ProgressionSystem 자동적용 제거(UI 선택으로 대체). 테스트 50/50(EditMode 42+PlayMode 8). 통합 검증: HUD 표시·레벨업 정지/선택/재개·승리·게임오버 전부 실측.

## 🎉 GOAL 달성
M1~M6 전 마일스톤 완료. 시작→플레이→자동전투→레벨업(3택)→생존/사망까지 한 판이 완결되는 플레이 가능한 프로토타입 완성. 전체 테스트 50/50, 200체 @193fps.

## 확장 작업 (GOAL 이후)
- ✅ 버그수정: 투사체를 대상 적 높이에서 발사(키 작은 적 명중)
- ✅ 버그수정: 적 접촉 데미지 미적용(`_playerHealth` 해석 누락) 해결 + 회귀 테스트
- ✅ as-built 기획서 `docs/08` 추가
- ✅ **M7 적 이동 고도화 — Flow Field 네비게이션** (NavMesh 미사용). `FlowField`(순수, 비용장 BFS+8방향 흐름) + `FlowFieldController`(영역·장애물·재계산) + `NavObstacle`. EnemySimulation의 seek를 흐름장 샘플로 대체(장애물 우회, 폴백 직선). 테스트 56/56(EditMode 46+PlayMode 10). 흐름장 우회 검증 완료.
- ✅ **M7+ SDF 벽 회피** — 거리장(장애물 BFS)+그래디언트로 부드러운 벽 반발 조향 + 침투 밀어내기(물리 없이). 격자 40×40·셀1로 정밀화. 디버그 기즈모. 테스트 59(EditMode 49+PlayMode 10), 벽 안 8마리 전원 축출 런타임 검증.
- ⏳ 다음 후보: 흐름장 위 steering 강화(alignment), Jobs/Burst 확장, 카드 UI 폴리시, 무기/웨이브 콘텐츠

## 작업 흐름 규칙
- 커밋 메시지 **한국어** (`AGENTS.md`)
- 커밋 정책: **검증 통과 마일스톤마다 자동 커밋** ([06. 자율 실행 헌장](06-autonomy-charter.md))
- 코드는 [04. 코딩 컨벤션](04-coding-conventions.md) 준수
