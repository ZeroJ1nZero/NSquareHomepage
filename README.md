# 🏢 NSquareHomepage (엔스퀘어 사내 통합 홈페이지 & SSO 시스템)

클린 아키텍처(Clean Architecture) 및 EF Core Code-First 구조 기반으로 제작된 엔스퀘어 사내 통합 백엔드 API 및 OIDC SSO(Single Sign-On) 인증 서버 프로젝트입니다.

---

## 📐 프로젝트 구조 (Project Architecture)

이 프로젝트는 **Clean Architecture** 원칙을 엄격히 준수하여 4개의 독립된 레이어 계층으로 구성되어 있으며, 사내 통합 인증을 위한 **`nsq_auth` SSO 서버**와 연동됩니다.

```text
C:\NSquareHomepage\
├── HomepagePrototype/                      # 🏢 사내 홈페이지 Web API 솔루션
│   ├── Prototype.slnx
│   └── src/
│       ├── Prototype.Domain/               # 💎 [Domain Layer] 순수 도메인 엔티티
│       │   └── Entities/
│       │       ├── CompanyInfo.cs          # 회사 소개 및 서비스 엔티티
│       │       └── CompanyHistory.cs       # 연혁 엔티티
│       │
│       ├── Prototype.Application/          # ⚙️ [Application Layer] 유스케이스 및 DTO
│       │   ├── DTOs/
│       │   │   ├── About/                  # AboutDto, UpdateAboutDto
│       │   │   ├── Service/                # ServiceDto, UpdateServiceDto
│       │   │   └── History/                # CompanyHistoryDtos
│       │   └── UseCases/
│       │       ├── About/                  # GetAboutUseCase, UpdateAboutUseCase
│       │       ├── Service/                # GetServiceUseCase, UpdateServiceUseCase
│       │       └── History/                # GetCompanyHistoriesUseCase, CreateCompanyHistoryUseCase ...
│       │
│       ├── Prototype.Infrastructure/       # 🗄️ [Infrastructure Layer] DB & EF Core
│       │   ├── Persistence/
│       │   │   ├── ApplicationDbContext.cs
│       │   │   └── Configurations/        # Fluent API 테이블 매핑
│       │   └── Migrations/                 # EF Core Code-First 마이그레이션
│       │
│       └── Prototype.Api/                  # 🌐 [Presentation Layer] RESTful Web API
│           ├── Controllers/
│           │   ├── AboutController.cs      # GET/PUT /api/about
│           │   ├── ServicesController.cs   # GET/PUT /api/services
│           │   ├── HistoriesController.cs  # GET/PUT /api/histories
│           │   └── AuthController.cs       # Dev Admin JWT 토큰 발급
│           └── Middlewares/                # Custom Exception, Logging, Auth 미들웨어
│
└── nsq_auth/                               # 🔐 [SSO Auth Server] OpenIddict OIDC 인증 서버
    ├── AuthServer.slnx
    └── src/
        ├── Domain/ & Application/ & Infrastructure/
        └── Web/                            # connect/authorize, connect/token 엔드포인트
```

---

## 🛠️ 기술 스택 (Tech Stack)

- **Framework**: .NET 10 Web API
- **Architecture**: Clean Architecture (Presentation ➔ Infrastructure ➔ Application ➔ Domain)
- **ORM / Database**: Entity Framework Core 10 (Code-First)
- **Database Server**: **Microsoft SQL Server Express** (`localhost\SQLEXPRESS` / `HomepagePrototypeDb`)
- **Authentication**: JWT Bearer Authentication & OpenIddict OIDC SSO (`nsq_auth`)
- **API Documentation**: Swashbuckle Swagger UI (`/swagger`) with Bearer Authorization

---

## 🌐 RESTful API 명세표 (API Specifications)

| 도메인 | HTTP Method | Endpoint | 설명 | 권한 요구사항 |
| :--- | :--- | :--- | :--- | :--- |
| **About** | `GET` | `/api/about` | 회사 소개 정보 조회 | 누구나 |
| **About** | `PUT` | `/api/about` | 회사 소개 정보 수정 | **SSO JWT 인증 필요 (🔒)** |
| **Services** | `GET` | `/api/services` | 회사 주요 서비스 정보 조회 | 누구나 |
| **Services** | `PUT` | `/api/services` | 회사 주요 서비스 정보 수정 | **SSO JWT 인증 필요 (🔒)** |
| **Histories** | `GET` | `/api/histories` | 전체 연혁 목록 조회 | 누구나 |
| **Histories** | `PUT` | `/api/histories` | 새로운 연혁 항목 입력/추가 | **SSO JWT 인증 필요 (🔒)** |
| **Auth (Dev)** | `GET` | `/api/auth/dev-token` | Swagger UI 테스트용 Admin JWT 토큰 발급 | 누구나 |

---

## 🔑 SSO 인증 & Swagger UI 테스트 가이드

1. **Web API 서버 실행**:
   ```bash
   dotnet run --project HomepagePrototype/src/Prototype.Api --urls "http://localhost:5000"
   ```
2. **Swagger UI 접속**: [http://localhost:5000/swagger](http://localhost:5000/swagger)
3. **테스트용 Admin JWT 토큰 발급**:
   - Swagger UI에서 `GET /api/auth/dev-token` ➔ `Try it out` ➔ `Execute` 실행
   - 응답으로 출력되는 `swagger_header_value` (`Bearer eyJhbGci...`) 복사
4. **Swagger UI 인증 수락**:
   - 우측 상단 🟢 **`Authorize (🔒)`** 버튼 클릭
   - 복사한 `Bearer eyJhbGci...` 전체 텍스트 붙여넣기 ➔ `Authorize` 클릭
5. **보호된 API 테스트**:
   - `PUT /api/about`, `PUT /api/services`, `PUT /api/histories` 실행 시 데이터 수정 및 SQL Server Express DB 자동 저장 확인!

---

## 🗄️ SQL Server Express DB 연결 가이드 (VS Code)

1. VS Code **`SQL Server (mssql)`** 확장 설치
2. `+ Add Connection` 클릭 후 **`Connection String`** 선택:
   ```text
   Server=localhost\SQLEXPRESS;Database=HomepagePrototypeDb;Trusted_Connection=True;TrustServerCertificate=True;
   ```
3. 프로필 이름: `NSquareHomepage` 입력 후 Enter ➔ `Databases` ➔ `HomepagePrototypeDb` ➔ `Tables` 데이터 확인!

---

## 🚀 Git & GitHub 게시 정보

- **Repository**: `https://github.com/ZeroJ1nZero/NSquareHomepage.git`
- **Git Config**: `user.name` = `ZeroJ1nZero`, `user.email` = `ZeroJ1nZero@github.com`
- **민감 정보 보호**: 비밀번호, 데이터베이스파일(`.db`), 빌드 결과물(`bin/`, `obj/`)은 `.gitignore`에 의해 안전하게 제외되어 있습니다.
