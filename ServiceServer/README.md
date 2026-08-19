# 🖥️ ServiceServer (서비스 웹 서버 & 세션 관리자)

엔스퀘어 사내 통합 홈페이지의 OIDC SSO 인증 및 세션을 전담하는 서비스 웹 서버(BFF)입니다.  
**연혁, 서비스, 회사 소개 등의 데이터 CRUD는 오직 [`ResourceServer`](file:///C:/NSquareHomepage/ResourceServer)에서 독립적으로 수행되며, ServiceServer는 데이터 CRUD를 포함하지 않고 인증 및 세션 관리 역할만 담당합니다.**

---

## 📐 역할 및 아키텍처 ([docs/sso_pipeline_specification.md](../docs/sso_pipeline_specification.md) 준수)

1. **BFF 세션 관리자**:
   - 브라우저에 `HttpOnly`, `SameSite=Lax` 속성의 `.NsqHomepage.ServiceSession` 쿠키 발급 및 검증
   - 브라우저에 JWT Access/Refresh Token을 노출하지 않아 XSS 공격 원천 차단
2. **OIDC SSO 인증 파이프라인**:
   - PKCE(`code_verifier` / `code_challenge`) 및 CSRF 방어용 `state` 관리
   - IdP(AuthServer :7213)와의 백채널 토큰 교환 및 세션 발급/전역 로그아웃 관리

---

## 🌐 제공 엔드포인트 목록

| Method | Endpoint | 설명 | 권한 |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/auth/login` | PKCE 키 생성 및 IdP 인가 주소 반환 | 누구나 |
| `GET/POST` | `/signin-oidc` | IdP 콜백 수신, 백채널 토큰 교환 및 서비스 세션 쿠키 발급 | IdP 인가 코드 필요 |
| `GET` | `/api/auth/me` | 현재 세션 사용자 정보 조회 | **서비스 세션 쿠키** |
| `GET/POST` | `/api/auth/logout` | 서비스 세션 파기 및 IdP 전역 로그아웃 | 누구나 |
| `GET` | `/signout-callback-oidc` | OIDC 전역 로그아웃 완료 화면 | 누구나 |

---

## 🚀 빠른 시작 가이드

1. **프로젝트 빌드**:
   ```bash
   dotnet build ServiceServer.slnx
   ```
2. **서비스 서버 구동**:
   ```bash
   dotnet run --project src/ServiceServer.Api --launch-profile https
   ```
3. **Swagger UI 접속**: [https://localhost:7001/swagger](https://localhost:7001/swagger)
