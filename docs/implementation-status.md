# 구현 및 검증 현황

기준일: 2026-11-01 (Asia/Seoul). 초기 월간 컨트롤 구현이며 공개 NuGet 게시는 하지 않았다. 아래 기존 검증 기록은 각 실행 시점의 결과이며 최신 API 변경은 [API usability improvements](api-usability-improvements.md)를 참조한다.

## 구현한 기능

- WinUI와 무관한 Core 날짜 범위·이벤트 의미·표시 발생분 모델.
- 종일 / 절대 시점 / floating 시간, 명시적 기간 / 종료 없는 시점 이벤트.
- 표시 월과 주 시작 요일에 따른 5주/6주 자동 계산, 최소 5주. 주 수 고정 옵션은 제공하지 않는다.
- 주별 기간 조각과 이어짐, 안정 정렬을 보존하는 겹침 배치, 주 단위 넘침과 날짜별 전체 목록.
- 템플릿 컨트롤과 기본 스타일·영어/한국어 문자열, 사용자 데이터 어댑터.
- 날짜 실행·선택 변경·이벤트 실행·더보기·범위 변경 알림.
- 날짜와 이벤트 선택은 기본 ON이며 독립적으로 설정한다. 이벤트 선택은 `IsEventSelectionEnabled`, 이벤트 실행 알림은 `IsEventInvocationEnabled`로 각각 제어한다.
- 이벤트는 `SelectedEvent` / `SelectedEventChanged`로 선택 상태를 유지하고, 포커스와 독립적인 2 DIP 외곽선을 모든 표시 조각에 적용한다. 호버·누름 글자색을 이벤트 전용 정책으로 통일하고 기본 툴팁은 제공하지 않는다.
- 날짜 선택 외곽선은 호버·누름과 독립적인 레이어로 고정한다. 이벤트 누름은 길이와 관계없이 80ms 배경색 전환만 사용하며 크기·위치를 변형하지 않는다.
- ObservableCollection / INotifyPropertyChanged / 명시적 갱신, 디스패처 변경 병합·항목별 투영 재사용·구독 차분 갱신·`DeferRefresh()`.
- 방향키로 현재 선택 날짜에서 좌우 하루·상하 일주일씩 즉시 선택하고 SelectedDateChanged를 알린다. 날짜 셀의 별도 포커스 테두리는 숨긴다. 방향키는 DateInvoked를 보내지 않고, 선택 OFF에서는 이동하지 않는다.
- 원본 연결, 인접 월 여부, 사용자 템플릿 내부 독립 버튼.
- `samples/Calendar.Example` packaged 예제 앱이 ProjectReference 없이 로컬 NuGet 패키지를 소비한다. API 검증은 같은 앱의 선택적 검증 모드에 통합했다.
- 예시 앱의 UTC 오프셋·언어 선택. 기본값은 모두 시스템이며, 언어는 시스템 / 한국어 / English를 제공한다. 컨트롤의 `DisplayLanguage`와 `DisplayTimeZone`으로 연결하고 전역 문화권은 변경하지 않는다.
- SDK 고정과 작업 폴더용 부트스트랩, 코어·UI·패키지 검증 스크립트.

공통 이벤트 템플릿 컨텍스트에는 월간 레인·열 타입을 넣지 않았다. 주간·일간은 미래 구현이며 현재 DisplayUnit은 Month만 지원한다.

## 검증 결과

| 구분 | 결과 |
| --- | --- |
| SDK | 작업 폴더의 .NET SDK 10.0.403 사용 |
| WinUI | Windows App SDK 2.6.4, WinUIAnalyzer 0.7.1 적용 |
| 빌드 | 솔루션 전체 및 예제 앱 x64 빌드 성공, 경고 0 / 오류 0 |
| 공통 코어 | 29개 사례를 net8.0 / net9.0 / net10.0에서 각각 통과 |
| 실제 UI | 로컬 packaged 예제에서 23개 시나리오 통과. 날짜·이벤트 선택과 실행 분리, 방향키·월 이동·데이터 갱신·테마·툴팁 미표시 포함. 추가 리사이즈 7개 및 언어·시간대 6개 통과 |
| 추가 마우스 검증 | 기간 막대의 날짜 경계 클릭 시 29일을 전달하고 날짜 실행 없이 이벤트 실행만 1회 발생하는 것을 실제 화면에서 확인 |
| 시각 검토 | 자동 5주/6주, 선택, 사용자 템플릿, Dark, 작은 창 PNG 확인 |
| 로컬 패키지 소비 | .NET 8.0.33 / 9.0.22 / 10.0.14에서 실제 실행 및 날짜·이벤트·더보기 확인 |
| 패키지 자원 | 단일 nupkg의 WinUI·Core DLL/XML, PRI/Generic.xaml 및 두 PDB를 담은 심벌 패키지 확인. 별도 Core 패키지 의존성 없음 |

