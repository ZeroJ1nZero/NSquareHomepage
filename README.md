# 🏢 N-SQUARE 통합 플랫폼 (Microservice & Clean Architecture Ecosystem)

엔스퀘어 사내 통합 플랫폼은 **3개의 독립된 서버 프로젝트**로 완벽히 분리되어 구성되어 있습니다. 각 서버는 독립된 Git 저장소, 독립된 솔루션 파일, 독립된 Dockerfile을 보유하며 Clean Architecture 4계층 구조를 따릅니다.

---

## 🏛️ 독립 프로젝트 구성

```text
NSquareHomepage/
│
├── 🔐 AuthServer/           # [독립 프로젝트 1] OIDC SSO 인증 서버 (:7213)
│   ├── AuthServer.slnx
│   ├── Dockerfile
│   ├── .gitignore
│   ├── README.md
│   └── src/ (Domain, Application, Infrastructure, Web)
│
├── 📦 ResourceServer/       # [독립 프로젝트 2] 데이터 리소스 API 서버 (:7002)
│   ├── ResourceServer.slnx
│   ├── Dockerfile
│   ├── .gitignore
│   ├── README.md
│   └── src/ (Domain, Application, Infrastructure, Api)
│
├── 🖥️ ServiceServer/        # [독립 프로젝트 3] BFF 세션 관리자 & 웹 호스트 (:7001)
│   ├── ServiceServer.slnx
│   ├── Dockerfile
│   ├── .gitignore
│   ├── README.md
│   └── src/ (Domain, Application, Infrastructure, Api, wwwroot)
│
├── docker-compose.yml       # 3개 독립 서버 + DB 통합 오케스트레이션
└── docs/                    # 아키텍처 및 파이프라인 명세서
```

---

## 🚀 빠른 시작 가이드

### 방법 1. 로컬 환경에서 각각 실행
각 프로젝트 디렉토리로 이동하여 독립적으로 실행할 수 있습니다:

```powershell
# 1. 인증 서버 실행 (AuthServer)
dotnet run --project AuthServer/src/Web --launch-profile https
# -> https://localhost:7213

# 2. 리소스 서버 실행 (ResourceServer)
dotnet run --project ResourceServer/src/Api --launch-profile https
# -> https://localhost:7002

# 3. 서비스 서버 실행 (ServiceServer)
dotnet run --project ServiceServer/src/Api --launch-profile https
# -> https://localhost:7001
```

### 방법 2. Docker Compose로 전체 오케스트레이션 실행
```bash
docker compose up --build -d
```
