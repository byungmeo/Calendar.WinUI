# 캘린더 표준과 외부 서비스 연동 조사

조사 기준일: 2026-10-28 (Asia/Seoul)

범위: 공식 표준 및 공급자 문서 조사. 실제 계정 로그인, API 호출, 가져오기·내보내기 호환성 실험은 수행하지 않았다. 아래 구현 방향은 제안이며 서비스 연결 기능의 구현 완료를 뜻하지 않는다.

**조사 후 확정된 범위:** 외부 연결은 패키지를 사용하는 개발자의 앱·어댑터 책임이다. 본 프로젝트는 RFC 5545의 이벤트 의미를 준수하는 표시 계약에 집중한다. 서비스 클라이언트·인증·동기화와 별도 연동 패키지 제안은 채택하지 않는다. 공급자별 내용은 배경 조사로만 보존하며, 실제 구현 범위는 [구현 계획](implementation-plan.md)을 따른다.

## 1. 결론

iCalendar와 CalDAV를 이해하고 데이터 의미를 보존하면 연동 비용을 줄일 수 있다. 그러나 한 표준을 준수했다고 모든 서비스의 인증·조회·수정·양방향 동기화가 자동으로 해결되지는 않는다.

이 프로젝트의 구현 방향은 다음과 같이 한정한다.

1. Core / WinUI는 서비스에 독립적인 이벤트 표시 계약을 제공한다.
2. iCalendar의 날짜·시간·반복 발생분을 손실 없이 표시 계층에 연결할 수 있게 한다.
3. 반복 전개와 외부 연결은 사용하는 개발자의 앱·어댑터에 맡긴다. 이 프로젝트에서 연동 계층이나 별도 서비스 패키지를 개발하지 않는다.
4. 첫 버전은 공급자 독립적인 표준 의미의 합성 데이터와 일반 어댑터 예제로 표시 계약을 검증한다. 실제 계정 연결은 인수 조건이 아니다.

## 2. 표준별 역할

