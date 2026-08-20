# 🖥️ ServiceServer (BFF 세션 관리자 & 웹 게이트웨이)

엔스퀘어 사내 통합 홈페이지의 **웹 호스팅(SPA)**, **OIDC Authorization Code Flow + PKCE SSO**, **BFF 세션 관리**, **ResourceServer 게이트웨이**를 전담하는 서버입니다.  
**Clean Architecture 4계층**과 **sso_pipeline_specification.md 규격**을 준수합니다.

---

## 🏛️ 아키텍처 (Clean Architecture 4계층)

```text
ServiceServer/
├── ServiceServer.slnx
├── Dockerfile
├── README.md
└── src/
    ├── Domain/            # [1. Domain] PKCE 상태, 세션 사용자 정보, 데이터 모델
    ├── Application/       # [2. Application] 트랙 A/B 유스케이스, OIDC 토큰/상태 인터페이스, DTOs
    ├── Infrastructure/    # [3. Infrastructure] ResourceServer HTTP 클라이언트, OIDC 백채널 통신
    └── Web/               # [4. Presentation] Auth/About/Service/History 컨트롤러, wwwroot UI, Swagger UI
```

---

## 🛡️ BFF(Backend For Frontend) 패턴 원리

1. **브라우저에 토큰 미노출**: Access Token / Refresh Token을 브라우저에 저장하지 않고 서비스 서버 내부 세션에 은폐 보관합니다.
2. **안전한 세션 쿠키 발급**: 브라우저에는 `HttpOnly`, `SameSite=Lax`, `Secure` 속성의 `.NsqHomepage.ServiceSession` 쿠키만 발급하여 XSS 탈취 공격을 원천 차단합니다.
3. **2-Track 파이프라인**:
   - **트랙 A (공개 조회)**: 로그인 불필요, ResourceServer(:7002) 최신 데이터 즉시 대행 반환
   - **트랙 B (관리자 처리)**: 서비스 세션의 `Role == Admin` 검증 후 내부 Access Token을 `Bearer` 헤더로 첨부하여 ResourceServer로 전송

---

## 🌐 제공 엔드포인트 목록 (11개)

| 그룹 | 메서드 | 엔드포인트 | 설명 |
| :--- | :--- | :--- | :--- |
| **1. SSO 로그인 & 토큰 발급** | `GET` | `/api/auth/start-sso` | [Step 1~2] PKCE/CSRF 키 생성 및 IdP 인가 주소 발급 |
| | `GET` | `/api/auth/oidc-callback` | [Step 8~12] OIDC 콜백 수신, 백채널 토큰 교환 & 세션 발급 |
| | `GET` | `/api/auth/user-identity` | [Step 12] 현재 세션 사용자 식별 및 Role 확인 |
| **2. 공개 데이터 조회 (트랙 A)** | `GET` | `/api/public/company-about` | 회사 소개 정보 공개 조회 (로그인 불필요 ➔ 리소스 서버 대행) |
| | `GET` | `/api/public/company-services` | 주요 서비스 정보 공개 조회 (로그인 불필요 ➔ 리소스 서버 대행) |
| | `GET` | `/api/public/company-histories`| 전체 연혁 목록 공개 조회 (로그인 불필요 ➔ 리소스 서버 대행) |
| **3. 관리자 데이터 처리 (트랙 B)** | `PUT` | `/api/admin/company-about` | 회사 소개 정보 수정 (`Role == Admin` 검증 + Bearer 첨부) |
| | `PUT` | `/api/admin/company-services` | 주요 서비스 정보 수정 (`Role == Admin` 검증 + Bearer 첨부) |
| | `PUT` | `/api/admin/company-histories`| 연혁 항목 저장/추가 (`Role == Admin` 검증 + Bearer 첨부) |
| **4. 기타 / 보조 기능** | `POST` | `/api/auth/logout` | 서비스 세션 쿠키 파기 및 로그아웃 |
| | `POST` | `/api/tools/manual-token-exchange` | Swagger/Postman용 수동 인가 코드/verifier 토큰 교환 도구 |

---

## 🚀 빠른 시작 가이드 (로컬 개발)

### 1. 솔루션 빌드
```powershell
dotnet build ServiceServer.slnx
```

### 2. 서버 실행
```powershell
dotnet run --project src/Web --launch-profile https
# 홈페이지 접속: https://localhost:7001
# Swagger UI: https://localhost:7001/swagger
```

### 3. Docker 독립 빌드 및 실행
```bash
docker build -t serviceserver:latest .
docker run -d -p 7001:7001 -p 8080:8080 --name serviceserver serviceserver:latest
```

---

## ⚙️ 환경 변수 및 설정 (`appsettings.json`)

| 키 | 설명 | 기본값 |
| :--- | :--- | :--- |
| `Authentication:Authority` | OIDC 인증 서버(IdP) 주소 | `https://localhost:7213` |
| `Authentication:ClientId` | OIDC 클라이언트 식별자 | `company-homepage` |
| `Authentication:RedirectUri` | OIDC 브라우저 콜백 주소 | `https://localhost:7001/api/auth/oidc-callback` |
| `ResourceServer:BaseUrl` | 리소스 API 서버 주소 | `https://localhost:7002` |
