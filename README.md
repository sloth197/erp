# ERP (Desktop + Auth API)

WPF 기반 ERP 데스크톱 앱과 회원가입/이메일 인증(Auth API) 프로젝트입니다.

## 주요 기능

- 사용자 로그인 / 권한 기반 메뉴 노출
- 회원가입 시 이메일 인증 코드 발송/검증
  - 인증 코드: 영문 대/소문자 + 숫자 8자리
  - 인증 코드 만료 시간: 3분
- 재고 / 구매 / 판매 / 회계 모듈(일부 구현)

## 개발 환경

- .NET SDK 8.0
- Docker Desktop (Compose 포함)
- PostgreSQL (`localhost:5432`)

## 빠른 시작

1. 환경 변수 파일 준비

```powershell
Copy-Item .env.example .env
```

2. DB 실행

```powershell
docker compose up -d
```

3. 솔루션 빌드

```powershell
dotnet build Erp.sln
```

4. Auth API 실행 (터미널 1)

```powershell
dotnet run --project Erp.AuthApi --launch-profile http
```

5. Desktop 실행 (터미널 2)

```powershell
dotnet run --project Erp.Desktop
```

## 기본 계정

- `admin` / `.env`의 `ERP_SEED_ADMIN_PASSWORD`
- `staff` / `.env`의 `ERP_SEED_STAFF_PASSWORD`

## SMTP 설정

이메일 인증을 사용하려면 `.env` 또는 `Erp.AuthApi/appsettings.Development.json`의 SMTP 값을 실제 정보로 설정하세요.

- `ERP_SMTP_HOST`
- `ERP_SMTP_PORT` (권장: `587`)
- `ERP_SMTP_SECURITY_MODE` (`StartTls`)
- `ERP_SMTP_USERNAME`
- `ERP_SMTP_PASSWORD`
- `ERP_SMTP_FROM`

## 참고

- 앱 시작 시 마이그레이션 및 시드 데이터가 자동 적용됩니다.
- DB 비밀번호 오류(`28P01`)가 나면 `.env` 확인 후 `docker compose down -v`로 DB를 초기화하고 다시 실행하세요.
- Desktop 파일 구조 가이드는 `docs/FOLDER_GUIDE_KO.md`를 참고하세요.