| 표준 | 담당 영역 | 표준만으로 해결되지 않는 것 |
| --- | --- | --- |
| [iCalendar / RFC 5545](https://www.rfc-editor.org/rfc/rfc5545.html) | 이벤트 등의 데이터 교환 형식. 일반적으로 .ics 파일에 사용 | 계정 접근, 변경 조회, 실시간 동기화 |
| [CalDAV / RFC 4791](https://www.rfc-editor.org/rfc/rfc4791.html) | WebDAV를 확장한 원격 캘린더 접근·관리 | 공급자별 인증·구현 차이와 미지원 기능 |
| [iTIP / RFC 5546](https://www.rfc-editor.org/rfc/rfc5546.html) | 초대·응답·변경·취소 등 일정 조정 의미 | 인증과 전송, 앱의 편집·초대 UI |
| [JSCalendar / RFC 8984](https://www.rfc-editor.org/rfc/rfc8984.html) | JSON 기반의 별도 캘린더 데이터 모델 | Google / Graph의 REST 스키마를 자동 통일하지 않음 |

JSCalendar는 단순히 iCalendar를 JSON으로 바꾼 jCal과도 다르다. 이 프로젝트의 준수 기준은 RFC 5545의 이벤트 의미로 한정한다. 다른 프로토콜·데이터 형식은 이 문서의 배경 조사다.

ICS 가져오기는 데이터 전송이고, URL 구독은 데이터 읽기 경로다. 원본 서비스의 수정·삭제와 충돌 해결까지 수행하는 양방향 동기화와 구분해야 한다.

## 3. 주요 서비스의 실제 연결 경로

| 서비스 | 공식 문서에서 확인한 경로 | 프로젝트에 대한 의미 |
| --- | --- | --- |
| Google Calendar | [Calendar API](https://developers.google.com/workspace/calendar/api/guides/overview), [CalDAV](https://developers.google.com/workspace/calendar/caldav/v2/guide) | REST JSON을 표시 모델에 매핑 가능. CalDAV도 OAuth 2.0과 구현 제약을 고려해야 함 |
| Outlook / Microsoft 365 | [Microsoft Graph event](https://learn.microsoft.com/en-us/graph/api/resources/event?view=graph-rest-1.0), [calendarView](https://learn.microsoft.com/en-us/graph/api/calendar-list-calendarview?view=graph-rest-1.0) | Graph 고유 데이터 모델을 변환. calendarView로 범위 내 반복 발생분·예외 조회 가능 |
| Apple Calendar / iCloud | [여러 계정과 CalDAV](https://support.apple.com/en-hk/guide/calendar/icl4308d6701/mac), [외부 앱의 iCloud 접근](https://support.apple.com/en-us/121539) | Calendar 앱 이름만으로 저장소를 정하지 말 것. 실제 iCloud 접근 인증과 연결 조건은 별도 확인 |
| Nextcloud / Thunderbird | [Nextcloud CalDAV 백엔드](https://docs.nextcloud.com/server/stable/admin_manual/groupware/calendar.html), [Thunderbird 연결](https://docs.nextcloud.com/server/stable/user_manual/en/groupware/sync_thunderbird.html) | CalDAV 공통 연결의 유효한 대상 |
| 네이버 캘린더 | [일정 추가 API](https://developers.naver.com/docs/login/calendar-api/calendar-api.md), [공개 API 목록](https://developers.naver.com/docs/common/openapiguide/apilist.md) | iCalendar 문자열을 받는 OAuth 기반 추가 API. 표준 형식을 받는다는 사실이 조회·양방향 동기화 API 제공을 뜻하지 않음 |

Google의 공식 CalDAV 문서는 일부 WebDAV 메서드와 기능을 지원하지 않는다고 명시한다. 같은 CalDAV 이름이라도 모든 기능이 동일하다고 가정해서는 안 된다.

Apple은 지원되는 외부 앱의 Apple Account 권한 부여와, 이를 지원하지 않는 앱의 앱 전용 암호 경로를 안내한다. 모든 연동이 같은 인증 방식을 사용한다거나 일반적인 Sign in with Apple만으로 접근 가능하다고 가정하지 않는다. 실제 접근 경로는 연결을 구현하는 개발자가 검토할 사항이다.

Apple Calendar는 iCloud 외에 Google·Exchange 등의 계정도 표시한다. 앱 이름과 실제 데이터 제공 서비스를 구분해야 한다.

## 4. 표시 계약에 지금 반영할 내용

### 4.1 원본 정의, 발생분, 화면 조각

```text
외부 이벤트 / 반복 시리즈 정의
    ↓ 서비스가 전개하거나 연동 계층이 전개
표시 범위에 해당하는 개별 발생분(Occurrence)
    ↓ 월간·주간·일간별 배치
화면 조각(Segment)
```

- 매주 월요일 회의의 서로 다른 날짜는 서로 다른 발생분이다.
- 월말부터 다음 주까지 이어지는 한 번의 출장은 여러 조각이어도 같은 발생분이다.
- 취소·시간 변경된 한 회차를 원본 시리즈와 구분해야 한다.
- UI 입력은 원본 객체와 선택한 발생분 정보를 함께 전달할 수 있어야 한다.
- 더보기는 그 날짜의 발생분 목록을 반환하고, 시각적 조각을 중복 집계하지 않는다.
- 화면 컨트롤이 RRULE을 무한 전개하거나 네트워크 요청을 시작하지 않는다.

[Google singleEvents](https://developers.google.com/workspace/calendar/api/v3/reference/events/list)와 [Graph calendarView](https://learn.microsoft.com/en-us/graph/api/calendar-list-calendarview?view=graph-rest-1.0)는 서비스가 전개한 발생분을 표시하는 경로다. 서비스가 전개한 결과와 로컬 전개 결과를 동시에 넣어 중복 생성하지 않는다.

### 4.2 식별자는 한 문자열로 통합하지 않기

공급자 ID, 교환용 UID, 반복 시리즈 ID, 발생분의 원래 시작 식별값을 구분한다.

- [Google](https://developers.google.com/workspace/calendar/api/v3/reference/events)의 id와 iCalUID는 다르며 반복 발생분들은 iCalUID를 공유한다. originalStartTime은 발생 시각이 변경된 뒤에도 원래 회차를 식별한다.
- [Graph](https://learn.microsoft.com/en-us/graph/api/resources/event?view=graph-rest-1.0)의 iCalUId는 반복 회차별로 다르다고 명시되어 있다. 기본 id도 항목 이동 시 달라질 수 있고 ImmutableId 요청 옵션이 별도로 있다.

따라서 UID만으로 전 서비스 중복 제거·갱신을 처리하지 않는다. 서비스·계정·캘린더·공급자 리소스 ID를 구분하고, 반복 인스턴스 식별은 해당 공급자의 의미에 따라 매핑한다.

표시용 필수 모델에 모든 메타데이터를 강제하지 않는다. 어댑터 컨텍스트에 선택적으로 연결하고, 실행 알림에서 원본 참조와 발생분 참조를 유지한다. 여러 캘린더에 있는 같은 회의도 컨트롤이 임의로 병합하지 않는다.

### 4.3 시간의 종류와 시간대 정의 보존

종일 날짜, 특정 시점을 나타내는 시간, 특정 시간대의 벽시계 시간, 어느 시간대에서도 같은 시각인 floating 시간을 구분한다.

DateTimeOffset은 이미 해석된 시점 표현에는 유용하지만, 원본 TZID·DST 규칙·floating 여부까지 단독으로 보존하지는 못한다. 반복 전개에 필요한 원본 시간대 정보는 연동 계층에서 유지하고, Core에는 해석된 발생분과 시간 의미를 전달한다.

Google은 IANA 식별자를 사용한다. Graph의 [supportedTimeZones](https://learn.microsoft.com/en-us/graph/api/outlookuser-supportedtimezones?view=graph-rest-1.0)는 Windows / IANA 형식을 구분하며 기본값은 Windows 형식이다. 시간대 변환기는 두 형식과 원본 식별자를 구분해야 한다.

ICS의 사용자 지정 VTIMEZONE을 무조건 시스템 시간대 ID로 찾을 수 있다고 가정하지 않는다. 해석할 수 없는 정의를 시스템 로컬로 조용히 바꾸지 말고, 연동 계층에서 해결하거나 명시적인 미지원 결과로 처리한다.

### 4.4 종료 없는 이벤트를 일괄 거부하지 않기

[RFC 5545 §3.6.1](https://www.rfc-editor.org/rfc/rfc5545.html#section-3.6.1)의 의미를 연동 계층에서 해석한다.

- 종일 시작만 있고 DTEND / DURATION이 없으면 하루 이벤트로 정규화한다.
- 시간제 시작만 있고 DTEND / DURATION이 없으면 시점 이벤트로 정규화할 수 있어야 한다.
- 명시적인 DTEND는 DTSTART보다 뒤여야 한다. 잘못된 종료 구간을 시점 이벤트로 위장하지 않는다.
- DURATION이 제공된 경우 그 의미를 먼저 해석한다. 필드 누락만 보고 시점 이벤트로 만들지 않는다.

따라서 기존 계획의 End > Start 일괄 제약을 기간 이벤트에 한정하고, 명시적인 시점 이벤트 종류를 추가하는 기본안으로 수정한다. 화면 최소 크기는 데이터의 실제 기간을 늘려서 만들지 않는다.

### 4.5 표시 모델로 원본을 재작성하지 않기

표시에 필요 없는 참석자·주최자·알림·상태·확장 속성도 원본에서 유지할 수 있어야 한다. 원본 객체를 전달하는 것만으로 가져오기→내보내기의 무손실성이 자동 보장되지는 않는다.

ICS 파서·작성기는 미지원 속성 보존 정책과 지원 범위를 명시해야 한다. 서비스 수정은 해당 API의 PATCH / 업데이트 계약으로 수행하고, UI가 가진 최소 필드만으로 원본 전체를 덮어쓰지 않는다. 읽기 전용 캘린더와 발생분·시리즈 편집 권한은 앱 또는 연동 계층이 판단한다.

## 5. 인증과 동기화는 별도 책임

| 문제 | 처리 계층 |
| --- | --- |
| OAuth, 계정 승인, 토큰 보관·갱신 | 앱 / 공급자 연동 계층 |
| 범위 조회, 페이지 순회, 요청 제한, 재시도 | 공급자 클라이언트 |
| 반복 전개·예외 적용 | 공급자 또는 ICS 연동 계층 |
| 변경·삭제 병합, 동기화 상태, 충돌 해결 | 동기화 계층 |
| 원본 데이터 → 표시 발생분 | 어댑터 |
| 기간 배치·선택·실행 알림 | Core / WinUI |

Google은 [syncToken과 만료 후 재동기화](https://developers.google.com/workspace/calendar/api/guides/sync), Graph는 [calendarView delta](https://learn.microsoft.com/en-us/graph/api/event-delta?view=graph-rest-1.0)를 제공한다. 두 서비스의 토큰·페이지·삭제 의미를 하나로 가정하지 않는다. 이것이 iCalendar 데이터 모델만으로 양방향 동기화가 완성되지 않는 구체적인 이유다.

삭제·취소 응답에는 일반 표시 이벤트의 필드가 없을 수 있다. 이를 잘못된 일정으로 렌더링하지 말고, 동기화 계층에서 기존 발생분의 제거·취소에 적용한 뒤 컨트롤에 전달한다.

## 6. 확정한 패키지 책임 경계

```text
개발자의 앱·어댑터: 계정·조회·동기화·필요 시 반복 전개
    ├─ Google Calendar API
    ├─ Microsoft Graph
    ├─ CalDAV / iCloud / Nextcloud
    └─ ICS 파일 / 구독 데이터
             ↓ 공급자 변환 / 필요 시 반복 전개
        공통 표시 발생분 + 원본 연결
             ↓
        Calendar.Core → Calendar.WinUI
```

### 이 프로젝트에서 준비

- 발생분 계약과 UID / RECURRENCE-ID에 대응하는 선택적 참조.
- 기간 / 시점, 종일 / 시간제, 시간대 투영 경계.
- 날짜·범위 기반 입력과 전체 이벤트를 반환하는 실행 알림.
- RFC 5545의 이벤트 의미를 검증하는 공급자 독립 합성 데이터와 일반 어댑터 예제.
- 원본 모델을 그대로 유지한 사용자 지정 표시 검증.

### 이 프로젝트 범위에서 제외

- 외부 서비스의 API / CalDAV 연결, 인증과 동기화.
- 공급자 전용 어댑터와 별도 연동 패키지 제작.
- 실제 계정 연결·서비스별 기능 호환성 검증.

외부 데이터는 개발자가 기존 EventAdapter 계약으로 연결한다. RFC 5545 전체 컴포넌트나 .ics 파서·파일 입출력은 이번 범위 결정만으로 추가하지 않는다. 실제 지원하는 VEVENT 의미와 제약을 공개 문서에 명시한다.

## 7. 구현 전 인수 기준에 추가할 사례

- 다일 종일, 자정 종료, DST 전후, floating 시간, 사용자 지정 시간대.
- 종료 없는 하루 / 시점 이벤트와 잘못된 명시 종료의 구분.
- 반복 한 회차의 이동·취소, 같은 UID를 가진 서로 다른 발생분.
- 같은 UID 아래 서로 다른 발생분과 앱이 전달한 원본 참조의 구분.
- 같은 발생분의 주별 조각과 더보기 개수의 일치.
- 원본 객체와 발생분 참조가 실행 알림에서 유지되는지.
- 파싱 미지원 정보가 임의로 소실·변환되지 않는지. 실제 round-trip 검증은 ICS 기능 도입 시 수행.

문서 표현도 구분한다. UI만 구현한 상태에서 RFC 전체 준수나 모든 서비스 연동을 표방하지 않는다. 실제 검증한 이벤트 의미, 파일 기능, 공급자 기능을 각각 명시한다.
