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

## 마일스톤 8 — 빌드 다양성 (무기 다종 · 패시브 · 진화)

> Phase 2의 첫 마일스톤. 프로토타입(루프 검증)을 "한 판이 매번 다른" 게임으로 끌어올리는 장르 핵심 작업. **데이터 주도 필러**(`docs/01`) 위에서 진행 — 새 시스템을 뚫는 게 아니라 깔아둔 SO 구조를 확장.

**목표:** 매 판 다른 빌드가 나온다. 여러 무기를 동시에 보유·레벨업하고, 패시브 아이템으로 전체를 강화하며, 특정 *무기+패시브* 조합이 **진화 무기**로 합쳐진다. 레벨업 3택이 "무기 신규 / 무기 레벨업 / 패시브 신규 / 패시브 레벨업" **동적 풀**에서 뽑힌다.

**왜 지금 이게 먼저인가:** 현재 `RunStats`가 무기 스탯을 **전역 단일**로 보유 → "무기 1개 = 플레이어 스탯". 빌드 선택지가 0이라, 무기를 인스턴스 단위로 분리해야 비로소 "빌드"가 성립한다.

**핵심 아키텍처 변경**
- 무기 스탯을 전역에서 **무기별 런타임 인스턴스**로 분리:
  - `WeaponInstance`(순수) — 보유 무기 1개 = 정의 참조 + 현재 레벨 + 자체 발사 타이머. **유효 스탯 = 기본값(SO) × 레벨 성장 × 전역 패시브 수정자**(VS 모델).
  - `WeaponInventory`(Gameplay) — 보유 무기 리스트(🟩 최대 6). 각 인스턴스 타이머를 굴려 발사. 기존 `WeaponController`(단일 무기) → 인벤토리 N무기 발사로 일반화.
- `WeaponDefinition` 확장 — `Id`·표시명·아이콘·설명, **레벨 성장 테이블**(레벨별 델타), `fireMode`(최근접 투사체 / 장판 오라 / 다중 스프레드 / 전방 관통 라인 …), **진화 링크**(`evolvesInto`, `requiredPassive`).
- `PassiveItemDefinition`(신규 SO) — 전역 수정자 1종 = 종류(Might 데미지% / Cooldown 연사% / Area 크기% / Amount 투사체+ / ProjectileSpeed% / Magnet / MaxHP / MoveSpeed / Regen) + 레벨 성장(🟩 최대 5).
- `RunStats` 역할 재정의 — 무기 스탯 제거, **플레이어 기본 스탯(이동/HP/자석) + 전역 패시브 수정자 집계**만. 무기 유효 스탯은 `WeaponInstance`가 계산.
- **업그레이드 풀 동적화** — 평면 `UpgradeDefinition[]` → 레벨업마다 런타임 후보 생성: 무기 신규(여유 시)·보유 무기 레벨업(<max)·패시브 신규(여유 시)·보유 패시브 레벨업(<max)·(풀 고갈 시) 회복 폴백. `UpgradeOption`(런타임: 종류+대상+결과레벨)을 `UpgradePicker`로 중복 없이 3택(기존 순수 로직 재사용).
- **진화** — 보유 무기 만렙 + 요구 패시브 만렙 충족 시 레벨업 카드에 진화 옵션 등장 → 기본 무기를 진화 무기로 교체.

**산출물 — 스크립트**
- Data: `WeaponDefinition`(확장)·`PassiveItemDefinition`(신규)·`WeaponFireMode`(enum)·진화 필드.
- Gameplay: `WeaponInstance`·`BuildState`(보유 목록+후보 생성, **순수·테스트 대상**)·`WeaponInventory`·`PassiveInventory`·`UpgradeOption`/`UpgradeOptionKind`·진화 판정(순수). `WeaponController`→인벤토리 발사 일반화 + 발사 모드별 로직(투사체/오라/스프레드/라인).
- UI: 레벨업 카드에 아이콘·현재 레벨·진화 표시(손맛 마일스톤과 접점).

