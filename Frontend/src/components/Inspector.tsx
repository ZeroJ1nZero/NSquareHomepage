import React, { useEffect, useRef } from 'react';
import type { LogEntry } from '../types';

interface InspectorProps {
  logs: LogEntry[];
  onClear: () => void;
  onRefresh: () => void;
}

export const Inspector: React.FC<InspectorProps> = ({ logs, onClear, onRefresh }) => {
  const streamRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (streamRef.current) {
      streamRef.current.scrollTop = streamRef.current.scrollHeight;
    }
  }, [logs]);

  return (
    <div className="inspector-column">
      <div className="inspector-card">
        <div className="inspector-header">
          <div className="inspector-title-wrap">
            <span className="pulse-dot"></span>
            <h3 className="inspector-title">실시간 파이프라인 인스펙터</h3>
          </div>
          <button onClick={onClear} className="btn btn-ghost btn-xs">
            지우기
          </button>
        </div>

        <div className="inspector-sub">
          프론트엔드 ➔ API 게이트웨이(:7001) ➔ 리소스/인증 서버 간의 실시간 통신 로그입니다.
        </div>

        <div className="log-stream" ref={streamRef}>
          {logs.map((log) => (
            <div key={log.id} className={`log-entry ${log.type}`}>
              <span className="log-time">[{log.time}]</span>
              <span className="log-tag">{log.tag}</span>
              <span className="log-msg">{log.msg}</span>
            </div>
          ))}
        </div>

        <div className="inspector-actions">
          <button onClick={onRefresh} className="btn btn-secondary btn-block">
            🔄 전체 데이터 새로고침
          </button>
        </div>
      </div>
    </div>
  );
};
