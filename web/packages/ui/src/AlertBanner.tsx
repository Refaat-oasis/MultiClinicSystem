import React from 'react';
import { AlertCircle, CheckCircle2, Info, AlertTriangle } from 'lucide-react';

export type AlertBannerVariant = 'error' | 'success' | 'warning' | 'info';

export interface AlertBannerProps {
  variant?: AlertBannerVariant;
  message: string;
  title?: string;
  onDismiss?: () => void;
}

export const AlertBanner: React.FC<AlertBannerProps> = ({
  variant = 'info',
  message,
  title,
  onDismiss,
}) => {
  const variantConfig = {
    error: {
      bg: 'rgba(239, 68, 68, 0.1)',
      border: 'rgba(239, 68, 68, 0.25)',
      color: '#ef4444',
      icon: <AlertCircle size={20} color="#ef4444" />,
    },
    success: {
      bg: 'rgba(16, 185, 129, 0.1)',
      border: 'rgba(16, 185, 129, 0.25)',
      color: '#10b981',
      icon: <CheckCircle2 size={20} color="#10b981" />,
    },
    warning: {
      bg: 'rgba(245, 158, 11, 0.1)',
      border: 'rgba(245, 158, 11, 0.25)',
      color: '#f59e0b',
      icon: <AlertTriangle size={20} color="#f59e0b" />,
    },
    info: {
      bg: 'rgba(13, 148, 136, 0.1)',
      border: 'rgba(13, 148, 136, 0.25)',
      color: 'var(--color-accent-teal)',
      icon: <Info size={20} color="var(--color-accent-teal)" />,
    },
  }[variant];

  return (
    <div
      role="alert"
      style={{
        display: 'flex',
        alignItems: 'flex-start',
        gap: '0.75rem',
        padding: '0.875rem 1rem',
        borderRadius: '0.75rem',
        backgroundColor: variantConfig.bg,
        border: `1px solid ${variantConfig.border}`,
        backdropFilter: 'var(--glass-blur)',
        WebkitBackdropFilter: 'var(--glass-blur)',
        color: 'var(--color-text-primary)',
        width: '100%',
        textAlign: 'start',
      }}
    >
      <div style={{ flexShrink: 0, marginTop: '2px' }}>{variantConfig.icon}</div>
      <div style={{ flexGrow: 1 }}>
        {title && (
          <div style={{ fontWeight: 600, fontSize: '0.9375rem', marginBottom: '0.25rem', color: variantConfig.color }}>
            {title}
          </div>
        )}
        <div style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)', lineHeight: 1.5 }}>
          {message}
        </div>
      </div>
      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          aria-label="إغلاق التنبيه"
          style={{
            background: 'none',
            border: 'none',
            cursor: 'pointer',
            color: 'var(--color-text-muted)',
            padding: '2px',
          }}
        >
          ✕
        </button>
      )}
    </div>
  );
};
