import React from 'react';

export type GlassButtonVariant = 'regular' | 'default' | 'prominent' | 'primary' | 'secondary' | 'soft' | 'danger';

export interface GlassButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: GlassButtonVariant;
  children: React.ReactNode;
}

export const GlassButton: React.FC<GlassButtonProps> = ({
  variant = 'regular',
  className = '',
  children,
  ...props
}) => {
  let variantClass = '';
  if (variant === 'prominent' || variant === 'primary') {
    variantClass = 'glass-button-prominent';
  } else if (variant === 'soft') {
    variantClass = 'glass-button-soft';
  } else if (variant === 'danger') {
    variantClass = 'glass-button-danger';
  }

  return (
    <button className={`glass-button ${variantClass} ${className}`.trim()} {...props}>
      {children}
    </button>
  );
};
