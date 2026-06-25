# 03. 기술 스택

## 엔진 / 패키지

| 항목 | 버전·선택 |
|------|----------|
| Unity | `6000.5.1f1` (Unity 6.2) |
| 렌더 파이프라인 | URP 17.5 — **Deferred+** 경로 |
| 입력 | New Input System 1.19 |
| 카메라 | Cinemachine — **쿼터뷰 45°** 추종 |
| 플레이어 이동 | Rigidbody(키네마틱) 물리 이동 |
| 성능 목표 | **60fps @ 동시 적 200체** |
| 테스트 | Unity Test Framework 1.7 (6.2부터 코어 패키지) |
| AI 하네스 | Unity-MCP (IvanMurzak) — `localhost:23714` |

## 렌더링: 왜 Deferred+ 인가

PC 타깃 + 다수 동적 광원/이펙트 환경이라 디퍼드 셰이딩을 원한다. 그러나 **렌더링 경로 선택이 대량 적 인스턴싱과 충돌**할 수 있어 다음과 같이 결정했다.

| 경로 | 다수 광원 | GPU Resident Drawer | MSAA | 판정 |
|------|----------|---------------------|------|------|
| Forward+ | ✅ 클러스터드 | ✅ | ✅ | 가능 |
| **Deferred+** (6.1+) | ✅✅ 디퍼드+클러스터 | ✅ | ❌ | **채택** |
| Deferred (클래식) | ✅ | ❌ **인스턴싱 손실** | ❌ | ⛔ 회피 |

- **클래식 Deferred는 GPU Resident Drawer와 비호환** → 적 200체가 인스턴싱 없이 그려져 드로우콜이 폭발한다. 절대 사용하지 않는다.
- **Deferred+** (Unity 6.1+ 도입, 클러스터드 디퍼드)는 디퍼드의 다광원 이점과 GPU Resident Drawer 인스턴싱을 **둘 다** 가져간다.

> 주의: 공식 6.2 매뉴얼 일부 페이지는 GPU Resident Drawer 요구사항을 "Forward+ only"로 단순 표기한다. 실제로는 Forward+/Deferred+ 모두 호환(클래식 Deferred만 비호환). URP 에셋 설정 시 에디터에서 실측 검증한다.

### 렌더링 설정 (URP 에셋)

- 렌더링 경로: **Deferred+**
- **GPU Resident Drawer**: Instanced Drawing
- **GPU Occlusion Culling**: ON (안 보이는 적 컬링 → 오버드로 감소)
- **SRP Batcher**: ON
- Project Settings → **BatchRendererGroup Variants = Keep All**
- 요구: 컴퓨트 셰이더 지원 그래픽 API (PC 데스크톱 GPU 충족)

## 적 애니메이션: 단순 메시 + 셰이더

**GPU Resident Drawer는 `MeshRenderer`만 인스턴싱 배칭하고, `SkinnedMeshRenderer`(뼈대 스키닝)는 배칭하지 못한다.** 따라서 적 200체를 일반 스키닝 애니메이션으로 만들면 인스턴싱 이득이 사라진다.

| 방식 | 인스턴싱 | 판정 |
|------|---------|------|
| **단순 메시 + 셰이더 움직임** | ✅ | **채택** — 콩콩·회전·스쿼시를 셰이더로 |
| VAT(버텍스 애니메이션 텍스처) | ✅ | 보류 — 진짜 애니 필요 시 옵션 |
| 일반 스키닝(Animator+SkinnedMesh) | ❌ | ⛔ 200체에선 회피 |

> 아트 에셋이 아직 없어, 당분간 **프리미티브(캡슐/큐브) 플레이스홀더**를 `MeshRenderer`로 사용한다. 둘 다 `MeshRenderer`라 처음부터 인스턴싱 친화 구조를 유지한다. 셰이더 모션은 추후 적용.

## 성능 전략

### 진짜 병목은 적 이동이 아니다

200체 규모에서 적 이동 루프는 병목이 아니다. 프레임을 깎는 실제 원인:

| 병목 | 대책 |
|------|------|
| **VFX 스폰 비용** (사망 시 파티클 Instantiate/Destroy) | ⭐ VFX 풀링 + **VFX Graph(GPU 파티클)** |
| 인스턴스화 스파이크 + GC (적·투사체·젬 생성/파괴) | 전부 Object Pool 경유 |
| 프레임당 힙 할당 (new·LINQ·람다·GetComponent) | 사전 캐싱, 핫패스 할당 0 |
| 충돌 판정 | 이 규모면 Unity 물리 트리거로 충분(키네마틱+레이어 매트릭스 최소화). 필요 시 SpatialGrid 직접 질의 |

### 엔지니어링 우선순위

1. **모든 스폰 객체 풀링** (적·투사체·XP젬·VFX) — 1순위
2. **핫패스 할당 제로** — Update/물리 경로에서 GC 유발 금지
3. GPU Resident Drawer로 렌더 CPU 비용 이전

## 규모 → 기술 결정

동시 적 100~200체는 **성능 관점에서 작은 숫자**다. 따라서:

- **DOTS/ECS** — 5만+ 체용. 과설계. 미사용.
- **Jobs + Burst** — 8천 체급. 과함. 미사용. (이동 루프가 병목이 아니므로 불필요)
- **평범한 GameObject + 풀링** — 정답. 디버깅·MCP 친화적, 반복 빠름.

## Unity 6.2 신기능 취사선택

| 기능 | 판정 | 사유 |
|------|------|------|
| GPU Resident Drawer + Occlusion Culling | 🟢 채택 | 동일 적 메시 인스턴싱 → CPU 렌더 비용 대폭 절감 |
| VFX Graph (GPU 파티클) | 🟢 채택 | 다수 VFX를 GPU에서 시뮬 → 적 수 무관 저비용 |
| UI Toolkit (World Space UI 포함) | 🟢 채택 | HUD·레벨업창 (데미지 숫자만 풀링 메시로 별도) |
| Awaitable | 🟢 채택 | 코루틴 대체, GC 감소 (타이머·흐름 제어) |
| Unity Test Framework (코어) | 🟢 채택 | EditMode/PlayMode 자율 테스트 루프 |
| Mesh LOD (6.2 신규) | 🟡 보류 | 적 메시 확정 후 적용 (성능 레버) |
| STP 업스케일링 | 🟡 보류 | GPU 병목 시 켜는 레버 |
| Sentis (AI Inference, 설치됨) | 🔴 미사용 | 런타임 신경망. 프로토타입 불필요(패키지는 유지) |
| Adaptive Performance | 🔴 미사용 | 모바일 발열 관리용. PC 타깃이라 불필요 |
| DOTS / Burst / Jobs | 🔴 미사용 | 200체엔 과설계 |

## 참고 출처

- Unity Manual — [What's new in Unity 6.2](https://docs.unity3d.com/6000.2/Documentation/Manual/WhatsNewUnity62.html)
- Unity Manual — [GPU Resident Drawer (URP)](https://docs.unity3d.com/6000.2/Documentation/Manual/urp/gpu-resident-drawer.html)
- Unity Manual — [Spatial-Temporal Post-processing (STP)](https://docs.unity3d.com/6000.1/Documentation/Manual/urp/stp/stp-upscaler.html)
- Unity Manual — [C# compiler and language version](https://docs.unity3d.com/6000.2/Documentation/Manual/csharp-compiler.html)
