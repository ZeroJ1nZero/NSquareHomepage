/**
 * 엔스퀘어(N-SQUARE) 통합 플랫폼 프론트엔드 어플리케이션
 * - SSO 2-Track 파이프라인 (sso_pipeline_specification.md 준수)
 */

document.addEventListener('DOMContentLoaded', () => {
  // Global State
  let currentUser = {
    isAuthenticated: false,
    userName: null,
    role: null
  };

  let aboutData = { content: '', updatedAt: null };
  let serviceData = { service: '', updatedAt: null };
  let historyList = [];

  // DOM Elements
  const authWidget = document.getElementById('authWidget');
  const sessionIndicator = document.getElementById('sessionIndicator');
  const sessionRoleText = document.getElementById('sessionRoleText');
  const logStream = document.getElementById('logStream');
  const btnClearLogs = document.getElementById('btnClearLogs');
  const btnRefreshAll = document.getElementById('btnRefreshAll');

  // About DOM
  const aboutContentText = document.getElementById('aboutContentText');
  const aboutUpdatedAt = document.getElementById('aboutUpdatedAt');
  const btnEditAbout = document.getElementById('btnEditAbout');
  const aboutViewBox = document.getElementById('aboutViewBox');
  const aboutEditBox = document.getElementById('aboutEditBox');
  const inputAbout = document.getElementById('inputAbout');
  const btnCancelAbout = document.getElementById('btnCancelAbout');
  const btnSaveAbout = document.getElementById('btnSaveAbout');

  // Service DOM
  const serviceContentText = document.getElementById('serviceContentText');
  const serviceUpdatedAt = document.getElementById('serviceUpdatedAt');
  const btnEditService = document.getElementById('btnEditService');
  const serviceViewBox = document.getElementById('serviceViewBox');
  const serviceEditBox = document.getElementById('serviceEditBox');
  const inputService = document.getElementById('inputService');
  const btnCancelService = document.getElementById('btnCancelService');
  const btnSaveService = document.getElementById('btnSaveService');

  // History DOM
  const historyTimeline = document.getElementById('historyTimeline');
  const btnToggleHistoryForm = document.getElementById('btnToggleHistoryForm');
  const historyAddBox = document.getElementById('historyAddBox');
  const inputHistoryDate = document.getElementById('inputHistoryDate');
  const inputHistoryContent = document.getElementById('inputHistoryContent');
  const btnCancelHistory = document.getElementById('btnCancelHistory');
  const btnSaveHistory = document.getElementById('btnSaveHistory');

  // Login Modal DOM
  const loginModal = document.getElementById('loginModal');
  const loginForm = document.getElementById('loginForm');
  const inputLoginEmail = document.getElementById('inputLoginEmail');
  const inputLoginPassword = document.getElementById('inputLoginPassword');
  const loginErrorMsg = document.getElementById('loginErrorMsg');
  const btnCloseLoginModal = document.getElementById('btnCloseLoginModal');
  const btnCancelLoginModal = document.getElementById('btnCancelLoginModal');

  // =========================================================================
  // 1. Logger & Toast Utilities
  // =========================================================================
  function log(tag, msg, type = 'info') {
    const timeStr = new Date().toTimeString().split(' ')[0];
    const entry = document.createElement('div');
    entry.className = `log-entry ${type}`;
    entry.innerHTML = `
      <span class="log-time">[${timeStr}]</span>
      <span class="log-tag">${tag}</span>
      <span class="log-msg">${msg}</span>
    `;
    logStream.appendChild(entry);
    logStream.scrollTop = logStream.scrollHeight;
  }

  function showToast(msg, type = 'info') {
    const container = document.getElementById('toastContainer');
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.textContent = msg;
    container.appendChild(toast);
    setTimeout(() => {
      toast.style.opacity = '0';
      toast.style.transform = 'translateY(10px)';
      toast.style.transition = 'all 0.3s ease';
      setTimeout(() => toast.remove(), 300);
    }, 3000);
  }

  function formatDate(isoStr) {
    if (!isoStr) return '-';
    try {
      const d = new Date(isoStr);
      return isNaN(d.getTime()) ? isoStr : d.toLocaleString('ko-KR');
    } catch {
      return isoStr;
    }
  }

  // =========================================================================
  // 2. SSO Authentication & Session Management
  // =========================================================================
  async function checkAuthStatus() {
    log('AUTH', '현재 서비스 세션 쿠키(.NsqHomepage.ServiceSession) 확인 중...');
    try {
      const res = await fetch('/api/auth/user-identity', { credentials: 'include' });
      if (res.ok) {
        const data = await res.json();
        currentUser = {
          isAuthenticated: data.isAuthenticated,
          userName: data.userName || 'Admin',
          role: data.role || 'Admin'
        };
        log('AUTH', `세션 확인 성공 - 사용자: ${currentUser.userName}, 권한: ${currentUser.role}`, 'success');
      } else {
        currentUser = { isAuthenticated: false, userName: null, role: null };
        log('AUTH', '비로그인 상태 (일반 방문자 - 트랙 A 공개 조회 모드)', 'info');
      }
    } catch (err) {
      currentUser = { isAuthenticated: false, userName: null, role: null };
      log('AUTH', `세션 검증 실패: ${err.message}`, 'warn');
    }

    renderAuthUI();
  }

  function renderAuthUI() {
    if (currentUser.isAuthenticated) {
      // Logged in as Admin
      authWidget.innerHTML = `
        <div class="user-badge-wrap">
          <span class="user-role-badge">${currentUser.role}</span>
          <span class="user-name-label">${currentUser.userName}</span>
          <button id="btnLogout" class="btn btn-secondary btn-sm" style="margin-left: 8px;">
            로그아웃
          </button>
        </div>
      `;
      document.getElementById('btnLogout').addEventListener('click', handleLogout);

      sessionIndicator.className = 'status-indicator admin';
      sessionRoleText.textContent = `관리자 (${currentUser.role}) [트랙 B]`;

      // Show Admin Edit Buttons
      btnEditAbout.style.display = 'inline-flex';
      btnEditService.style.display = 'inline-flex';
      btnToggleHistoryForm.style.display = 'inline-flex';
    } else {
      // Logged out
      authWidget.innerHTML = `
        <button id="btnLogin" class="btn btn-primary btn-sm">
          <span class="icon">🔐</span> 로그인 (관리자)
        </button>
      `;
      document.getElementById('btnLogin').addEventListener('click', handleLogin);

      sessionIndicator.className = 'status-indicator';
      sessionRoleText.textContent = '비로그인 [트랙 A]';

      // Hide Admin Edit Buttons
      btnEditAbout.style.display = 'none';
      btnEditService.style.display = 'none';
      btnToggleHistoryForm.style.display = 'none';

      // Close all edit forms
      aboutEditBox.style.display = 'none';
      aboutViewBox.style.display = 'block';
      serviceEditBox.style.display = 'none';
      serviceViewBox.style.display = 'block';
      historyAddBox.style.display = 'none';
    }

    if (historyList && historyList.length > 0) {
      renderHistoryTimeline(historyList);
    }
  }

  function openLoginModal() {
    if (loginErrorMsg) {
      loginErrorMsg.style.display = 'none';
      loginErrorMsg.textContent = '';
    }
    if (inputLoginEmail) inputLoginEmail.value = 'test@company.local';
    if (inputLoginPassword) inputLoginPassword.value = 'Test1234!';
    if (loginModal) loginModal.style.display = 'flex';
    if (inputLoginEmail) inputLoginEmail.focus();
  }

  function closeLoginModal() {
    if (loginModal) loginModal.style.display = 'none';
  }

  function handleLogin() {
    log('AUTH', '[클라이언트] 로그인 팝업을 표시합니다.');
    openLoginModal();
  }

  if (loginForm) {
    loginForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const email = inputLoginEmail.value.trim();
      const password = inputLoginPassword.value;

      if (!email || !password) {
        loginErrorMsg.textContent = '이메일과 비밀번호를 모두 입력해 주세요.';
        loginErrorMsg.style.display = 'block';
        return;
      }

      loginErrorMsg.style.display = 'none';
      log('AUTH', `[클라이언트] 로그인 요청 전송: ${email} (POST /api/auth/login)`);
      try {
        const res = await fetch('/api/auth/login', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ email, password }),
          credentials: 'include'
        });

        const data = await res.json();
        if (res.ok && data.success) {
          log('AUTH', `[서비스 서버] 인증 서버(IdP) 자격증명 검증 완료 & 클라이언트 서비스 세션 발급 성공! (${data.user.userName}님)`, 'success');
          showToast('성공적으로 로그인되었습니다.', 'success');
          closeLoginModal();
          await checkAuthStatus();
          await loadAllData();
        } else {
          const errorText = data.message || '로그인에 실패했습니다. 아이디 또는 비밀번호를 확인해 주세요.';
          loginErrorMsg.textContent = errorText;
          loginErrorMsg.style.display = 'block';
          log('AUTH', `로그인 실패: ${errorText}`, 'error');
          showToast(errorText, 'error');
        }
      } catch (err) {
        loginErrorMsg.textContent = `통신 오류: ${err.message}`;
        loginErrorMsg.style.display = 'block';
        log('AUTH', `로그인 통신 오류: ${err.message}`, 'error');
      }
    });
  }

  if (btnCloseLoginModal) btnCloseLoginModal.addEventListener('click', closeLoginModal);
  if (btnCancelLoginModal) btnCancelLoginModal.addEventListener('click', closeLoginModal);
  if (loginModal) {
    loginModal.addEventListener('click', (e) => {
      if (e.target === loginModal) {
        closeLoginModal();
      }
    });
  }

  async function handleLogout() {
    log('AUTH', '서비스 세션 로그아웃 요청 전송 (POST /api/auth/logout)');
    try {
      const res = await fetch('/api/auth/logout', { method: 'POST', credentials: 'include' });
      if (res.ok) {
        log('AUTH', '서비스 세션이 파기되었습니다.', 'success');
        showToast('성공적으로 로그아웃되었습니다.', 'info');
        await checkAuthStatus();
      } else {
        log('AUTH', '로그아웃 실패', 'error');
      }
    } catch (err) {
      log('AUTH', `로그아웃 통신 오류: ${err.message}`, 'error');
    }
  }

  // =========================================================================
  // 3. Track A: Public GET Requests (회사 소개, 서비스, 연혁)
  // =========================================================================
  async function loadAbout() {
    log('트랙 A', '회사 소개 정보 조회 (GET /api/public/company-about) -> ResourceServer 대행 호출');
    try {
      const res = await fetch('/api/public/company-about');
      if (res.ok) {
        aboutData = await res.json();
        aboutContentText.textContent = aboutData.content || aboutData.introduction || '(등록된 회사 소개가 없습니다. 관리자로 로그인하여 등록하세요)';
        aboutUpdatedAt.textContent = formatDate(aboutData.updatedAt);
        log('트랙 A', '회사 소개 데이터 수신 완료 (200 OK)', 'success');
      } else {
        aboutContentText.textContent = '데이터를 불러올 수 없습니다.';
        log('트랙 A', `회사 소개 조회 실패: ${res.status}`, 'error');
      }
    } catch (err) {
      log('트랙 A', `회사 소개 통신 오류: ${err.message}`, 'error');
    }
  }

  async function loadService() {
    log('트랙 A', '회사 서비스 정보 조회 (GET /api/public/company-services) -> ResourceServer 대행 호출');
    try {
      const res = await fetch('/api/public/company-services');
      if (res.ok) {
        serviceData = await res.json();
        serviceContentText.textContent = serviceData.service || '(등록된 주요 서비스가 없습니다. 관리자로 로그인하여 등록하세요)';
        serviceUpdatedAt.textContent = formatDate(serviceData.updatedAt);
        log('트랙 A', '회사 서비스 데이터 수신 완료 (200 OK)', 'success');
      } else {
        serviceContentText.textContent = '데이터를 불러올 수 없습니다.';
        log('트랙 A', `회사 서비스 조회 실패: ${res.status}`, 'error');
      }
    } catch (err) {
      log('트랙 A', `회사 서비스 통신 오류: ${err.message}`, 'error');
    }
  }

  async function loadHistory() {
    log('트랙 A', '회사 전체 연혁 목록 조회 (GET /api/public/company-histories) -> ResourceServer 대행 호출');
    try {
      const res = await fetch('/api/public/company-histories');
      if (res.ok) {
        const data = await res.json();
        historyList = data.history || [];
        renderHistoryTimeline(historyList);
        log('트랙 A', `연혁 데이터 수신 완료 (${historyList.length}건, 200 OK)`, 'success');
      } else {
        historyTimeline.innerHTML = '<div class="timeline-empty">연혁 데이터를 불러올 수 없습니다.</div>';
        log('트랙 A', `연혁 조회 실패: ${res.status}`, 'error');
      }
    } catch (err) {
      log('트랙 A', `연혁 통신 오류: ${err.message}`, 'error');
    }
  }

  function renderHistoryTimeline(list) {
    if (!list || list.length === 0) {
      historyTimeline.innerHTML = '<div class="timeline-empty">등록된 회사 연혁이 없습니다.</div>';
      return;
    }

    const isAdmin = currentUser && currentUser.isAuthenticated && currentUser.role === 'Admin';

    historyTimeline.innerHTML = list.map(item => `
      <div class="timeline-item" style="position: relative;">
        <div style="display: flex; justify-content: space-between; align-items: center;">
          <div class="timeline-date">${item.data || item.date || '-'}</div>
          ${isAdmin && item.id ? `
            <button class="btn-delete-history btn btn-danger btn-sm" data-id="${item.id}" style="padding: 3px 8px; font-size: 12px; border-radius: 4px; background: #ef4444; color: white; border: none; cursor: pointer;">
              🗑️ 삭제
            </button>
          ` : ''}
        </div>
        <div class="timeline-content">${item.content || ''}</div>
      </div>
    `).join('');

    if (isAdmin) {
      document.querySelectorAll('.btn-delete-history').forEach(btn => {
        btn.addEventListener('click', async (e) => {
          const historyId = e.currentTarget.getAttribute('data-id');
          if (!historyId) return;

          if (!confirm(`이 연혁 항목(ID: ${historyId})을 삭제하시겠습니까?`)) {
            return;
          }

          log('트랙 B', `[Step 1] 연혁 항목 삭제 요청 (DELETE /api/admin/company-histories/${historyId})`);
          try {
            const res = await fetch(`/api/admin/company-histories/${historyId}`, {
              method: 'DELETE',
              credentials: 'include'
            });

            if (res.ok) {
              const data = await res.json();
              log('트랙 B', `[Step 2] 연혁 삭제 완료! ResourceServer DB 반영 (200 OK)`, 'success');
              showToast(data.message || '연혁이 삭제되었습니다.', 'success');
              await loadHistory();
            } else if (res.status === 401 || res.status === 302) {
              log('트랙 B', '관리자 인증이 필요합니다.', 'warn');
              showToast('관리자 권한이 필요합니다.', 'error');
              handleLogin();
            } else {
              log('트랙 B', `연혁 삭제 실패 (HTTP ${res.status})`, 'error');
              showToast('연혁 삭제에 실패했습니다.', 'error');
            }
          } catch (err) {
            log('트랙 B', `연혁 삭제 통신 오류: ${err.message}`, 'error');
          }
        });
      });
    }
  }

  async function loadAllData() {
    await Promise.all([loadAbout(), loadService(), loadHistory()]);
  }

  // =========================================================================
  // 4. Track B: Admin PUT Requests (회사 소개, 서비스, 연혁)
  // =========================================================================

  // --- About Edit/Save ---
  btnEditAbout.addEventListener('click', () => {
    inputAbout.value = aboutData.content || aboutData.introduction || '';
    aboutViewBox.style.display = 'none';
    aboutEditBox.style.display = 'block';
  });

  btnCancelAbout.addEventListener('click', () => {
    aboutEditBox.style.display = 'none';
    aboutViewBox.style.display = 'block';
  });

  btnSaveAbout.addEventListener('click', async () => {
    const newContent = inputAbout.value.trim();
    if (!newContent) {
      showToast('소개 내용을 입력해 주세요.', 'error');
      return;
    }

    log('트랙 B', '회사 소개 수정 요청 전송 (PUT /api/admin/company-about, 관리자 세션 쿠키 첨부)');
    try {
      const res = await fetch('/api/admin/company-about', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ content: newContent }),
        credentials: 'include'
      });

      if (res.ok) {
        const data = await res.json();
        aboutData = data;
        aboutContentText.textContent = data.content || data.introduction;
        aboutUpdatedAt.textContent = formatDate(data.updatedAt);
        aboutEditBox.style.display = 'none';
        aboutViewBox.style.display = 'block';
        log('트랙 B', '회사 소개 수정 성공! ResourceServer DB에 저장되었습니다. (200 OK)', 'success');
        showToast('회사 소개가 성공적으로 수정되었습니다.', 'success');
      } else if (res.status === 302 || res.status === 401) {
        log('트랙 B', '관리자 권한이 없거나 세션이 만료되었습니다. SSO 로그인이 필요합니다.', 'warn');
        showToast('관리자 인증이 필요합니다.', 'error');
        handleLogin();
      } else {
        log('트랙 B', `수정 실패 (HTTP ${res.status})`, 'error');
        showToast('수정에 실패했습니다.', 'error');
      }
    } catch (err) {
      log('트랙 B', `통신 오류: ${err.message}`, 'error');
    }
  });

  // --- Service Edit/Save ---
  btnEditService.addEventListener('click', () => {
    inputService.value = serviceData.service || '';
    serviceViewBox.style.display = 'none';
    serviceEditBox.style.display = 'block';
  });

  btnCancelService.addEventListener('click', () => {
    serviceEditBox.style.display = 'none';
    serviceViewBox.style.display = 'block';
  });

  btnSaveService.addEventListener('click', async () => {
    const newService = inputService.value.trim();
    if (!newService) {
      showToast('서비스 내용을 입력해 주세요.', 'error');
      return;
    }

    log('트랙 B', '서비스 정보 수정 요청 전송 (PUT /api/admin/company-services, 관리자 세션 쿠키 첨부)');
    try {
      const res = await fetch('/api/admin/company-services', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ service: newService }),
        credentials: 'include'
      });

      if (res.ok) {
        const data = await res.json();
        serviceData = data;
        serviceContentText.textContent = data.service;
        serviceUpdatedAt.textContent = formatDate(data.updatedAt);
        serviceEditBox.style.display = 'none';
        serviceViewBox.style.display = 'block';
        log('트랙 B', '서비스 정보 수정 성공! ResourceServer DB에 저장되었습니다. (200 OK)', 'success');
        showToast('서비스 정보가 성공적으로 수정되었습니다.', 'success');
      } else if (res.status === 302 || res.status === 401) {
        log('트랙 B', '관리자 권한이 없거나 세션이 만료되었습니다.', 'warn');
        showToast('관리자 인증이 필요합니다.', 'error');
        handleLogin();
      } else {
        log('트랙 B', `수정 실패 (HTTP ${res.status})`, 'error');
        showToast('수정에 실패했습니다.', 'error');
      }
    } catch (err) {
      log('트랙 B', `통신 오류: ${err.message}`, 'error');
    }
  });

  // --- History Add/Save ---
  btnToggleHistoryForm.addEventListener('click', () => {
    inputHistoryDate.value = new Date().toISOString().split('T')[0];
    inputHistoryContent.value = '';
    historyAddBox.style.display = historyAddBox.style.display === 'none' ? 'block' : 'none';
  });

  btnCancelHistory.addEventListener('click', () => {
    historyAddBox.style.display = 'none';
  });

  btnSaveHistory.addEventListener('click', async () => {
    const dateVal = inputHistoryDate.value;
    const contentVal = inputHistoryContent.value.trim();

    if (!dateVal || !contentVal) {
      showToast('날짜와 내용을 모두 입력해 주세요.', 'error');
      return;
    }

    log('트랙 B', `새 연혁 항목 추가 요청 (PUT /api/admin/company-histories, [${dateVal}] ${contentVal})`);
    try {
      const res = await fetch('/api/admin/company-histories', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          history: [
            { data: dateVal, content: contentVal }
          ]
        }),
        credentials: 'include'
      });

      if (res.ok) {
        const data = await res.json();
        historyList = data.history || [];
        renderHistoryTimeline(historyList);
        historyAddBox.style.display = 'none';
        log('트랙 B', '연혁 항목 저장 성공! ResourceServer DB에 반영되었습니다. (200 OK)', 'success');
        showToast('연혁 항목이 성공적으로 추가되었습니다.', 'success');
      } else if (res.status === 302 || res.status === 401) {
        log('트랙 B', '관리자 권한이 필요합니다.', 'warn');
        showToast('관리자 인증이 필요합니다.', 'error');
        handleLogin();
      } else {
        log('트랙 B', `저장 실패 (HTTP ${res.status})`, 'error');
        showToast('저장에 실패했습니다.', 'error');
      }
    } catch (err) {
      log('트랙 B', `통신 오류: ${err.message}`, 'error');
    }
  });

  // =========================================================================
  // 5. Inspector Controls
  // =========================================================================
  btnClearLogs.addEventListener('click', () => {
    logStream.innerHTML = '';
    log('SYSTEM', '로그 콘솔이 초기화되었습니다.', 'info');
  });

  btnRefreshAll.addEventListener('click', async () => {
    log('SYSTEM', '전체 데이터 및 세션 상태를 새로고침합니다...');
    await checkAuthStatus();
    await loadAllData();
    showToast('데이터를 새로고침했습니다.', 'info');
  });

  // Initial Load
  checkAuthStatus();
  loadAllData();
});
