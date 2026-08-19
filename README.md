# 🏢 NSquareHomepage (엔스퀘어 사내 통합 홈페이지 & SSO 시스템)

OIDC SSO(Single Sign-On) 기반의 세션-JWT 이중 보안 아키텍처와 Clean Architecture로 구축된 엔스퀘어 사내 통합 시스템입니다.  
본 시스템은 **[docs/sso_pipeline_specification.md](file:///C:/NSquareHomepage/docs/sso_pipeline_specification.md)** 명세를 엄격히 준수하여 역할을 엄격히 분리한 3개의 독립 서버 및 통합 클라이언트로 운영됩니다.

---

## 📐 시스템 컴포넌트 아키텍처 (System Architecture)

```text
C:\NSquareHomepage\
├── docs/                                   # 📁 [문서 & 다이어그램 통합 관리]
│   ├── sso_pipeline_specification.md       # 📜 SSO 2-Track 파이프라인 및 8대 자격증명 상세 명세서
│   ├── OIDC-도입-보고서.md                 # 📋 OIDC 도입 분석 및 기술 검토 보고서
│   ├── SSO 로그인 요약 순서도.drawio         # 📊 SSO 파이프라인 순서도 다이어그램
│   ├── auth-flow.drawio                    # 📊 상세 인증 시퀀스 다이어그램
│   └── images/                             # 🖼️ 아키텍처 다이어그램 및 이미지 에셋
│
├── ServiceServer/                          # 🖥️ [2. 서비스 서버] BFF 웹 서버 & 세션 관리자 (:7001)
│   ├── ServiceServer.slnx
│   └── src/
│       └── ServiceServer.Api/              # OIDC SSO(BFF), 세션 쿠키 관리 & 게이트웨이 프록시
│           ├── Controllers/                # AuthController (/api/auth/login, /signin-oidc, /api/auth/me, /api/auth/logout)
│           ├── Services/                   # ResourceApiClient, OidcStateService
│           ├── Middlewares/                # 예외 처리, 로깅, 커스텀 인증 미들웨어
│           └── wwwroot/                    # 🌐 [1. 클라이언트 UI] 방문자 / 관리자 웹 인터페이스
│
├── ResourceServer/                         # 📦 [3. 리소스 서버] 데이터 API 서버 (모든 CRUD 전담 & Zero-Trust) (:7002)
│   ├── ResourceServer.slnx
│   └── src/
│       ├── ResourceServer.Domain/          # 💎 [Domain Layer] 순수 엔티티 (CompanyInfo, CompanyHistory)
│       ├── ResourceServer.Application/     # ⚙️ [Application Layer] 비즈니스 유스케이스 & DTOs
│       ├── ResourceServer.Infrastructure/  # 🗄️ [Infrastructure Layer] EF Core DbContext, SQLite/MSSQL 지원
│       └── ResourceServer.Api/             # 🌐 [Presentation Layer] CRUD RESTful API & JWT Bearer 검증
│           └── Controllers/                # AboutController, ServicesController, HistoriesController
│
├── AuthServer/                             # 🔐 [4. 인증 서버] OpenIddict OIDC 통합 인증 서버 (:7213)
│   ├── AuthServer.slnx
│   └── src/
│       ├── Domain/ & Application/ & Infrastructure/
│       └── Web/                            # OIDC 엔드포인트 (/connect/authorize, /connect/token, /login)
│
├── mssqllocaldb/                           # 🗄️ MSSQL LocalDB 데이터베이스 프로젝트
└── Nsq_HomepageServer.postman_collection.json # 📄 Postman API 테스팅 콜렉션 파일
```

---

## 🔄 역할 분리 및 처리 파이프라인

- **Client (`ServiceServer/wwwroot`)**: 자바스크립트에 JWT 토큰(`Access/Refresh Token`)을 절대 소유하지 않으며, `HttpOnly` 서비스 세션 쿠키로 통신하여 XSS를 원천 차단합니다.
- **ServiceServer (`:7001`)**: 연혁, 서비스, 회사 소개에 대한 자체 CRUD DB 로직을 두지 않으며, **오직 OIDC SSO 인증, PKCE 검증, 세션 쿠키 발급, 게이트웨이 API 중계 및 전역 로그아웃 관리**만 전담합니다.
- **ResourceServer (`:7002`)**: 연혁, 서비스, 회사 소개에 대한 **모든 CRUD를 전담**하며, [트랙 A: 공개 조회 `GET`]와 [트랙 B: 관리자 `PUT` (JWT Bearer Token + Role == Admin 검증)]로 처리합니다.
- **AuthServer (`:7213`)**: OpenIddict 6 및 MariaDB 기반으로 계정 검증, SSO 쿠키, 1회용 인가 코드 및 15분 수명의 JWT Access Token 세트를 발급하는 중앙 IdP입니다.

---

## 🛠️ 컴포넌트별 기술 스택 및 포트 구성

| 컴포넌트 | 실행 포트 | 주요 기술 및 역할 | 비고 |
| :--- | :--- | :--- | :--- |
| **AuthServer** | `https://localhost:7213`<br/>`http://localhost:5123` | .NET 10, OpenIddict 6, MariaDB, PKCE 원사이드 검증, OIDC 토큰 세트 발급 | 통합 인증 IdP |
| **ServiceServer** | `https://localhost:7001`<br/>`http://localhost:5016` | .NET 10 Web API, BFF 아키텍처, `HttpOnly` 서비스 세션 쿠키 발급 및 OIDC 로그인 관리, 정적 UI 호스팅 | 웹 세션 관리자 & UI |
| **ResourceServer** | `https://localhost:7002`<br/>`http://localhost:5002` | .NET 10 Web API, Clean Architecture, EF Core 10 (SQLite/MSSQL), Zero-Trust JWT 인가 | 데이터 CRUD 리소스 서버 |

---

## 🌐 엔드포인트 명세표 (API Specifications)

### 1. ServiceServer (:7001) — 인증 & 세션 엔드포인트 (CRUD 없음)
| Method | Endpoint | 설명 | 권한 요구사항 |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/auth/login` | PKCE 키 생성 및 IdP 인가 주소 반환 | 누구나 |
| `GET/POST` | `/signin-oidc` | IdP 콜백 수신, 토큰 백채널 교환 및 서비스 세션 쿠키 발급 | IdP 인가 코드 필요 |
| `GET` | `/api/auth/me` | 현재 서비스 세션 사용자 정보 조회 | **서비스 세션 쿠키 필요** |
| `GET/POST` | `/api/auth/logout` | 서비스 세션 파기 및 IdP 전역 로그아웃 | 누구나 |
| `GET` | `/signout-callback-oidc` | 전역 로그아웃 완료 화면 | 누구나 |

### 2. ResourceServer (:7002) — 데이터 CRUD 리소스 엔드포인트
| 도메인 | Method | Endpoint | 설명 | 권한 요구사항 |
| :--- | :--- | :--- | :--- | :--- |
| **About** | `GET` | `/api/Home/about` | [트랙 A] 회사 소개 조회 | `[AllowAnonymous]` (누구나) |
| **About** | `PUT` | `/api/Home/about` | [트랙 B] 회사 소개 수정 | `[Authorize(Roles = "Admin")]` (JWT Bearer) |
| **Services** | `GET` | `/api/Home/service` | [트랙 A] 주요 서비스 정보 조회 | `[AllowAnonymous]` (누구나) |
| **Services** | `PUT` | `/api/Home/service` | [트랙 B] 주요 서비스 정보 수정 | `[Authorize(Roles = "Admin")]` (JWT Bearer) |
| **Histories** | `GET` | `/api/Home/history` | [트랙 A] 전체 연혁 목록 조회 | `[AllowAnonymous]` (누구나) |
| **Histories** | `PUT` | `/api/Home/history` | [트랙 B] 연혁 항목 저장/추가 | `[Authorize(Roles = "Admin")]` (JWT Bearer) |

---

## 🚀 전체 서버 실행 방법

각 서버를 별도의 터미널 창에서 순서대로 실행합니다:

### 1. OIDC SSO 인증 서버 (`AuthServer`) 실행
```bash
dotnet run --project AuthServer/src/Web --launch-profile https
```
- **Swagger UI**: [https://localhost:7213/swagger](https://localhost:7213/swagger) (또는 [http://localhost:5123/swagger](http://localhost:5123/swagger))

### 2. 데이터 리소스 서버 (`ResourceServer`) 실행
```bash
dotnet run --project ResourceServer/src/ResourceServer.Api --launch-profile https
```
- **Swagger UI**: [https://localhost:7002/swagger](https://localhost:7002/swagger) (또는 [http://localhost:5002/swagger](http://localhost:5002/swagger))

### 3. 서비스 웹 서버 (`ServiceServer`) 실행
```bash
dotnet run --project ServiceServer/src/ServiceServer.Api --launch-profile https
```
- **Swagger UI**: [https://localhost:7001/swagger](https://localhost:7001/swagger) (또는 [http://localhost:5016/swagger](http://localhost:5016/swagger))
- **홈페이지 UI**: [https://localhost:7001](https://localhost:7001)
