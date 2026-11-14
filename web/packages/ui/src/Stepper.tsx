import React from 'react';

export interface StepItem {
  id: string | number;
  label: string;
}

export interface StepperProps {
  steps: StepItem[];
  currentStepIndex: number;
  className?: string;
}

export const Stepper: React.FC<StepperProps> = ({
  steps,
  currentStepIndex,
  className = '',
}) => {
  return (
    <div className={`clinical-stepper ${className}`.trim()} style={{ width: '100%' }}>
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          fontSize: '0.75rem',
          fontWeight: 600,
          marginBottom: '0.5rem',
          gap: '0.5rem',
        }}
      >
        {steps.map((step, index) => {
          const isActive = index <= currentStepIndex;
          return (
            <span
              key={step.id}
              style={{
                color: isActive ? 'var(--color-accent-teal)' : 'var(--color-text-secondary)',
                flex: 1,
                textAlign: index === 0 ? 'start' : index === steps.length - 1 ? 'end' : 'center',
              }}
            >
              {step.label}
            </span>
          );
        })}
      </div>

      <div
        style={{
          display: 'grid',
          gridTemplateColumns: `repeat(${steps.length}, 1fr)`,
          gap: '0.5rem',
          width: '100%',
        }}
      >
        {steps.map((step, index) => {
          const isCompletedOrActive = index <= currentStepIndex;
          return (
            <div
              key={step.id}
              style={{
                height: '0.375rem',
                borderRadius: 'var(--radius-pill)',
                backgroundColor: isCompletedOrActive ? 'var(--color-accent-teal)' : 'var(--color-border)',
                transition: 'background-color 0.2s ease',
              }}
            />
          );
        })}
      </div>
    </div>
  );
};
