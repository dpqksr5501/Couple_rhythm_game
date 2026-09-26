# 인형 재고 Firestore 연동 인계서

> 저장소: `seojunyun8/Couple_rhythm_game`  
> 문서 기준일: 2026-09-26  
> 대상: 데이터베이스·백엔드 담당자  
> 목적: 커플 리듬게임에서 실제 지급한 인형을 Firestore 재고에서 안전하게 차감하고, 남은 수량과 지급 이력을 운영자가 확인할 수 있게 한다.

## 1. 먼저 확인할 핵심 사항

- 현재 프로젝트는 **Google Cloud Firestore REST API**를 사용한다.
- Firebase 프로젝트 ID는 현재 `ludens-booth26-2`로 설정되어 있다.
- Unity는 결제 확인 시 플레이 수와 매출을 기록하고, 게임 종료 시 점수를 저장한다.
- `GameState/stats`에 인형 수량 필드는 이미 있지만, **실제 Unity 게임에는 재고 차감 기능이 아직 연결되어 있지 않다.**
- 점수 달성 순간이 아니라 운영자가 인형을 실제로 건넨 뒤 `지급 완료`를 누를 때 재고를 차감해야 한다.
- 중복 클릭·네트워크 재시도·여러 기기의 동시 지급에도 수량이 정확해야 하므로, 차감은 Firestore 트랜잭션 또는 보호된 백엔드 함수에서 처리해야 한다.

## 2. 현재 Firebase 연결 정보

| 항목 | 현재 값 |
|---|---|
| Firebase Project ID | `ludens-booth26-2` |
| Database | Firestore `(default)` |
| REST 기본 경로 | `https://firestore.googleapis.com/v1/projects/ludens-booth26-2/databases/(default)/documents` |
| Unity 설정 파일 | `Assets/Resources/FirebaseConfig.json` |
| 환경 변수 | `FIREBASE_PROJECT_ID` |
| Unity 연동 코드 | `Assets/CoupleRhythm/Scripts/RhythmFirebaseService.cs` |
| 환경 변수 로더 | `Assets/CoupleRhythm/Scripts/EnvLoader.cs` |

Firebase 프로젝트 ID는 비밀번호가 아니지만, 서비스 계정 키·관리자 토큰·비공개 API 키는 이 문서나 Git 저장소에 올리지 않는다.

## 3. 현재 Firestore 데이터 구조

### `GameState/stats`

부스 전체 집계와 현재 인형 수를 한 문서에 저장한다.

| 필드 | Firestore 형식 | 현재 의미 | 초기값 |
|---|---|---|---:|
| `totalDolls` | integer | 일반 인형 남은 수량 | 100 |
| `totalLegendaryDolls` | integer | 프리미엄 인형 남은 수량 | 10 |
| `totalRevenue` | integer | 누적 매출(원) | 0 |
| `totalRegistrations` | integer | 참가자 등록 수 | 0 |
| `totalPlays` | integer | 누적 게임 수 | 0 |
| `totalSuccesses` | integer | 누적 상품 지급 성공 수 | 0 |

현재 초기화 코드는 `admin_tools/reset_booth_data.py`와 `admin_tools/seed_dummy_participants.py`에 있다.

### `RhythmLeaderboard/{autoId}`

Unity가 게임 종료 시 생성하는 점수 기록이다.

| 필드 | Firestore 형식 | 설명 |
|---|---|---|
| `songId` | string | `redred`, `its_me`, `lemonade`, `rude` 중 하나 |
| `songTitle` | string | 화면에 표시되는 곡 제목 |
| `accuracy` | double | 0.0~100.0 |
| `maxCombo` | integer | 최대 콤보 |
| `perfectCount` | integer | PERFECT 수 |
| `goodCount` | integer | GOOD 수 |
| `missCount` | integer | MISS 수 |
| `wrongPressCount` | integer | 잘못 누른 횟수 |
| `timestamp` | string | 현재 UTC 문자열. 신규 데이터는 Firestore timestamp 형식 권장 |

### `Participants/{participantId}`

다른 부스 기능에서 사용하는 참가자 데이터다. 재고 연동 시 이 컬렉션을 수정할 필요는 없다.

## 4. 현재 Unity 동작과 보상 기준

### 이미 연결된 동작

1. 결제 확인 버튼 클릭
   - `RecordGameStart(1000)` 호출
   - `GameState/stats.totalPlays`를 1 증가
   - `GameState/stats.totalRevenue`를 1,000원 증가
2. 곡 종료
   - `SubmitScore(...)` 호출
   - `RhythmLeaderboard`에 결과 문서 추가

### 보상 기준

