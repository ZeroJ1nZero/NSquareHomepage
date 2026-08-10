# NSquare Homepage Web API Server

**NSquare 홈페이지 백엔드 RESTful Web API 서버**입니다.  
클린 아키텍처(Clean Architecture)와 Entity Framework Core Code-First 방식을 적용하여 유지보수성과 확장성을 극대화하였습니다.

---

## 🛠 기술 스택 (Tech Stack)

- **Framework**: .NET 10 (ASP.NET Core Web API)
- **ORM / DB**: Entity Framework Core 10 (Code-First), SQLite / SQL Server
- **Architecture**: Clean Architecture (Presentation / Infrastructure → Application → Domain)
- **API Spec & Tools**: RESTful API Design, Swagger UI (OpenAPI 3.0), Postman Collection 호환

---

## 🏗 프로젝트 아키텍처 (Project Structure)

```
NSquareHomepage/
├── HomepagePrototype/                          # .NET 10 Solution
│   ├── Prototype.slnx                          # 솔루션 파일
│   └── src/
│       ├── Prototype.Domain/                   # [Domain Layer] 
│       │   └── Entities/                       # CompanyInfo, CompanyHistory 엔티티
│       │
│       ├── Prototype.Application/              # [Application Layer]
│       │   ├── DTOs/                           # 계층 간 데이터 전송 DTOs
│       │   ├── UseCases/                       # 컨트롤러 액션 1:1 대치 비즈니스 유스케이스
│       │   └── Common/Interfaces/              # IApplicationDbContext 추상화
│       │
│       ├── Prototype.Infrastructure/           # [Infrastructure Layer]
│       │   ├── Persistence/                    # ApplicationDbContext, EF Core Configurations
│       │   └── Migrations/                     # Code-First DB 마이그레이션 스크립트
│       │
│       └── Prototype.Api/                      # [Presentation Layer]
│           ├── Controllers/                    # RESTful & Postman 호환 API Controllers
│           ├── Middlewares/                    # 전역 예외처리, 로깅, 인증 미들웨어 (OCP 적용)
│           ├── appsettings.json                # DB 연결 및 애플리케이션 설정
│           └── Program.cs                      # DI 등록 및 HTTP 요청 파이프라인
│
├── Nsq_HomepageServer.postman_collection.json  # Postman API 명세서
└── README.md
```

---

## 🚀 주요 기능 (Key Features)

1. **회사 소개 (Company Introduction)**
   - 회사 소개 및 대표 문구 조회 / 수정
2. **회사 서비스 (Company Service)**
   - 주요 제공 서비스 정보 조회 / 수정
3. **회사 연혁 (Company History)**
   - 연혁 전체 목록 조회 / 단건 상세 조회 / 신규 연혁 등록 / 연혁 수정 / 연혁 삭제 (RESTful CRUD)
4. **미들웨어 파이프라인 (Middlewares - OCP 적용)**
   - **ExceptionHandlingMiddleware**: 전역 예외 처리 및 표준 JSON 에러 응답
   - **RequestResponseLoggingMiddleware**: 요청/응답 수행 시간 및 HTTP 메서드 로깅
   - **CustomAuthMiddleware**: 확장 가능한 인증/인가 헤더 검증
5. **대화형 API 문서 (Swagger UI)**
   - `/swagger` 경로를 통한 대화형 API 테스트 지원

---

## 📋 API 엔드포인트 명세 (API Endpoints)

### RESTful API Endpoints
| Verb | Endpoint | Description |
| :--- | :--- | :--- |
| **GET** | `/api/company-info` | 회사 전체 정보(소개 & 서비스) 조회 |
| **PUT** | `/api/company-info` | 회사 정보 수정 |
| **GET** | `/api/histories` | 회사 연혁 전체 목록 조회 |
| **GET** | `/api/histories/{id}` | 특정 연혁 1개 상세 조회 |
| **POST** | `/api/histories` | 신규 연혁 항목 추가 |
| **PUT** | `/api/histories/{id}` | 특정 연혁 1개 수정 |
| **DELETE** | `/api/histories/{id}` | 특정 연혁 1개 삭제 |

### Postman Collection Legacy 호환 Endpoints
- `GET /api/Home/about`, `PUT /api/Home/about`
- `GET /api/Home/service`, `PUT /api/Home/service`
- `GET /api/Home/history`, `PUT /api/Home/history`

---

## 💻 실행 및 시작 가이드 (Getting Started)

### 1. 솔루션 빌드
```bash
dotnet build HomepagePrototype/Prototype.slnx
```

### 2. 프로젝트 실행
```bash
dotnet run --project HomepagePrototype/src/Prototype.Api --urls "http://localhost:5000"
```

### 3. Swagger UI 접속 테스트
웹 브라우저에서 아래 주소로 접속하여 API를 테스트합니다:
- **Swagger UI**: [http://localhost:5000/swagger](http://localhost:5000/swagger)