현재 방향키 즉시 선택 반영 후 UI 결과는 `artifacts/ui-20261029-223504/results.json`이다(22개 통과). 날짜 키보드 선택은 별도 포커스 테두리 없이 선택선 하나로 표시하는 것을 확인했다. 이벤트 선택·호버 및 OFF 상태에서도 Light/Dark 배경·텍스트 가독성을 유지하는 것을 PNG로 확인했다. 앞선 .NET 8/9/10 패키지 소비 결과는 `artifacts/package-smoke-20261029-003855/results.json`이다. 로컬 artifacts는 Git 제외 대상이며 새 체크아웃에서는 스크립트로 재생성한다.

주 수 고정 API 제거 후 로컬 패키지를 다시 생성했고, 새 패키지 캐시로 .NET 10 소비 앱의 빌드·실행 및 날짜·이벤트·더보기를 재검증했다: `artifacts/package-smoke-20261029-214531/results.json`.

이벤트 선택 정책 추가 후 최종 로컬 패키지의 .NET 10 소비 결과는 `artifacts/package-smoke-20261029-215447/results.json`이다. 소비 앱은 날짜·이벤트 선택의 기본 ON과 새 속성 설정도 초기화 시 검사한다.

지속 이벤트 선택과 호버 스타일 자원 추가 후 패키지 소비 결과는 `artifacts/package-smoke-20261029-220823/results.json`이다. `SelectedEvent` 재투영 유지 및 선택 변경 중복 방지도 소비 앱에서 검사한다. 날짜 버튼으로 실제 키보드 포커스를 옮긴 뒤에도 이벤트 선택 외곽선이 남는 추가 검증은 `artifacts/ui-20261029-220731/focus-review.json`에 기록했다.

날짜 선택선 및 이벤트 누름 효과 수정 후 패키지 소비 결과는 `artifacts/package-smoke-20261029-222238/results.json`이다. Light/Dark 날짜 선택선의 호버 전후 가장자리 픽셀 차이는 모두 0으로 확인했다(`scripts/verify-interaction-visuals.ps1`). 긴 이벤트와 짧은 이벤트의 실제 포인터 누름 프레임에서 크기·텍스트 위치가 유지되는 시각 검토는 `artifacts/interaction-press-final-20261029/review.json` 및 같은 폴더의 영상·프레임에 기록했다.

방향키 즉시 날짜 선택 및 날짜 포커스 테두리 제거 후 패키지 소비 결과는 `artifacts/package-smoke-20261029-223613/results.json`이다. UI 결과의 `02b-arrow-selected.png`에서 Enter 없이 선택이 갱신되고 별도 포커스 테두리가 없는 것을 확인했다.

코드 검토에서는 공통 코어의 XAML 비의존성, 템플릿 컨텍스트의 월간 비의존성, 입력 중복 방지, 원본 보존, 구독 해제, 시간대 의미, 테마 자원을 확인했다. 소비 앱은 크기 옵션 객체를 교체해도 표시 범위가 유지되고 월 이동 시 35→42→35 범위와 알림 횟수가 자동 갱신되는지 검사한다. 선택 변경 핸들러에서 월을 이동해도 DateInvoked가 클릭 당시의 인접 여부를 유지하는 사례도 소비 앱에서 검사한다.

## 리사이즈 성능 개선

리사이즈 성능 개선을 반영했다. 날짜·요일·주별 Grid를 유지하고 가로 크기 및 동일 이벤트 표시 용량의 높이 변경에서는 자체 UI 재구성을 생략한다. 표시 용량이 바뀔 때도 날짜 셀과 계속 표시되는 이벤트·더보기 컨트롤을 재사용한다. 이벤트별 리소스 사전 로딩을 제거하고 날짜/이벤트 선택은 변경된 대상만 갱신한다. 테마 전환에서는 구분선 브러시를 갱신한다.

2026-10-29 동일 Release 측정에서 가로 변경의 자체 재구성은 54→0회, 동일 용량의 높이 변경은 49→0회였다. 용량 경계 변경은 53→9회, 갱신 중앙값 25.42→1.437ms, 날짜 버튼 생성 1,855→0개로 줄었다. 전체 프레임/FPS를 측정한 수치는 아니다. 상세 조건과 후속 과제는 `docs/performance-review.md`에 기록했다.

기존 UI 22개와 최종 비계측 앱의 리사이즈 회귀 7개를 통과했고 결과의 PNG를 검토했다: `artifacts/resize-optimization-20261029/ui/results.json`, `artifacts/resize-optimization-20261029/resize-ui/results.json`. 리사이즈 전후 날짜 포커스·독립 이벤트 선택, 넘침/확장, 데이터 변경 후 더보기 전체 목록, Light/Dark/Light 복귀와 방향키 선택을 확인했다. 재현 스크립트는 `scripts/verify-resize.ps1`이다.

