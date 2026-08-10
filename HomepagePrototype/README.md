# 🏢 HomepagePrototype Web API

엔스퀘어 사내 통합 홈페이지 시스템의 백엔드 Web API 프로젝트입니다. Clean Architecture와 EF Core Code-First 방식으로 작성되었습니다.

---

## 📐 레이어 계층 구조 (Clean Architecture Layers)

- **`Prototype.Domain`**: 순수 도메인 엔티티 (`CompanyInfo`, `CompanyHistory`). 외부 의존성 0개.
- **`Prototype.Application`**: 비즈니스 유스케이스 (`About`, `Service`, `History` 유스케이스) 및 DTO 객체.
- **`Prototype.Infrastructure`**: EF Core `ApplicationDbContext`, Fluent API 테이블 설정, Code-First 마이그레이션 (`SQL Server Express` / `SQLite` 동적 지원).
- **`Prototype.Api`**: RESTful API 컨트롤러 (`AboutController`, `ServicesController`, `HistoriesController`, `AuthController`), 커텀 예외/로깅/인증 미들웨어, Swagger UI.

---

## 🚀 빠른 시작 가이드

1. **프로젝트 빌드**:
   ```bash
   dotnet build Prototype.slnx
   ```
2. **DB 마이그레이션 적용 (SQL Server Express)**:
   ```bash
   dotnet ef database update --project src/Prototype.Infrastructure --startup-project src/Prototype.Api
   ```
3. **Web API 서버 구동**:
   ```bash
   dotnet run --project src/Prototype.Api --urls "http://localhost:5000"
   ```
4. **Swagger UI 접속**: [http://localhost:5000/swagger](http://localhost:5000/swagger)
