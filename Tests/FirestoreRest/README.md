# 리듬게임 Firestore REST 요청 검사

`dotnet run --project Tests/FirestoreRest/FirestoreRestChecks.csproj`는 실제
`RhythmFirebaseService.cs`의 결제 확인, 상품 재고 차감, 랭킹 기록 요청을
Unity 모의 객체로 실행해 JSON 형식을 검사합니다.

실제 Firebase 인증·규칙, Unity 직렬화, 네트워크 장애와 Windows 빌드를
검사하지 않습니다. 별도 테스트 Firebase 프로젝트에서 실연동 검증이 필요합니다.