**산출물 — 데이터 (🟩 시작 콘텐츠, 수치는 데이터 밸런싱)**
- 무기 5종 · 패시브 6종 · 진화 5종 — 상세는 아래 "M8 무기 로스터(구체안)".

**테스트 (순수 로직 우선)**
- `WeaponInstanceTests` — 정의×레벨×수정자 유효 스탯·만렙 클램프.
- `BuildStateTests` — 후보 생성(여유/만렙/고갈 분기)·인벤토리 cap·중복 방지.
- `EvolutionTests` — 조건 판정·교체 결과.
- `WeaponInventoryTests`(PlayMode) — N무기 동시 발사·풀 재사용.

**검증 게이트:** 한 판에서 무기 2종+ 동시 발사·패시브 강화·진화 1회 실측(스크린샷/플레이스루) · **200체 60fps 유지** · 테스트 그린.

**결정 포인트:**
- ❓ 동시 무기/패시브 상한(🟩 6/6) · 무기 만렙(🟩 8) · 패시브 만렙(🟩 5)
- ❓ 진화 방식 — 카드형(🟩) vs VS식 보스 상자형(후속)
- 🟩 시작 콘텐츠 수치는 기본값 → 데이터로 밸런싱

### M8 무기 로스터 (구체안)

설계 원칙: ① 5종이 **조준 방식·형태·주 스케일 축**에서 서로 겹치지 않는다 ② 전부 기존 인프라(Projectile 풀 · `ObstacleField` 벽충돌/LOS · `SpatialHashGrid` 반경질의 · `DamageSystem`)로 구현 가능 ③ 각 진화가 **서로 다른 공격 패시브**를 요구 → 패시브 선택이 빌드를 가른다. (수치는 🟩 시작값, 데이터 밸런싱 대상)

| # | 무기 | 역할 | fireMode | 조준/형태 | 주 스케일 축 |
|---|------|------|----------|-----------|-------------|
| 1 | **마력 볼트** MagicBolt (기존) | 기본 단일 타겟 | `NearestProjectile` | 사거리 내 최근접 자동조준 투사체 | 데미지·관통·투사체수 |
| 2 | **서리 오라** FrostAura | 근접 군중정리+둔화 | `AuraField` | 플레이어 중심 원형 지속 틱 | 반경·틱데미지 |
| 3 | **산탄** Scatter | 전방 광역 | `SpreadProjectile` | 이동방향 부채꼴 다발(조준X) | 투사체수·부채각 |
| 4 | **관통 창** Lance | 라인 관통·zoning | `PierceLine` | 바라보는 방향 직선 관통 | 길이·폭 |
| 5 | **궤도 위성** Orbit | 자동 방어 근접 | `Orbital` | 주위 회전체 접촉(상시) | 위성수·반경 |

**무기별 상세 (기본값 / 레벨 1→8 성장 / 진화)**

1. **마력 볼트** — 데미지 5 · 간격 0.6s · 속도 14 · 관통 0 · 사거리 12. 성장: 데미지↑·관통+·투사체+(연속발사)·간격↓. **진화 + Amount(만렙) → "볼트 스톰"**: 한 번에 다발 발사 + 약한 유도.
2. **서리 오라** — 틱 데미지 3 · 틱 간격 0.5s · 반경 3 · 둔화 20%. 성장: 반경↑·틱데미지↑·틱간격↓·둔화↑. (둔화는 PBD 군중 속도를 낮춰 3D 연출/시너지). **진화 + Area(만렙) → "블리자드"**: 반경·데미지 대폭 + 짧은 빙결.
3. **산탄** — 데미지 4 · 간격 0.9s · 투사체 3 · 부채각 40° · 사거리 8. 성장: 투사체+·부채각↑·데미지↑·사거리↑. **진화 + ProjectileSpeed(만렙) → "플랙"**: 명중 시 소폭 폭발(AoE).
4. **관통 창** — 데미지 8 · 간격 1.1s · 길이 10 · 폭 1 · 라인 관통. 성장: 길이↑·폭↑·데미지↑·간격↓. **진화 + Might(만렙) → "임페일러"**: 전 화면 길이 + 처치 시 폭발.
5. **궤도 위성** — 접촉 데미지 6 · 위성 1 · 반경 2.5 · 회전 180°/s. 성장: 위성+·반경↑·회전속도↑·데미지↑. **진화 + Cooldown(만렙) → "헤일로"**: 위성 다수 + 거대 반경.

