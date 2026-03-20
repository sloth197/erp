# ERP 파일/폴더 가이드 (한국어)

파일 찾기 쉽게, **기능별 폴더 구조**로 정리한 기준입니다.

## 1) 실행/환경설정

- 루트 설정 파일: `.env`, `.env.example`
- Docker/PostgreSQL: `docker-compose.yml`
- 전체 솔루션: `Erp.sln`
- 데스크톱 실행 프로젝트: `Erp.Desktop/`
- 인증 API 실행 프로젝트: `Erp.AuthApi/`

## 2) Desktop 구조 (기능별)

### Views

- 인증: `Erp.Desktop/Views/Auth/`
- 대시보드: `Erp.Desktop/Views/Dashboard/`
- 재고: `Erp.Desktop/Views/Inventory/`
- 기준정보: `Erp.Desktop/Views/Master/`
- 시스템: `Erp.Desktop/Views/System/`
- 공통: `Erp.Desktop/Views/Common/`

### ViewModels

- 인증: `Erp.Desktop/ViewModels/Auth/`
- 대시보드: `Erp.Desktop/ViewModels/Dashboard/`
- 재고: `Erp.Desktop/ViewModels/Inventory/`
- 기준정보: `Erp.Desktop/ViewModels/Master/`
- 시스템: `Erp.Desktop/ViewModels/System/`
- 쉘(메인 메뉴/레이아웃): `Erp.Desktop/ViewModels/Shell/`
- 공통: `Erp.Desktop/ViewModels/Common/`

### Services

- 인증 API 통신: `Erp.Desktop/Services/Auth/`
- 공통 유틸: `Erp.Desktop/Services/Common/`
- 시스템 유틸: `Erp.Desktop/Services/System/`

## 3) 회원가입 + 이메일 인증(SMTP)

- API 엔드포인트: `Erp.AuthApi/Program.cs`
- SMTP 설정(개발): `Erp.AuthApi/appsettings.Development.json`
- SMTP 전송 구현: `Erp.Infrastructure/Email/SmtpEmailSender.cs`
- 이메일 인증 서비스: `Erp.Infrastructure/Services/EmailVerificationService.cs`
- 인증 옵션: `Erp.Infrastructure/Services/EmailVerificationOptions.cs`
- 인증 엔티티: `Erp.Domain/Entities/EmailVerificationCode.cs`
- 인증 DTO:
  - `Erp.Application/DTOs/SendEmailVerificationCodeRequest.cs`
  - `Erp.Application/DTOs/SendEmailVerificationCodeResult.cs`
  - `Erp.Application/DTOs/VerifyEmailVerificationCodeRequest.cs`
  - `Erp.Application/DTOs/VerifyEmailVerificationCodeResult.cs`

## 4) Home 대시보드

- 홈 화면 UI: `Erp.Desktop/Views/Dashboard/HomeView.xaml`
- 홈 화면 로직: `Erp.Desktop/ViewModels/Dashboard/HomeViewModel.cs`
- 대시보드 조회 인터페이스: `Erp.Application/Interfaces/IHomeDashboardQueryService.cs`
- 대시보드 조회 구현: `Erp.Infrastructure/Services/HomeDashboardQueryService.cs`
- 대시보드 DTO: `Erp.Application/DTOs/HomeDashboardSummaryDto.cs`

## 5) 공통 스타일(색/버튼/탭)

- 색상: `Erp.Desktop/Themes/Colors.xaml`
- 컨트롤 스타일: `Erp.Desktop/Themes/Controls.xaml`
- 타이포그래피: `Erp.Desktop/Themes/Typography.xaml`
- 메인 레이아웃: `Erp.Desktop/MainWindow.xaml`

## 6) DB/EF Core

- DbContext: `Erp.Infrastructure/Persistence/ErpDbContext.cs`
- 마이그레이션: `Erp.Infrastructure/Persistence/Migrations/`
- DI 등록: `Erp.Infrastructure/Extensions/DependencyInjection.cs`

## 7) 권한/메뉴 구성

- 권한 코드 상수: `Erp.Application/Authorization/PermissionCodes.cs`
- 메인 메뉴 생성: `Erp.Desktop/ViewModels/Shell/MainWindowViewModel.cs`
- 네비게이션 서비스: `Erp.Desktop/Navigation/NavigationService.cs`

## 8) 빠른 검색(PowerShell)

```powershell
# 이메일 인증 관련
rg -n "EmailVerification|Smtp|send-code|verify-code" Erp.*

# Home 대시보드 관련
rg -n "HomeView|HomeViewModel|HomeDashboard" Erp.*

# 사이드바/메인 레이아웃
rg -n "SidebarMenuButtonStyle|TopBar|MainWindow" Erp.Desktop
```
