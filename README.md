# ERP (Enterprise Resource Planning)

WPF 기반 ERP 데스크톱 앱과 ASP.NET Core AuthApi를 함께 구성한 포트폴리오 프로젝트입니다.
로그인, 회원가입 이메일 인증, 권한 기반 메뉴 제어, 품목/재고/사용자 관리, 감사 로그처럼 ERP에서 자주 필요한 흐름을 실행 가능한 형태로 구현했습니다.

## 기술 스택

- 언어: C#
- Framework: .NET 8
- UI: WPF
- Backend: ASP.NET Core Minimal API
- ORM: Entity Framework Core
- DB: PostgreSQL
- Test: xUnit
- Infra: Docker, GitHub Actions

## 주요 기능

- 로그인 및 권한 기반 메뉴 제어
- 회원가입 이메일 인증
  - 인증번호 유효 시간 3분
  - 3회 연속 발송 후 5분 쿨다운
  - SMTP 환경변수 기반 설정
- ID 중복 확인
- 품목 관리
- 재고 입고, 출고, 조정
- 로트/시리얼 추적
- 사용자 승인 및 권한 관리
- 감사 로그 기록

## 프로젝트 구조

```text
Erp.Domain          핵심 도메인 엔티티
Erp.Application     DTO, 인터페이스, 권한 코드, 애플리케이션 계층
Erp.Infrastructure  EF Core, PostgreSQL, SMTP, 인증/권한 서비스 구현
Erp.AuthApi         이메일 인증, 회원가입, ID 중복 확인 API
Erp.Desktop         WPF 데스크톱 앱
Erp.Tests           xUnit 테스트
```

## 릴리즈 실행

GitHub Release의 `erp-v1.0.3-win-x64.zip` 파일에는 `AuthApi`와 `Desktop` 실행 파일이 함께 포함됩니다.

실행 순서:

1. PostgreSQL 실행
2. 환경변수 설정
3. `AuthApi/Erp.AuthApi.exe` 실행
4. `Desktop/Erp.Desktop.exe` 실행

릴리즈 파일은 self-contained 방식으로 빌드되어 별도 .NET Runtime 설치 없이 실행할 수 있습니다.

## 보안 및 운영 참고

- 비밀번호는 PBKDF2 기반 해시로 저장합니다.
- SMTP 계정, DB 비밀번호, seed 비밀번호는 코드에 저장하지 않고 환경변수로 주입합니다.
- 권한은 Role/Permission 기반으로 관리합니다.
- 주요 사용자 처리와 재고 처리 이력은 AuditLog로 기록합니다.
- 현재 Desktop 앱은 포트폴리오 실행 편의를 위해 일부 기능에서 DB에 직접 접근합니다.
  운영 환경에서는 Desktop이 DB에 직접 접근하지 않고 AuthApi를 통해 업무 기능을 처리하는 구조가 권장됩니다.

## 향후 개선 사항

- Desktop 업무 기능의 AuthApi 이전
- JWT 기반 인증 고도화
- 통합 테스트 확대
- Release 자동화
