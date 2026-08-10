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

---

## 💻 실행 가이드 (Getting Started)

```bash
# 솔루션 빌드
dotnet build Prototype.slnx

# API 서버 실행
dotnet run --project src/Prototype.Api --urls "http://localhost:5000"
```
- **Swagger UI**: [http://localhost:5000/swagger](http://localhost:5000/swagger)