| 결과 | 실제 게임 코드 조건 | 권장 재고 필드 |
|---|---|---|
| 프리미엄 상품 | 정확도 `100.0%` | `totalLegendaryDolls` |
| 일반 상품 | 정확도 `90.0% 이상`, `100.0% 미만` | `totalDolls` |
| 보상 없음 | 정확도 `90.0% 미만` | 차감 없음 |

> 기존 `Assets/CoupleRhythm/README.md`에는 80% 이상이라고 적혀 있으나, 현재 `CoupleRhythmGame.cs`의 실제 기준은 90%이다. 이 문서는 실행 코드 기준인 90%를 사용한다.

위 재고 매핑은 현재 두 수량 필드의 이름을 바탕으로 한 권장안이다. `totalDolls`가 프리미엄을 포함한 전체 인형 수인지, 일반 인형만 뜻하는지는 운영 담당자가 반드시 확정해야 한다.

## 5. 현재 구현에서 보완해야 할 문제

### Unity 게임에서 인형이 차감되지 않음

`totalDolls` 차감 코드는 테스트용 `admin_tools/simulate_game_play.py`에만 있으며 실제 게임 결과 화면에서는 호출되지 않는다. 따라서 현재 상태로 운영하면 점수와 플레이 수는 쌓이지만 남은 인형 수는 자동으로 줄지 않는다.

### 읽은 뒤 덮어쓰는 방식은 동시성에 취약함

현재 플레이 수와 테스트용 인형 수 갱신은 다음 순서다.

1. 현재 값 GET
2. 로컬에서 `+1` 또는 `-1`
3. 계산한 값을 PATCH

두 기기가 동시에 같은 값을 읽으면 한쪽 변경이 사라질 수 있다. 예를 들어 재고 10을 두 기기가 동시에 읽고 각각 9를 저장하면 실제로 두 개를 지급했어도 DB에는 9가 남는다.

### 중복 지급 방지 키와 지급 이력이 없음

- 동일 게임에서 버튼을 두 번 눌렀는지 판단할 세션 ID가 없다.
- 타임아웃 후 재시도할 때 최초 요청이 성공했는지 확인할 수 없다.
- 누가 언제 어떤 등급의 인형을 지급했는지 원장이 남지 않는다.

### 인증 방식 확인 필요

현재 Unity REST 요청에는 `Authorization` 헤더나 Firebase App Check가 없다. Firestore 규칙이 공개 쓰기를 허용하면 외부에서 점수·매출·재고를 변경할 수 있다. 운영 전에 보안 규칙과 인증 방식을 반드시 확정해야 한다.

## 6. 권장 신규 데이터 구조

기존 `GameState/stats`는 요약 수량으로 유지하고, 게임 세션과 지급 이력을 별도 컬렉션에 추가한다.

### `GameSessions/{sessionId}`

`sessionId`는 게임 시작 시 Unity가 생성한 UUID를 문서 ID로 사용한다. 같은 요청을 재전송해도 같은 문서를 가리키므로 중복 게임 생성이 줄어든다.

| 필드 | 형식 | 설명 |
|---|---|---|
| `machineId` | string | 설치 기기 고유값, 예: `COUPLE-GAME-01` |
| `songId` | string | 선택한 곡 ID |
| `status` | string | `STARTED`, `COMPLETED`, `CANCELLED` |
| `accuracy` | double | 최종 정확도 |
| `perfectCount` | integer | PERFECT 수 |
| `goodCount` | integer | GOOD 수 |
| `missCount` | integer | MISS 수 |
| `wrongPressCount` | integer | 잘못 누른 횟수 |
| `maxCombo` | integer | 최대 콤보 |
| `eligibleTier` | string | `NONE`, `NORMAL`, `PREMIUM` |
| `startedAt` | timestamp | 서버 시각 |
| `completedAt` | timestamp | 서버 시각 |
| `redemptionId` | string/null | 지급 완료 후 연결되는 지급 기록 ID |

### `PrizeRedemptions/{sessionId}`

문서 ID를 `sessionId`와 동일하게 사용한다. 이 구조 자체가 한 게임당 한 번만 지급하도록 돕는다.

| 필드 | 형식 | 설명 |
|---|---|---|
| `sessionId` | string | 게임 세션 ID |
| `machineId` | string | 지급 기기 |
| `prizeTier` | string | `NORMAL` 또는 `PREMIUM` |
| `quantity` | integer | 현재 정책에서는 항상 1 |
| `stockField` | string | 차감한 필드명 |
| `remainingAfter` | integer | 지급 직후 잔여 수량 |
| `idempotencyKey` | string | 재시도 중복 차감 방지용 UUID |
| `operatorId` | string/null | 관리자 로그인을 도입할 경우 사용 |
| `createdAt` | timestamp | 서버 시각 |

예시:

