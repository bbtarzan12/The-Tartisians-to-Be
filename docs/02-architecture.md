# 02. 아키텍처

## 설계 원칙

- **단방향 의존성** — 어셈블리(asmdef) 단위로 컴파일을 쪼개 의존성 방향을 강제한다. 상위 레이어만 하위를 참조하고, 역방향 참조는 컴파일 단계에서 차단된다.
- **데이터 주도** — 게임 콘텐츠(적·무기·업그레이드·웨이브)는 코드가 아닌 ScriptableObject로 정의한다.
- **느슨한 결합** — 시스템 간 통신은 직접 참조 대신 EventBus와 ServiceLocator로 분리한다.
- **싱글톤 남발 금지** — 전역 상태는 명시적 부트스트랩(`RuntimeInitializeOnLoadMethod`)과 ServiceLocator로 관리한다.

## 레이어 / 어셈블리 구조

```
Assets/_Project/
├─ Scripts/
│  ├─ Core/        (Game.Core.asmdef)      무의존. 부트스트랩·EventBus·ServiceLocator·FSM·공용 타입
│  ├─ Data/        (Game.Data.asmdef)      → Core. ScriptableObject 정의(Enemy/Weapon/Upgrade/Wave)
│  ├─ Systems/     (Game.Systems.asmdef)   → Core. PrefabPool·SpatialGrid·DamageSystem·TimeSystem
│  ├─ Gameplay/    (Game.Gameplay.asmdef)  → Core,Data,Systems. Player·Enemy·Weapon·Pickup·Spawner
│  ├─ UI/          (Game.UI.asmdef)        → Core,Gameplay. HUD·LevelUp 선택창 (UI Toolkit)
│  └─ Tests/       (Game.Tests.asmdef)     → 전체 + Test Framework. EditMode/PlayMode 테스트
├─ Data/        적/무기/업그레이드/웨이브 .asset 인스턴스(밸런스 표)
├─ Prefabs/
├─ Scenes/      Bootstrap, Game
├─ Art/
└─ Audio/
```

### 의존성 방향

```
            ┌────────┐
            │  Core  │  (무의존)
            └────────┘
              ▲  ▲  ▲
       ┌──────┘  │  └───────┐
   ┌───────┐ ┌────────┐     │
   │ Data  │ │Systems │     │
   └───────┘ └────────┘     │
       ▲          ▲         │
       └────┬─────┘         │
        ┌────────┐          │
        │Gameplay│──────────┘
        └────────┘
            ▲
        ┌──────┐
        │  UI  │
        └──────┘

   Tests → (전체 참조)
```

## 핵심 시스템 맵

```
┌─ Core ───────────────────────────────────────────────┐
│  GameBootstrap → GameStateMachine(Menu/Play/LevelUp/   │
│  GameOver) · EventBus · ServiceLocator · 고정 타임스텝   │
└────────────────────────────────────────────────────────┘
       │
┌─ Player ─────────┐   ┌─ Enemy (대량) ───────────────┐
│ InputReader      │   │ EnemySpawner (웨이브/시간기반) │
│ Movement (URP)   │   │ Enemy (MonoBehaviour, 풀링)   │
│ Stats / Health   │   │ → seek(플레이어) + 분리(밀집)  │
│ XP / Level       │   │ SpatialGrid 등록              │
└──────────────────┘   └───────────────────────────────┘
       │                          │
┌─ Weapon (자동전투) ┐   ┌─ Combat ────────────────────┐
│ WeaponInventory   │──▶│ DamageSystem                │
│ 자동 발사 타이머    │   │ SpatialGrid 질의로 히트 판정  │
│ ProjectilePool    │   │ 데미지 → 사망 → VFX·XP젬 스폰  │
└───────────────────┘   └─────────────────────────────┘
       │
┌─ Progression ─────┐   ┌─ UI (UI Toolkit) ───────────┐
│ 레벨업 → 업그레이드 │──▶│ HUD(HP/XP/타이머/킬)          │
│ 3택 카드 (시간정지) │   │ LevelUpScreen                │
└───────────────────┘   └─────────────────────────────┘
```

## 데이터 주도 정의 (ScriptableObject)

| SO | 필드(예) |
|----|---------|
| `EnemyDefinition` | 체력, 이동속도, 접촉 데미지, 메시/머티리얼, XP 보상 |
| `WeaponDefinition` | 발사 간격, 데미지, 투사체, 관통, 범위, 레벨별 성장 |
| `UpgradeDefinition` | 무기 신규/강화 + 스탯 업(이속·회복·자석 범위 등) |
| `WaveDefinition` | 시간대별 스폰 적 종류·밀도·보스 |

밸런싱이 전부 `.asset` 파일에서 일어나므로, MCP 하네스로 수치를 조정하고 플레이모드로 즉시 검증하는 **자율 밸런싱 루프**가 가능하다.

## 채택 패턴 (모던 Unity, git-amend 계열)

- **EventBus** — `IEvent` 마커 인터페이스 + 제네릭 `EventBus<T>` + `EventBinding<T>`. 타입 안전, 저할당, 시스템 간 디커플링.
- **ServiceLocator** — 전역 서비스(설정·풀 매니저 등) 접근. 싱글톤 난립 대체.
- **State Machine** — 게임 상태(Menu/Play/LevelUp/GameOver) 및 추후 적/플레이어 행동 FSM.
- **Object Pooling** — `UnityEngine.Pool.ObjectPool<T>` 빌트인 위에 GameObject 친화 래퍼(`PrefabPool`). 적·투사체·XP젬·VFX 전부 풀 경유.

> 패턴별 코드 규칙과 금지 사항은 [04. 코딩 컨벤션](04-coding-conventions.md) 참조.
