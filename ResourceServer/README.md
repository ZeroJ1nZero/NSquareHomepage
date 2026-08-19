# 📦 ResourceServer (데이터 리소스 API 서버)

엔스퀘어 사내 홈페이지 시스템의 데이터 리소스 API 서버입니다. Clean Architecture와 EF Core Code-First 방식으로 작성되었으며, Zero-Trust 원칙에 따라 JWT Bearer 토큰 서명/만료 및 Role을 독립적으로 검증합니다.

---

## 📐 레이어 계층 구조 (Clean Architecture Layers)

- **`ResourceServer.Domain`**: 순수 도메인 엔티티 (`CompanyInfo`, `CompanyHistory`). 외부 의존성 0개.
- **`ResourceServer.Application`**: 비즈니스 유스케이스 (`About`, `Service`, `History` 유스케이스) 및 DTO 객체.
- **`ResourceServer.Infrastructure`**: EF Core `ApplicationDbContext`, Fluent API 테이블 설정, Code-First 마이그레이션 (`SQL Server Express` / `SQLite` 지원).
- **`ResourceServer.Api`**: RESTful API 컨트롤러 (`AboutController`, `ServicesController`, `HistoriesController`), 미들웨어, Swagger UI.

---

## 🌐 2-Track 처리 파이프라인

- **트랙 A: 공개 조회 (`GET`)**: 인증 없이 모든 사용자 접근 허용 (`[AllowAnonymous]`)
- **트랙 B: 관리자 처리 (`PUT`)**: AuthServer에서 발급한 JWT Bearer 토큰의 서명 및 `Role == Admin` 검증 (`[Authorize(Roles = "Admin")]`)

---

## 🚀 빠른 시작 가이드

1. **프로젝트 빌드**:
   ```bash
   dotnet build ResourceServer.slnx
   ```
2. **Web API 서버 구동**:
   ```bash
   dotnet run --project src/ResourceServer.Api --launch-profile https
   # 또는: dotnet run --project src/ResourceServer.Api --urls "https://localhost:7002;http://localhost:5002"
   ```
3. **Swagger UI 접속**: [https://localhost:7002/swagger](https://localhost:7002/swagger)
