# ERP (Enterprise Resource Planning) Desktop

WPF 기반 ERP 데스크톱 앱입니다.

## 주요기능 
- 사용자 로그인 / 권한 기반 메뉴 노출
- 회원가입 시 이메일 인증 코드 발송/인증
  - 인증 코드: 숫자 + 영문(대문자+소문자) 8자리
  - 인증 코드 만료 시간: 3분
- 재고/구매/판매/회계 모듈

## 개발 환경
- .NET SDK 8.0
- Docker Desktop
- PostgreSQL

## 참고
- 프로그램 시작 시 마이그레이션 및 시드 데이터가 자동 적용됩니다.
- DB 비밀번호 오류 시 '.env' 값 확인 후 DB를 초기화 한 후 다시 실행해야 합니다.

## 예정 사항
- Window App (.exe) 생성 예정
- 각 메뉴 세분화 
