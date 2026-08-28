import React, { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import * as api from '../api';

export const LoginPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const returnUrl = searchParams.get('returnUrl') || '/';

  useEffect(() => {
    const fullReturnUrl = returnUrl.startsWith('http')
      ? returnUrl
      : window.location.origin + (returnUrl.startsWith('/') ? returnUrl : `/${returnUrl}`);
    api.startSso(fullReturnUrl, 'none');
  }, [returnUrl]);

  return (
    <main className="main-content" style={{ minHeight: 'calc(100vh - 72px)', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '40px 16px' }}>
      <div className="login-page-card" style={{ maxWidth: '480px', width: '100%', background: '#ffffff', borderRadius: '16px', border: '1px solid #e2e8f0', boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)', padding: '36px', textAlign: 'center' }}>
        <div style={{ display: 'inline-flex', width: '56px', height: '56px', background: 'linear-gradient(135deg, #1e40af, #2563eb)', color: 'white', borderRadius: '14px', alignItems: 'center', justifyContent: 'center', fontSize: '26px', marginBottom: '14px', boxShadow: '0 8px 16px rgba(37, 99, 235, 0.25)' }}>
          🔐
        </div>
        <h2 style={{ fontSize: '20px', fontWeight: '800', color: '#0f172a', marginBottom: '8px' }}>
          SSO 통합 인증 페이지로 이동 중...
        </h2>
        <p style={{ fontSize: '14px', color: '#64748b' }}>
          잠시만 기다려 주세요. 인증 서버(AuthServer)로 자동 연결됩니다.
        </p>
      </div>
    </main>
  );
};
