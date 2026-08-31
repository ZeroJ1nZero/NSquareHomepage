import React, { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

const API_BASE = 'https://localhost:7001/api';

export const CallbackPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [status, setStatus] = useState<string>('인증 서버(IdP) 응답 검증 중...');
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    const processCallback = async () => {
      const code = searchParams.get('code');
      const state = searchParams.get('state');
      const error = searchParams.get('error');
      const errorDescription = searchParams.get('error_description');

      if (error) {
        if (isMounted) setErrorMsg(`${error}: ${errorDescription || '인증이 취소되었거나 실패했습니다.'}`);
        return;
      }

      if (!code) {
        if (isMounted) setErrorMsg('인증 서버로부터 인가 코드를 수신하지 못했습니다.');
        return;
      }

      try {
        // Step 05: 클라이언트-서비스서버 간 state 무결성(CSRF) 검증
        if (state) {
          if (isMounted) setStatus('CSRF State 무결성 검증 중 (Step 05)...');
          try {
            await fetch(`${API_BASE}/auth/verify-state?state=${encodeURIComponent(state)}`, {
              credentials: 'include',
            });
          } catch (e) {
            console.warn('State verification warning:', e);
          }
        }

        // Step 06: 서버 간 백채널 PKCE 토큰 교환 및 기본 세션 수립
        if (isMounted) setStatus('보안 세션 수립 및 백채널 토큰 검증 중 (Step 06)...');

        const exchangeRes = await fetch(`${API_BASE}/auth/validate-pkce`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({
            code: code,
            redirectUri: 'http://localhost:3000/callback',
          }),
        });

        if (!exchangeRes.ok) {
          const errData = await exchangeRes.json().catch(() => ({}));
          throw new Error(errData.message || '인증 서버와의 토큰 교환에 실패했습니다.');
        }

        // 대상 서비스 및 복귀 목적지 URL 확인
        const storedService = sessionStorage.getItem('sso_target_service') || undefined;
        const storedReturnUrl = sessionStorage.getItem('sso_return_url') || undefined;

        // Step 08: 서비스별 세션 쿠키 발급이 필요한 경우 호출 (토큰 없이 세션 메모리 기반 발급)
        if (storedService && storedService !== 'none') {
          const serviceName = storedService === 'about' ? '회사 소개' : storedService === 'service' ? '주요 서비스' : storedService === 'history' ? '회사 연혁' : '전역';
          if (isMounted) setStatus(`${serviceName} 서비스 세션 쿠키 발급 중 (Step 08)...`);

          await fetch(`${API_BASE}/auth/oidc-callback`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({
              service: storedService,
            }),
          });
        }

        let targetUrl = storedReturnUrl || '/';

        // 세션스토리지 정리
        sessionStorage.removeItem('sso_return_url');
        sessionStorage.removeItem('sso_target_service');

        if (isMounted) setStatus('인증 완료! 요청하신 페이지로 이동합니다...');
        setTimeout(() => {
          if (targetUrl.startsWith('http://') || targetUrl.startsWith('https://')) {
            window.location.href = targetUrl;
          } else {
            window.location.href = window.location.origin + (targetUrl.startsWith('/') ? targetUrl : `/${targetUrl}`);
          }
        }, 200);
      } catch (err: any) {
        if (isMounted) setErrorMsg(err.message || '인증 처리 중 오류가 발생했습니다.');
      }
    };

    processCallback();

    return () => {
      isMounted = false;
    };
  }, [searchParams, navigate]);

  return (
    <main className="main-content" style={{ minHeight: 'calc(100vh - 72px)', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '40px 16px' }}>
      <div className="login-page-card" style={{ maxWidth: '480px', width: '100%', background: '#ffffff', borderRadius: '16px', border: '1px solid #e2e8f0', boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)', padding: '36px', textAlign: 'center' }}>
        <div style={{ display: 'inline-flex', width: '56px', height: '56px', background: errorMsg ? 'linear-gradient(135deg, #ef4444, #dc2626)' : 'linear-gradient(135deg, #1e40af, #2563eb)', color: 'white', borderRadius: '14px', alignItems: 'center', justifyContent: 'center', fontSize: '26px', marginBottom: '14px', boxShadow: errorMsg ? '0 8px 16px rgba(239, 68, 68, 0.25)' : '0 8px 16px rgba(37, 99, 235, 0.25)' }}>
          {errorMsg ? '⚠️' : '🔐'}
        </div>
        <h2 style={{ fontSize: '20px', fontWeight: '800', color: errorMsg ? '#dc2626' : '#0f172a', marginBottom: '8px' }}>
          {errorMsg ? '인증 처리 오류' : 'SSO 인증 완료 처리 중...'}
        </h2>
        <p style={{ fontSize: '14px', color: errorMsg ? '#ef4444' : '#64748b', marginBottom: errorMsg ? '20px' : '0' }}>
          {errorMsg || status}
        </p>
        {errorMsg && (
          <button
            onClick={() => { window.location.href = '/'; }}
            style={{ padding: '10px 20px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '8px', cursor: 'pointer', fontWeight: 600 }}
          >
            홈으로 돌아가기
          </button>
        )}
      </div>
    </main>
  );
};
