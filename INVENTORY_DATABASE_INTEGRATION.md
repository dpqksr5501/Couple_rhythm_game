# 커플 리듬게임 공유 재고 연동 (2026-09-26)

**운영 빌드 전 실서버·Unity 검증 필요.** Firebase Web API Key와 스태프 계정, 배포된 보안 규칙이 없는 상태에서는 로그인이 동작하지 않습니다.

- 커플 리듬은 인스타 ID를 저장하지 않습니다.
- 결제 확인 버튼은 기존 1,000원 기록을 `GameRounds/rhythm_{roundId}` 영수증과 함께 한 번만 저장합니다. 같은 버튼 연타와 응답 유실 재시도는 같은 회차 ID를 사용합니다.
- 곡 완료 성적은 `RhythmLeaderboard/{roundId}`에 저장해 중복 행을 막습니다. 통신이 끊겼으면 결과 화면에서 운영진이 `Ctrl+Alt+S`로 같은 회차의 점수 저장을 재시도할 수 있습니다.
- 정확도 90~99.9%는 일반 `totalDolls`, 100%는 `totalLegendaryDolls` 재고를 사용합니다.
- 결과 화면에서 `boothAdmin` 운영진이 `Ctrl+Alt+P`를 누르면 현재 재고를 확인하고 `InventoryChanges/rhythm_{roundId}` 영수증, 공유 재고 차감, 성공 횟수 증가를 단일 Firestore `commit`으로 확정합니다. **재고 확정 후 실물 상품을 건네세요.**
- 품절·통신 오류로 확정할 수 없으면 자동 지급을 보류하고 운영진이 회차 ID로 수기 대조합니다. 하위 상품 대체는 하지 않습니다.
- 재고 수량과 기본 플레이/매출 카운터는 `GameState/stats`에 운영진이 실제 값으로 초기화합니다. 인형 100개를 코드가 임의로 생성하지 않습니다.
- REST 요청은 Firebase 이메일 로그인으로 받은 ID 토큰을 사용합니다. `firestore.rules`는 `boothStaff` 클레임 계정만 허용하지만 아직 서버 배포 여부를 확인하지 못했습니다.

## 운영 전에 할 일

1. 별도 Firebase 테스트 프로젝트에서 이메일/비밀번호 로그인, 스태프 계정, `boothStaff` 클레임을 준비합니다. `admin_tools/grant_staff_claim.py`는 기본 미리보기이며 `--apply`에서만 변경합니다.
2. `FIREBASE_PROJECT_ID`와 `FIREBASE_WEB_API_KEY`를 각 기기의 `.env`에 설정합니다. 비밀번호와 관리용 ADC 파일은 저장소에 넣지 않습니다.
3. `GameState/stats` 6개 정수 필드를 실제 재고로 만듭니다.
4. 테스트 프로젝트에 `firestore.rules`와 `firestore.indexes.json`을 배포하고 인증 없는 조회·쓰기·쿼리·commit이 거부되는지 확인합니다.
5. Windows 빌드에서 90%/100%, 재고 1개에 동시 2건, 버튼 연타, 응답 유실, Wi-Fi 끊김을 확인합니다. 기록과 실제 지급 수량이 같아야 합니다.
6. 검증을 마친 뒤 운영 프로젝트에 규칙을 배포하고 공개 조회가 차단됐는지 다시 확인합니다.

참고: `admin_tools`의 관리용 REST 요청은 Google ADC를 사용합니다. 위험한 시드·시뮬레이션의 운영 DB 직접 쓰기는 차단했습니다.
