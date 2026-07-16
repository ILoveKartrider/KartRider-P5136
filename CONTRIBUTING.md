# Contributing

모든 변경은 소스 코드와 재현 가능한 합성 테스트 자료만으로 구성해야 합니다.

```powershell
./scripts/Test-SourceBoundary.ps1
./scripts/Build.ps1
```

다음 자료는 이름을 바꾸거나 압축해도 제출할 수 없습니다.

- 게임 실행 파일, PIN/XML, RHO/RHO5/BML/PK/AES와 추출 리소스
- 패킷 캡처·실제 trace, 메모리·크래시 덤프와 실행 로그
- IDA/Ghidra/x64dbg 프로젝트, 디컴파일·디스어셈블 출력과 주소 맵
- 실제 계정·프로필 DB, 사용자명, IP, 개인 경로, 토큰과 키
- 라이선스를 확인할 수 없는 외부 저장소의 소스 또는 바이너리

새 프로토콜 동작은 고수준 설명, 직접 작성한 serializer와 최소 합성
fixture로 재현하세요. 외부 코드를 가져오거나 수정했다면 먼저 라이선스를
확인하고 `THIRD_PARTY_NOTICES.md`에 출처, 버전과 변경 범위를 기록해야 합니다.

호환성 변경에는 공개 동작, 실패 모드, 서버·접속기 양쪽 영향과 검증 범위를
함께 설명해 주세요.