```json
{
  "sessionId": "e82c3bb9-8f60-4db7-a0fd-f52b55ecf629",
  "machineId": "COUPLE-GAME-01",
  "prizeTier": "NORMAL",
  "quantity": 1,
  "stockField": "totalDolls",
  "remainingAfter": 42,
  "idempotencyKey": "030fe716-48f4-428b-ab6d-9c205eef41d2",
  "createdAt": "SERVER_TIMESTAMP"
}
```

### 상품 종류가 늘어나는 경우

인형이 일반/프리미엄 두 종류보다 많아지면 `GameState/stats`의 개별 필드를 계속 추가하지 말고 아래 구조로 분리한다.

```text
PrizeInventory/{sku}
  name: string
  tier: NORMAL | PREMIUM
  stockQuantity: integer
  lowStockThreshold: integer
  isActive: boolean
  updatedAt: timestamp
```

## 7. 권장 지급 처리 흐름

1. 결제 확인 후 게임 시작
   - Unity가 UUID `sessionId` 생성
   - `GameSessions/{sessionId}`를 `STARTED`로 생성
2. 게임 종료
   - 결과 저장
   - 정확도를 기준으로 서버가 `eligibleTier` 계산
3. 결과 화면
   - 지급 가능 등급과 현재 재고 표시
   - 재고가 0이면 품절 표시
4. 실제 상품 전달
   - 운영자가 `지급 완료` 클릭
   - Unity가 지급 API를 한 번 호출
5. 백엔드 트랜잭션
   - 기존 `PrizeRedemptions/{sessionId}` 존재 여부 확인
   - 세션이 `COMPLETED`인지 확인
   - 요청한 등급이 `eligibleTier`와 같은지 확인
   - 해당 재고가 1 이상인지 확인
   - 재고 `-1`, `totalSuccesses +1`, 지급 기록 생성을 한 트랜잭션으로 커밋
6. 응답
   - Unity가 `remainingAfter`를 화면에 표시

점수 달성 즉시 자동 차감하지 않는 이유는 사용자가 상품을 받지 않고 떠나거나, 품절로 지급하지 못하거나, 운영자가 지급을 보류하는 상황을 실제 재고와 일치시키기 위해서다.

## 8. 지급 API 계약 제안

Unity가 Firestore 수량 문서를 직접 GET/PATCH하지 않고, Cloud Functions 또는 별도 백엔드의 보호된 엔드포인트를 호출하는 방식을 권장한다.

`POST /redeemPrize`

요청 헤더:

```text
Authorization: Bearer {machine_token_or_firebase_id_token}
Idempotency-Key: 030fe716-48f4-428b-ab6d-9c205eef41d2
Content-Type: application/json
```

요청 본문:

```json
{
  "sessionId": "e82c3bb9-8f60-4db7-a0fd-f52b55ecf629",
  "machineId": "COUPLE-GAME-01",
  "prizeTier": "NORMAL",
  "quantity": 1
}
```

성공 응답:

```json
{
  "status": "REDEEMED",
  "redemptionId": "e82c3bb9-8f60-4db7-a0fd-f52b55ecf629",
  "prizeTier": "NORMAL",
  "quantity": 1,
  "remainingAfter": 42,
  "redeemedAt": "2026-09-26T08:32:20Z"
}
```

오류 응답 예시:

```json
{
  "status": "REJECTED",
  "code": "OUT_OF_STOCK",
  "message": "지급 가능한 일반 인형 재고가 없습니다."
}
```

| HTTP | `code` | 의미/Unity 처리 |
|---|---|---|
| 400 | `INVALID_REQUEST` | 입력 오류, 지급 버튼 비활성화 |
| 401/403 | `UNAUTHORIZED` | 기기 인증 확인 필요 |
| 404 | `SESSION_NOT_FOUND` | 세션 저장 상태 재확인 |
| 409 | `OUT_OF_STOCK` | 품절 표시, 인형 지급 금지 |
| 409 | `ALREADY_REDEEMED` | 기존 지급 결과 반환, 추가 차감 금지 |
| 409 | `TIER_MISMATCH` | 점수 등급과 상품 등급 불일치 |
| 422 | `NOT_ELIGIBLE` | 보상 기준 미달 |
| 500/503 | `SERVER_ERROR` | 같은 `Idempotency-Key`로 재시도 |

타임아웃 후에는 새 키를 만들지 않고 같은 `Idempotency-Key`로 재시도한다. 백엔드는 이미 성공한 요청이면 최초 성공 결과를 그대로 반환해야 한다.

## 9. Firestore 트랜잭션 요구사항

지급 함수는 다음 작업을 **하나의 Firestore 트랜잭션**으로 실행해야 한다.

