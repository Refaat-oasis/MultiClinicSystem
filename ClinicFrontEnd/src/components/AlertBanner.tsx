import React from 'react';
import { AlertCircle, AlertTriangle, CheckCircle2, Info } from 'lucide-react';

export type AlertType = 'info' | 'warning' | 'danger' | 'success';

export interface AlertBannerProps {
  type?: AlertType;
  title?: string;
  message: string;
  isAllergy?: boolean;
}

export const AlertBanner: React.FC<AlertBannerProps> = ({
  type = 'info',
  title,
  message,
  isAllergy = false,
}) => {
  if (isAllergy) {
    return (
      <div className="clinic-allergy-banner" role="alert">
        <AlertTriangle size={22} color="#dc2626" style={{ flexShrink: 0 }} />
        <div>
          <div style={{ fontSize: '0.9375rem', fontWeight: 700, color: '#991b1b' }}>
            {title || 'تنبيه حساسية دوائية حرجة (CRITICAL ALLERGY ALERT)'}
          </div>
          <div style={{ fontSize: '0.8125rem', color: '#b91c1c', marginTop: '0.125rem' }}>
            {message}
          </div>
        </div>
      </div>
    );
  }

  const icons: Record<AlertType, React.ReactNode> = {
    info: <Info size={18} color="var(--clinic-status-info)" />,
    warning: <AlertTriangle size={18} color="var(--clinic-status-warning)" />,
    danger: <AlertCircle size={18} color="var(--clinic-status-danger)" />,
    success: <CheckCircle2 size={18} color="var(--clinic-status-success)" />,
  };

  return (
    <div
      className={`clinic-chip clinic-chip-${type}`}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: '0.625rem',
        padding: '0.75rem 1rem',
        borderRadius: 'var(--clinic-radius-input)',
        width: '100%',
        marginBottom: '1rem',
      }}
      role="alert"
    >
      {icons[type]}
      <div style={{ flex: 1, fontSize: '0.875rem' }}>
        {title && <strong style={{ display: 'block', marginBottom: '0.125rem' }}>{title}</strong>}
        <span>{message}</span>
      </div>
    </div>
  );
};
