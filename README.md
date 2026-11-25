# 🛡️ Budget Defender & Planner Suite (통합 예산 관리 프로그램)

<div align="center">

[![Latest Release](https://img.shields.io/github/v/release/mikuchan1004/BudgetSuite?style=for-the-badge&logo=github&logoColor=white&label=RELEASE&color=89B4FA)](https://github.com/mikuchan1004/BudgetSuite/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/mikuchan1004/BudgetSuite/total?style=for-the-badge&logo=github&logoColor=white&label=DOWNLOADS&color=A6E3A1)](https://github.com/mikuchan1004/BudgetSuite/releases)
[![Build Status](https://img.shields.io/github/actions/workflow/status/mikuchan1004/BudgetSuite/release.yml?style=for-the-badge&logo=githubactions&logoColor=white&label=BUILD)](https://github.com/mikuchan1004/BudgetSuite/actions)
[![Platform](https://img.shields.io/badge/WINDOWS-10%2F11%20x64-0078D6?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/mikuchan1004/BudgetSuite/releases)

[![Framework](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Language](https://img.shields.io/badge/C%23-12-239120?style=for-the-badge&logo=dotnet&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Database](https://img.shields.io/badge/CLOUD%20SYNC-MySQL%20%2F%20TiDB-4479A1?style=for-the-badge&logo=mysql&logoColor=white)](schema.sql)
[![AI Collaboration](https://img.shields.io/badge/CO--CREATED%20WITH-GOOGLE%20GEMINI%20AI-8E75B2?style=for-the-badge&logo=googlegemini&logoColor=white)](https://deepmind.google/technologies/gemini/)
[![AI Partner](https://img.shields.io/badge/AI%20PARTNER-ANTIGRAVITY%20%26%20LUNA-CBA6F7?style=for-the-badge&logo=googlegemini&logoColor=1E1E2E)](https://github.com/mikuchan1004/BudgetSuite)

</div>

`BudgetManager`(수입 및 고정지출 플래너)와 `BudgetDefender`(일일 실질 예산 방어기 & 지출 장부)를 하나로 통합한 올인원 개인 재정 관리 데스크톱 프로그램입니다.

---

## 🖼️ 스크린샷 

<img width="1122" height="726" alt="image" src="https://github.com/user-attachments/assets/ab9754b4-6d83-4476-8fc7-0619f0f17a48" />

## 📥 다운로드 및 실행 방법 (일반 사용자용)

1. 우측(또는 상단)의 **[Releases]** 페이지에서 최신 버전의 `BudgetSuite.exe`를 다운로드합니다.
2. 원하는 폴더에 넣고 **`BudgetSuite.exe`를 더블클릭**하면 바로 실행됩니다.
   - 별도의 .NET 설치가 필요 없는 **단일 실행 파일(Self-Contained)**입니다. (Windows 10 / 11 x64 지원)
3. 입력하신 잔액, 고정지출, 거래 내역은 실행 파일과 같은 폴더의 **`appsettings.json`**에 자동 저장됩니다.

---

## ☁️ 선택 사항: 나만의 클라우드 DB 동기화 설정

기본적으로는 내 PC에만 저장(`💾 로컬 저장 모드`)되지만, 여러 기기에서 데이터를 동기화하고 싶다면 본인의 MySQL / MariaDB / TiDB Cloud 계정을 연결할 수 있습니다.

1. DB에 접속해 [`schema.sql`](schema.sql)을 실행하여 필요한 테이블(`wallets`, `transactions`, `fixed_expenses`, `plans`)을 생성합니다.
2. [`db_config.example.json`](db_config.example.json)을 복사해 `BudgetSuite.exe`와 같은 폴더에 **`db_config.json`** 이름으로 저장합니다.
3. `db_config.json` 안에 본인의 DB 주소(`host`), 포트(`port`), 사용자(`user`), 비밀번호(`password`), 데이터베이스 이름(`database`)을 입력한 뒤 프로그램을 다시 실행하면 자동으로 클라우드 동기화가 활성화됩니다.

---

## 🌟 주요 통합 기능 및 특징

### 1. 🛡️ 일일 예산 방어 (Daily Defender)
* **실시간 실질 가용 현금 계산**: 통장 잔고에서 월말 목표 잔고와 해외 가승인/홀드금을 차감한 뒤, 남은 일수 및 오늘의 현금 지출을 계산하여 **1,000원 단위 절사 가용액**을 실시간 산출합니다.
* **일일 거래 빠른 등록 (지출 & 수입 & 이체 지원)**:
  * 수입 등록 시 선택한 계좌/포인트(통장, 네이버P, 카카오P)로 **잔고가 즉시 실시간 입금 가산(+)**됩니다.
  * 지출 등록 시 해당 수단 잔고에서 **즉시 실시간 차감(-)**됩니다.
  * 거래 내역 삭제 시 해당 금액이 원래대로 완벽 복구됩니다.
* **해외 가승인/홀드금 분리 관리**: 해외 사이트에서 일시적으로 묶인 가승인/홀드금을 독립적으로 분리 보존합니다.
* **미출금 고정지출 보호 차감 옵션**: 플래너에 등록된 아직 출금되지 않은 고정지출을 통장 가용액에서 미리 차감하여 과소비를 원천 차단합니다.
* **포인트/머니 방패**: 네이버페이 포인트와 카카오페이머니를 현금 대신 방어벽으로 관리 및 합산 표시합니다.

### 2. 💰 월간 수입 & 고정지출 플래너 (Monthly Planner)
* **동적 수입 계획 관리**: 월급, 부수입, 수당 등 다양한 수입 항목을 자유롭게 `+ 항목 추가` 및 `✕ 삭제`, 금액 수정할 수 있습니다.
* **동적 투자 & 저축/용돈 계획 관리**: 저축, 투자, 용돈 등 원하는 항목을 마음대로 추가/삭제/관리합니다.
* **스마트 고정지출 체크리스트**: 고정지출 항목 추가/삭제, 금액 설정 및 납부 완료 체크 기능을 제공합니다.
* **자동 월초 리셋**: 매월 1일이 되면 완료 체크가 자동으로 안전하게 해제되어 새로운 달의 납부 관리를 준비합니다.
* **실시간 계산 요약**: 총 수입액, 총 투자금 합계, 투자금 차감 후 잔액, 총 고정지출, 남은 고정지출, 최종 비상금(월말 잔고)을 실시간 집계합니다.

### 3. ⚡ 플래너 ➔ 방어기 스마트 데이터 연동 (Core Synergy)
* **원클릭 일괄 동기화 (`⚡ 플래너 ➔ 방어기 일괄 동기화`)**:
  * 플래너의 **미출금 고정지출** ➔ 방어기의 **가승인/홀딩액**에 즉시 반영 (고정지출 예정액을 통장에서 미리 잠금)
  * 플래너의 **최종 비상금** ➔ 방어기의 **월말 목표 잔고**로 동기화
  * 플래너의 **카카오페이 용돈** ➔ 방어기의 **카카오페이머니 방패**로 동기화
* 필요에 따라 각 항목별로 개별 동기화 버튼도 지원합니다.

### 4. 📊 월간 지출 분석 & 히스토리 (Analytics)
* 당월 전체 지출 내역을 한눈에 조회하고 관리할 수 있습니다.
* **월간 지표 분석**: 이번 달 총 지출액, 현금/체크 실질 지출, 포인트 방어액, 지출 기록 일수, 일평균 현금 지출액을 실시간 브리핑합니다.

### 5. 🎨 세련된 비주얼 디자인
* **전용 배경 일러스트** 탑재 (부드러운 다크 그라데이션 오버레이로 최상의 텍스트 가독성 확보)
* **다크 글래스모피즘(Glassmorphism) 테마**: Catppuccin Mocha 팔레트 기반의 눈이 편안한 카드 레이아웃

---

## 🛠️ 기술 스택 & 직접 빌드하기
* **Framework**: .NET 8.0 (WPF / Windows x64)
* **Language**: C# 12
* **Storage**: `appsettings.json` (로컬 UTF-8 자동 저장) + 선택적 MySQL/TiDB 클라우드 동기화 (`db_config.json`)

```powershell
dotnet publish BudgetSuite.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false -o Publish
```