1. `GameSessions/{sessionId}` 읽기
2. `PrizeRedemptions/{sessionId}` 읽기
3. `GameState/stats` 읽기
4. 세션 상태·등급·중복 지급·재고 수량 검증
5. 일반 상품이면 `totalDolls - 1`, 프리미엄이면 `totalLegendaryDolls - 1`
6. `totalSuccesses + 1`
7. `PrizeRedemptions/{sessionId}` 생성
8. `GameSessions/{sessionId}.redemptionId` 갱신

트랜잭션 밖에서 GET 후 PATCH하는 방식이나 단순 `increment(-1)`만 사용하는 방식은 금지한다. 단순 증감만으로는 동시에 마지막 재고를 요청할 때 음수 방지와 중복 지급 검증을 함께 보장할 수 없다.

취소가 필요하면 지급 기록을 삭제하지 말고 `PrizeRedemptions`에 취소 상태·시각을 남기고 별도 트랜잭션으로 재고를 1 복원한다.

## 10. 보안 규칙

- Unity 클라이언트에는 서비스 계정 JSON이나 관리자 자격 증명을 포함하지 않는다.
- `GameState/stats`의 재고 필드는 일반 클라이언트가 직접 수정하지 못하게 한다.
- 재고 차감과 수동 입고는 Cloud Functions/서버 SDK 같은 신뢰할 수 있는 환경에서만 수행한다.
- 점수 등록은 인증된 부스 기기만 허용하는 것이 좋다.
- Firebase App Check 또는 Firebase Authentication과 기기별 식별자를 사용한다.
- 관리자 조회·입고·취소 기능은 별도 관리자 권한을 요구한다.
- 로그에 토큰, Authorization 헤더 또는 참가자 개인정보를 남기지 않는다.

최소 권한 원칙:

| 작업 | Unity 기기 | 관리자 | 백엔드 함수 |
|---|---:|---:|---:|
| 재고 조회 | 허용 | 허용 | 허용 |
| 점수/세션 생성 | 허용 | 조회 | 허용 |
| 재고 직접 수정 | 거부 | 거부 | 허용 |
| 입고/취소 요청 | 거부 | 허용 | 실행 |
| 지급 이력 삭제 | 거부 | 거부 | 거부 |

## 11. 운영자 조회 항목

관리 화면 또는 관리 도구에서 최소한 아래 내용을 확인할 수 있어야 한다.

- 일반 인형과 프리미엄 인형의 현재 수량
- 품절 및 부족 재고 경고
- 일별/기간별 게임 수, 매출, 상품 지급 수
- 게임 세션별 점수와 지급 여부
- 지급 시각, 등급, 기기, 지급 직후 잔여 수량
- 입고·지급·취소·수동 조정 이력

기존 `admin_tools/export_booth_summary.py`는 `GameState/stats`를 읽어 남은 일반 인형 수를 표시한다. 프리미엄 재고와 `PrizeRedemptions` 집계를 함께 출력하도록 확장해야 한다.

## 12. DB·백엔드 담당자가 확정해서 전달할 값

- 일반 인형 초기 수량
- 프리미엄 인형 초기 수량
- `totalDolls`가 일반 전용인지 전체 인형 합계인지
- 정확도 100%일 때 프리미엄만 1개 차감하는지, 전체 수량도 함께 차감하는지
- 부족 재고 알림 기준
- 지급 함수의 실제 URL
- Unity 기기 인증 방식과 필요한 토큰 발급 절차
- 운영자 입고·취소 방식
- 인터넷 장애 시 지급을 막을지, 오프라인 지급 후 동기화를 허용할지
- Firestore 보안 규칙 배포 담당자

Unity 담당자에게 전달할 최종 설정 예시:

```text
FIREBASE_PROJECT_ID=ludens-booth26-2
MACHINE_ID=COUPLE-GAME-01
PRIZE_API_BASE_URL=https://<region>-ludens-booth26-2.cloudfunctions.net
```

실제 토큰과 비밀값은 `.env`, 안전한 기기 설정 또는 비밀 관리 도구로 전달하고 Git에는 커밋하지 않는다.

## 13. 완료 검증 기준

- 실제 Unity 게임에서 지급 완료 시 올바른 등급의 재고가 1 감소한다.
- 같은 세션으로 요청을 두 번 보내도 재고는 한 번만 감소한다.
- 같은 `Idempotency-Key`로 재시도하면 최초 결과가 반환된다.
- 두 기기가 마지막 인형 1개를 동시에 요청하면 한 요청만 성공한다.
- 재고가 0일 때 요청은 거절되고 수량은 0을 유지한다.
- 90% 미만 세션은 지급할 수 없다.
- 90% 이상 100% 미만은 일반 재고만 차감한다.
- 100%는 운영 정책에서 확정한 프리미엄 재고만 차감한다.
- 지급 성공 직후 Unity 화면과 관리자 조회 결과의 잔여 수량이 같다.
- 모든 지급과 취소를 세션 ID 기준으로 추적할 수 있다.

