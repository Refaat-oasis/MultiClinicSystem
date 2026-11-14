import React from 'react';
import { CheckCircle2, Info, AlertTriangle, X } from 'lucide-react';

export type AlertBannerVariant = 'error' | 'success' | 'warning' | 'info';

export interface AlertBannerProps {
  variant?: AlertBannerVariant;
  message: string;
  title?: string;
  isAllergy?: boolean;
  onDismiss?: () => void;
  className?: string;
}

export const AlertBanner: React.FC<AlertBannerProps> = ({
  variant = 'info',
  message,
  title,
  isAllergy = false,
  onDismiss,
  className = '',
}) => {
  const isDanger = variant === 'error' || isAllergy;

  const variantConfig = {
    error: {
      bg: 'var(--color-status-danger-soft)',
      border: 'var(--color-status-danger)',
      color: 'var(--color-status-danger)',
      icon: <AlertTriangle size={20} color="var(--color-status-danger)" />,
    },
    success: {
      bg: 'var(--color-status-success-soft)',
      border: 'var(--color-status-success)',
      color: 'var(--color-status-success)',
      icon: <CheckCircle2 size={20} color="var(--color-status-success)" />,
    },
    warning: {
      bg: 'var(--color-status-warning-soft)',
      border: 'var(--color-status-warning)',
      color: 'var(--color-status-warning)',
      icon: <AlertTriangle size={20} color="var(--color-status-warning)" />,
    },
    info: {
      bg: 'var(--color-status-info-soft)',
      border: 'var(--color-status-info)',
      color: 'var(--color-status-info)',
      icon: <Info size={20} color="var(--color-status-info)" />,
    },
  }[variant];

  return (
    <div
      role="alert"
      className={`${isAllergy ? 'allergy-alert-banner' : ''} ${className}`.trim()}
      style={{
        display: 'flex',
        alignItems: 'flex-start',
        gap: '0.75rem',
        padding: '0.875rem 1.25rem',
        borderRadius: 'var(--radius-card)',
        backgroundColor: variantConfig.bg,
        border: `1px solid ${variantConfig.border}`,
        color: variantConfig.color,
        width: '100%',
        boxShadow: isDanger ? '0 1px 4px rgba(220, 38, 38, 0.15)' : 'none',
      }}
    >
      <div style={{ flexShrink: 0, marginTop: '0.125rem' }}>
        {variantConfig.icon}
      </div>

      <div style={{ flex: 1, minWidth: 0 }}>
        {title && (
          <div style={{ fontWeight: 700, fontSize: '0.9375rem', marginBottom: '0.25rem', color: variantConfig.color }}>
            {title}
          </div>
        )}
        <div style={{ fontSize: '0.875rem', lineHeight: 1.5, color: 'var(--color-text-primary)' }}>
          {message}
        </div>
      </div>

      {!isAllergy && onDismiss && (
        <button
          onClick={onDismiss}
          aria-label="Dismiss alert"
          style={{
            background: 'transparent',
            border: 'none',
            color: 'inherit',
            opacity: 0.7,
            cursor: 'pointer',
            padding: '0.25rem',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            borderRadius: '4px',
          }}
        >
          <X size={16} />
        </button>
      )}
    </div>
  );
};