**패시브 6종 (진화 게이트 매핑)**

| 패시브 | 효과 | 게이트하는 진화 |
|--------|------|----------------|
| Might | 전 무기 데미지 +% | 임페일러 |
| Cooldown | 전 무기 발사간격 −% | 헤일로 |
| Area | 오라/폭발/창 크기 +% | 블리자드 |
| Amount | 투사체 +개 | 볼트 스톰 |
| ProjectileSpeed | 투사체 속도 +% | 플랙 |
| Magnet *(또는 MaxHP)* | 자석 반경 +% | — (순수 유틸) |

**fireMode별 구현 메모**
- `NearestProjectile`·`SpreadProjectile`(+플랙 폭발) → 기존 `Projectile` 풀·벽충돌·LOS 그대로 재사용.
- `PierceLine` → 얇은 박스 오버랩(또는 초고속·무한관통 투사체), 벽에서 차단.
- `AuraField` → 투사체 없음. `SpatialHashGrid` 반경 질의로 틱 데미지(풀 불필요).
- `Orbital` → 풀 없이 플레이어 자식 회전 트랜스폼 + 접촉 트리거(상시 존재).

**구현 순서 (M8 내부 단계)**
- **8a 시스템 + 코어 3종:** 아키텍처 전환(WeaponInstance/Inventory/동적 풀/패시브) + 마력볼트·서리오라·산탄 + 진화 1종(볼트 스톰)으로 시스템 검증.
- **8b 확장:** 관통 창·궤도 위성 + 나머지 진화 4종.

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
- ✅ **아트 1차** — 플레이어 비주얼을 Crystal 스킨드 캐릭터 + Mecanim 애니메이션(Idle↔Walk 블렌드, Generic 멀티-FBX 아바타 공유)으로 교체. 상세는 `docs/08` 4절.
- ✅ **M8 빌드 다양성 완료(8a+8b)** — `WeaponInstance`/`BuildState`/`UpgradePool`, `WeaponController` 인벤토리(5 fireMode: Nearest/Spread/Aura/PierceLine/Orbital), 동적 업그레이드 풀(무기/패시브/진화 혼합). 콘텐츠: 기본 무기 5종(볼트/오라/산탄/창/궤도)+진화 5종(볼트스톰/블리자드/플랙/임페일러/헤일로)+패시브 6종. 테스트 90/90, 플레이 실측(다무기 발사·동적 카드·예외 0).
- ✅ **무기 VFX 완료** — 발사 형태별 4시각 언어(투사체 색/트레일·오라 디스크·관통 빔·궤도 오브)+무기별 색/스케일, `WeaponVfx`. 커밋 30ff87d.
- ✅ **손맛(피격 연출) 1차 완료** — 적 피격 플래시+스케일 펀치, 떠오르는 데미지 숫자(`DamageNumberLayer`), 임팩트 스파크(`ImpactVfx`), 플레이어 피격 비네트(HUD `DangerMeter`). 순수 로직 `Core.Feedback`(`HitReactState`/`DangerMeter`/`FloatingTextAnim`)+테스트. `EnemyHitEvent` 도입. 테스트 103/103, 플레이 실측. 상세 `docs/08` 9절.
- ⏳ 다음 후보: SFX/사운드(아직 0), 카드 아이콘/리롤/보유현황 UI, 화면 흔들림(Cinemachine 임펄스), 난도 곡선+보스/엘리트(Leaf), 메타 progression+캐릭터 선택

## 작업 흐름 규칙
- 커밋 메시지 **한국어** (`AGENTS.md`)
- 커밋 정책: **검증 통과 마일스톤마다 자동 커밋** ([06. 자율 실행 헌장](06-autonomy-charter.md))
- 코드는 [04. 코딩 컨벤션](04-coding-conventions.md) 준수
