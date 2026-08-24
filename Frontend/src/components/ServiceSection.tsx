import React from 'react';
import type { ServiceData } from '../types';
import { Link } from 'react-router-dom';

interface ServiceSectionProps {
  service: ServiceData;
  formatDate: (date?: string | null) => string;
}

export const ServiceSection: React.FC<ServiceSectionProps> = ({ service, formatDate }) => {
  const displayText = service.service || '(등록된 주요 서비스가 없습니다)';

  return (
    <section id="service" className="content-card">
      <div className="card-header">
        <div className="header-left">
          <span className="section-icon">⚡</span>
          <h2 className="card-title">주요 서비스 (Service)</h2>
        </div>
        <div className="header-right">
          <Link to="/service" className="btn btn-outline btn-xs">
            상세 페이지 ➔
          </Link>
        </div>
      </div>

      <div className="card-body">
        <div className="data-view-box">
          <p className="data-text">{displayText}</p>
          <div className="data-meta">
            <span className="meta-label">최종 갱신:</span>
            <span className="meta-val">{formatDate(service.updatedAt)}</span>
          </div>
        </div>
      </div>
    </section>
  );
};