최적화한 로컬 NuGet 소비는 새 캐시에서 .NET 8.0.33 실행(`artifacts/package-smoke-20261029-230753/results.json`의 8 대상 PASS)과 .NET 10.0.14 실행(`artifacts/package-smoke-20261101-011912/results.json`)을 확인했다. 첫 실행의 10 대상 빌드 실패는 미완료로 남기지 않고 `ConsumerNetVersion=10.0`으로 빌드 및 전체 소비 검증을 다시 수행해 통과했다. 최종 Release 예제 빌드와 UI 패키지 생성도 경고/오류 없이 완료했다. 임시 성능 계측 코드는 최종 소스와 패키지에 포함하지 않았다.

## 현재 제한과 추가 검증

2026-11-01 언어·시간대 설정을 추가했다. `DisplayLanguage`의 null/빈 문자열은 시스템 UI 문화권, `DisplayTimeZone` 미설정은 시스템 시간대다. 언어 변경 시 요일·날짜·오늘·더보기·선택 안내를 다시 표시하며, 이벤트 원본 제목과 앱이 재정의한 문자열은 보존한다. 예시 앱은 지역 시간대 규칙을 따르는 시스템 선택과 고정 UTC 오프셋 선택(30분/45분 포함)을 제공한다. 오늘 버튼도 선택한 표시 시간대를 따른다.

설정 UI 6개 검증 결과는 `artifacts/locale-settings-20261101/settings-ui-verified/results.json`이다. UTC의 10월 20일 23:30 이벤트가 UTC+09에서 21일로 이동하고, 종일 날짜는 그대로인 것을 확인했다. 최종 화면 검토에서 발견한 ComboBox의 시스템 항목 표시 텍스트 캐시를 수정한 뒤 영어→한국어→시스템 전환을 추가 검증했다: `artifacts/locale-settings-20261101/system-label-check/results.json`. 기존 UI 22개도 통과했다: `artifacts/locale-settings-20261101/regression-ui-verified/results.json`. 각 결과의 PNG를 확인했다.

최신 로컬 NuGet를 새 캐시로 소비하는 .NET 10 앱에서 두 API의 시스템 기본값·명시 설정·시스템 복귀를 초기화 시 검사하고, 실제 날짜·이벤트·더보기 실행까지 통과했다: `artifacts/package-smoke-20261101-021639/results.json`. 예제 Release 빌드와 패키지 생성은 경고/오류 없이 완료했다. 이 변경에서 Core의 시간 의미·정렬 정책은 수정하지 않았다.

- 검증 머신은 Windows 11 x64다. ARM64 / x86 실기, 구형 Windows, 다른 DPI / RTL 조합은 검증하지 않았다. UI 라이브러리는 아키텍처 중립 관리 DLL로 패키징하고 실행 앱이 네이티브 아키텍처를 선택한다.
- 고대비 자원은 제공하지만 실제 Windows 고대비 테마와 화면 읽기 프로그램을 켠 검증은 미실시다. Light / Dark와 UI Automation 이름·포커스는 확인했다.
- Sandbox는 기능이 설치되어 있으나 시작 시 공유 단계의 접근 거부(0x80070005)로 실패했다. 로컬로 대상 변경을 알린 후 검증했으며 Sandbox 성공으로 표기하지 않는다.
- trimming / Native AOT / C++ 소비 지원은 검증하지 않았다.
- RFC 5545의 VEVENT 의미에 맞춘 표시 계약이다. 전체 RFC 파서, .ics 입출력, 반복 전개, 외부 연결·인증·동기화는 포함하지 않는다.
- 정렬과 기간 연결을 지키기 위해 빈 레인이 생길 수 있다. 템플릿 높이는 월간에서 동일 행 높이를 사용한다.
- 공개 배포 전 패키지 ID, 라이선스, API XML 문서의 전체 커버리지, 정식 릴리스 버전·지원 표를 별도로 마무리해야 한다.

## 재현

```powershell
.\scripts\bootstrap.ps1 -IncludeTestRuntimes
./scripts/build.ps1
dotnet test src\Calendar.Core.Tests\Calendar.Core.Tests.csproj
./scripts/build.ps1 -PackagesOnly
.\scripts\verify-package.ps1
```

로컬 nupkg를 같은 버전으로 다시 만들었다면 기존 NuGet 캐시와 혼동하지 않도록 새 작업 폴더 캐시를 지정할 수 있다.

```powershell
.\scripts\verify-package.ps1 -PackageCache C:\Workspace\Calendar.WinUI\artifacts\fresh-package-cache
```

예제 앱은 `winapp run samples/Calendar.Example/Calendar.Example.csproj --arch x64 -p Platform=x64 --detach --json`으로 실행한다. 반환된 최신 PID로 `scripts/ui-tests.ps1`을 실행한다. 실행 중인 이전 예제를 다시 배포할 때는 그 예제 창을 먼저 닫아 파일 잠금을 해제한다.
