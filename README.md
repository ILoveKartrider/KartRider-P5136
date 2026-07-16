# KartRider P5136

한국 카트라이더 프로토콜 5136 클라이언트를 위한 소스 전용 서버와 Windows 접속기입니다.
현재 코드는 단일 클라이언트에서 로그인, 메인 메뉴, 마이룸, 방 생성, 맵 변경과 경기 시작까지 확인한 개발 버전입니다.

이 프로젝트는 [yanygm/Launcher_V2](https://github.com/yanygm/Launcher_V2)의 AFL-3.0 소스를 수정한 파생 프로젝트입니다. 원본 Git 이력을 그대로 게시한 포크가 아니라 공개 가능한 소스만 새 이력으로 구성한 저장소입니다.

## 공개 범위

저장소와 공식 릴리스에는 다음 자료를 포함하지 않습니다.

- 원본·언팩·수정 게임 실행 파일과 DLL
- PIN, XML, RHO/RHO5, BML, PK, AES 및 기타 게임 데이터
- 추출한 맵·아이템·모델·텍스처·음원 등의 리소스
- 패킷 캡처와 실제 패킷 trace, 실행 로그, 계정·프로필 데이터
- 메모리·크래시 덤프, IDA/Ghidra/x64dbg 프로젝트
- 디컴파일·디스어셈블 출력, 주소 맵과 내부 분석 자료

클라이언트는 제공하지 않습니다. 사용자는 적법하게 보유한 지원 설치본을 직접 준비해야 합니다.

## 요구 사항

- 빌드: .NET 8 SDK
- 실행: Windows x64용 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- 지원 대상: 한국 카트라이더 프로토콜 5136 설치본

공식 바이너리는 framework-dependent입니다. .NET 런타임을 실행 파일에 포함하지 않아 서버와 접속기 EXE가 각각 10MiB를 넘지 않도록 게시 단계에서 검사합니다.

## 빌드

PowerShell에서 저장소 루트를 기준으로 실행합니다.

```powershell
./scripts/Build.ps1
./scripts/Publish.ps1
```

`Build.ps1`은 공개 금지 자료와 로컬 비밀정보를 먼저 검사한 후 두 프로젝트를 빌드하고 KRData 압축·암호화 네 가지 조합을 왕복 검증합니다. `Publish.ps1`은 새 출력 디렉터리에 framework-dependent win-x64 패키지와 SHA-256 목록을 만듭니다.

```text
artifacts/publish/server/
artifacts/publish/connector/
artifacts/release/
```

## 사용

1. 릴리스의 서버와 접속기 EXE를 본인이 보유한 P5136 `KartRider.exe`가 있는 폴더에 둡니다.
2. 서버를 먼저 실행합니다.
3. 접속기에서 서버 IPv4 주소, 기본 포트와 사용자명을 설정한 뒤 게임을 실행합니다.
4. 로컬 또는 신뢰하는 격리 LAN에서만 사용합니다.

서버와 접속기는 실행 중 `Profile/`, `logs/`와 설정 파일을 로컬에 만들 수 있습니다. 이 파일에는 사용자명, 주소 또는 인증 패킷이 기록될 수 있으므로 이슈나 릴리스에 첨부하지 마세요.

## 지원 상태

자세한 구현·검증 범위와 알려진 제한은 [FEATURES.md](FEATURES.md)를 확인하세요. 특히 2인 이상 실주행 동기화와 랜덤 맵 후보 선택은 아직 멀티클라이언트 검증 전입니다.

## 보안과 라이선스

이 구형 프로토콜에는 현대적인 인증·기밀성·접속 제한이 없습니다. 서버를 공인 인터넷에 직접 노출하지 마세요.

소스는 Academic Free License 3.0으로 배포됩니다. 출처와 수정 고지는 [NOTICE.md](NOTICE.md), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), 콘텐츠 정책은 [LEGAL.md](LEGAL.md)를 확인하세요.
