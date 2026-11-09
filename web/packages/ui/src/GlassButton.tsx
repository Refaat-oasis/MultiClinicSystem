import React from 'react';

export interface GlassButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'regular' | 'default' | 'prominent';
  children: React.ReactNode;
}

export const GlassButton: React.FC<GlassButtonProps> = ({
  variant = 'regular',
  className = '',
  children,
  ...props
}) => {
  const variantClass = variant === 'prominent' ? 'glass-button-prominent' : '';
  return (
    <button className={`glass-button ${variantClass} ${className}`.trim()} {...props}>
      {children}
    </button>
  );
};

