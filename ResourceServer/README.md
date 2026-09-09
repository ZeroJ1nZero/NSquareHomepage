# 📦 ResourceServer (독립 데이터 리소스 API 서버)

엔스퀘어 사내 플랫폼의 회사 소개, 서비스, 연혁 데이터를 전담 관리하는 **독립 데이터 리소스 REST API 서버**입니다.  
**Clean Architecture 4계층**과 **Zero-Trust JWT Bearer 보안 모델**을 준수합니다.

---

## 🏛️ 아키텍처 (Clean Architecture 4계층)

```text
ResourceServer/
├── ResourceServer.slnx
├── Dockerfile
├── README.md
└── src/
    ├── Domain/            # [1. Domain] 순수 엔티티 (CompanyAbout, CompanyService, CompanyHistory)
    ├── Application/       # [2. Application] 비즈니스 유스케이스 (CRUD 9개), DTO, Interfaces
    ├── Infrastructure/    # [3. Infrastructure] EF Core DbContext, DB 매핑 설정 (MariaDB)
    └── Web/               # [4. Presentation] REST 컨트롤러, JWT Bearer 검증 미들웨어, Swagger UI
```

---

## 🛡️ 2-Track 보안 파이프라인

1. **트랙 A: 공개 조회 (`GET`)**
   - 인증/인가 없이 누구나 최신 데이터를 조회할 수 있습니다 (`[AllowAnonymous]`).
2. **트랙 B: 관리자 처리 (`PUT`, `POST`, `DELETE`)**
   - AuthServer(:7213)에서 발급된 서명 검증 및 만료 검사를 수행합니다.
   - `Role == Admin` 클레임이 포함된 유효한 JWT Bearer 토큰이 첨부되어야 합니다 (`[Authorize(Roles = "Admin")]`).

---

## 🌐 제공 엔드포인트 목록

| 메서드 | 엔드포인트 | 파이프라인 분류 | 인증 요구사항 |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/Home/about` | [트랙 A] 회사 소개 정보 조회 | 없음 (공개) |
| `PUT` | `/api/Home/about` | [트랙 B] 회사 소개 정보 수정 | `Bearer Token` + `Role == Admin` |
| `GET` | `/api/Home/service` | [트랙 A] 주요 서비스 정보 조회 | 없음 (공개) |
| `PUT` | `/api/Home/service` | [트랙 B] 주요 서비스 정보 수정 | `Bearer Token` + `Role == Admin` |
| `GET` | `/api/Home/history` | [트랙 A] 전체 연혁 목록 조회 | 없음 (공개) |
| `PUT` | `/api/Home/history` | [트랙 B] 연혁 항목 저장/수정 | `Bearer Token` + `Role == Admin` |

---

## 🚀 빠른 시작 가이드 (로컬 개발)

### 1. 솔루션 빌드
```powershell
dotnet build ResourceServer.slnx
```

### 2. 서버 실행
```powershell
dotnet run --project src/Web --launch-profile https
# 접속 주소: https://localhost:7002
# Swagger UI: https://localhost:7002/swagger
```

### 3. Docker 독립 빌드 및 실행
```bash
docker build -t resourceserver:latest .
docker run -d -p 7002:7002 -p 8080:8080 --name resourceserver resourceserver:latest
```

---

## ⚙️ 환경 변수 및 설정 (`appsettings.json`)

| 키 | 설명 | 기본값 |
| :--- | :--- | :--- |
| `ConnectionStrings:DefaultConnection` | MariaDB 연결 문자열 | `Server=localhost;Port=3306;Database=NSquareResourceDb;User=root;Password=1234;` |
| `Authentication:Authority` | JWT 서명 검증을 위한 IdP 주소 | `https://localhost:7213` |
| `Authentication:Audience` | 대상 클라이언트 식별자 | `company-homepage` |
