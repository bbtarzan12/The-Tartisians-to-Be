# 04. 코딩 컨벤션

레거시 Unity C# 방식을 배제하고 **모던 Unity 6 방식**으로 작성한다. 이 문서는 실수(특히 컴파일 불가 문법)를 막기 위한 가드레일이다.

## ⚠️ 언어 버전: Unity 6.2 = C# 9.0 까지만

공식 매뉴얼 확인 — Unity 6.2의 기본 C# 언어 버전은 **9.0**이다. C# 10/11 기능을 쓰면 **컴파일 에러**가 난다.

### ❌ 사용 금지 (C# 10/11 또는 Unity 미지원)

| 금지 | 이유 | 대신 |
|------|------|------|
| `namespace Foo;` (파일 스코프 namespace) | C# 10 | `namespace Foo { }` 블록 스코프 |
| `global using` | C# 10 | 각 파일에 `using` 명시 |
| `record struct` | C# 10 | 일반 `struct` |
| `required` 멤버 | C# 11 | 생성자 강제 |
| `init` 세터 | Unity가 `IsExternalInit` 미제공 | 일반 세터 / 생성자 |
| `record` (직렬화 타입) | Unity 직렬화 미지원 | 일반 `class` |
| raw string `"""..."""`, 리스트 패턴 | C# 11 | 일반 문자열, switch 식 |

### ✅ 적극 사용 (C# 9)

- target-typed `new()` — `Dictionary<int, Enemy> map = new();`
- switch 식, 향상된 패턴 매칭
- 람다 (단, 핫패스에서 클로저 할당 주의)

## 레거시 → 모던 매핑

| ❌ 레거시 | ✅ 모던 Unity 6 |
|-----------|----------------|
| `Input.GetKey` / `Input.GetAxis` (구 Input Manager) | **New Input System** (InputAction + 생성 C# 래퍼) |
| 코루틴 `yield return new WaitForSeconds()` | **Awaitable** + `destroyCancellationToken` |
| 직접 만든 풀 / `Instantiate`+`Destroy` 반복 | **`UnityEngine.Pool.ObjectPool<T>`** 빌트인 래핑 |
| `GameObject.Find` / `SendMessage` / `BroadcastMessage` | SerializeField 참조 · EventBus · ServiceLocator |
| `GetComponent` + null 체크 | `TryGetComponent(out var x)` |
| `FindObjectOfType` (deprecated) | `FindAnyObjectByType` / `FindFirstObjectByType` |
| `FindObjectsOfType` (deprecated) | `FindObjectsByType(..., FindObjectsSortMode.None)` |
| 핫패스 `Camera.main` (내부 Find) | 참조 캐싱 |
| `obj.tag == "Enemy"` | `obj.CompareTag("Enemy")` |
| `public` 필드 (인스펙터 노출용) | `[SerializeField] private` |
| 문자열 Animator 파라미터 매 프레임 | `Animator.StringToHash` 캐싱 |
| `OnGUI` / IMGUI 런타임 UI | UI Toolkit |
| 곳곳에 싱글톤 | ServiceLocator · ScriptableObject 이벤트채널 · `RuntimeInitializeOnLoadMethod` |

## Awaitable 주의사항

- Awaitable 인스턴스는 **풀링**된다 → **한 번만 await 가능**. 두 번 await 금지.
- MonoBehaviour의 `destroyCancellationToken`을 넘겨 객체 파괴 시 자동 취소 → 코루틴의 누수 문제 회피.
- 고정 주기 반복은 새 Awaitable 남발 대신 루프 + `await Awaitable.NextFrameAsync(token)` 패턴.

## 핫패스 할당 제로 규칙

`Update` / `FixedUpdate` / 충돌 콜백 등 매 프레임·다수 호출 경로에서:

- `new` 컬렉션·배열 생성 금지 (사전 할당·재사용)
- LINQ 금지 (지연 평가 + 할당)
- 람다 클로저 캡처 주의 (할당 유발)
- `GetComponent` 반복 금지 (캐싱 또는 `TryGetComponent` 1회)
- 박싱 유발 코드 금지 (struct를 object로)

## 일반 스타일

- namespace 루트: `Tartisians` (예: `Tartisians.Core`, `Tartisians.Gameplay`)
- 필드: `private` + `[SerializeField]`, 카멜케이스 `_camelCase`(private) 는 프로젝트 합의에 따름
- 공개 표면 최소화 — `internal`/`private` 우선, 어셈블리 경계 존중
- 한 파일 = 한 주요 타입 (블록 namespace 안)

## 참고 자료

- [git-amend 유튜브 채널](https://www.youtube.com/@git-amend) — 모던 Unity 아키텍처(EventBus, ServiceLocator, FSM, Awaitable, 풀링)
- Unity Manual — [C# compiler and language version](https://docs.unity3d.com/6000.2/Documentation/Manual/csharp-compiler.html)
- Unity Manual — [Asynchronous programming with Awaitable](https://docs.unity3d.com/6000.4/Documentation/Manual/async-awaitable-introduction.html)
- Unity Manual — [UnityEngine.Pool / Object pooling](https://learn.unity.com/course/design-patterns-unity-6)
