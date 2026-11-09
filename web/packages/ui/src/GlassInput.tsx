import React from 'react';

export interface GlassInputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  icon?: React.ReactNode;
}

export const GlassInput = React.forwardRef<HTMLInputElement, GlassInputProps>(
  ({ label, error, icon, id, style, className = '', ...props }, ref) => {
    const inputId = id || (label ? `input-${label.replace(/\s+/g, '-').toLowerCase()}` : undefined);
    const errorId = error && inputId ? `${inputId}-error` : undefined;

    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.375rem', width: '100%' }}>
        {label && (
          <label
            htmlFor={inputId}
            style={{
              fontSize: '0.875rem',
              fontWeight: 500,
              color: 'var(--color-text-secondary)',
              textAlign: 'start',
            }}
          >
            {label}
          </label>
        )}

        <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
          {icon && (
            <div
              style={{
                position: 'absolute',
                insetInlineStart: '0.875rem',
                display: 'flex',
                alignItems: 'center',
                pointerEvents: 'none',
                color: 'var(--color-text-muted)',
              }}
            >
              {icon}
            </div>
          )}

          <input
            ref={ref}
            id={inputId}
            aria-invalid={Boolean(error)}
            aria-describedby={errorId}
            className={`glass-input ${className}`}
            style={{
              width: '100%',
              minHeight: '2.75rem',
              paddingBlock: '0.625rem',
              paddingInlineStart: icon ? '2.5rem' : '1rem',
              paddingInlineEnd: '1rem',
              fontSize: '0.9375rem',
              color: 'var(--color-text-primary)',
              background: 'var(--glass-surface)',
              border: `1px solid ${error ? '#ef4444' : 'var(--glass-border)'}`,
              borderRadius: '0.75rem',
              backdropFilter: 'var(--glass-blur)',
              WebkitBackdropFilter: 'var(--glass-blur)',
              boxShadow: error ? '0 0 0 1px #ef4444' : 'var(--glass-shadow)',
              outline: 'none',
              transition: 'border-color 0.15s ease, box-shadow 0.15s ease',
              textAlign: 'start',
              ...style,
            }}
            {...props}
          />
        </div>

        {error && (
          <span
            id={errorId}
            role="alert"
            style={{
              fontSize: '0.8125rem',
              color: '#ef4444',
              textAlign: 'start',
            }}
          >
            {error}
          </span>
        )}
      </div>
    );
  }
);

GlassInput.displayName = 'GlassInput';

