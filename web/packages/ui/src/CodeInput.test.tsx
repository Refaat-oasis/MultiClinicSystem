import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CodeInput } from './CodeInput';

describe('CodeInput Component', () => {
  it('renders 6 separate digit inputs with proper aria labels', () => {
    render(<CodeInput value="" onChange={() => {}} />);
    const inputs = screen.getAllByRole('textbox');
    expect(inputs).toHaveLength(6);
    expect(inputs[0].getAttribute('aria-label')).toBe('Digit 1 of 6');
  });

  it('displays provided digits in the corresponding input boxes', () => {
    render(<CodeInput value="123456" onChange={() => {}} />);
    const inputs = screen.getAllByRole('textbox') as HTMLInputElement[];
    expect(inputs[0].value).toBe('1');
    expect(inputs[1].value).toBe('2');
    expect(inputs[5].value).toBe('6');
  });

  it('calls onChange when user types a digit', async () => {
    const handleChange = vi.fn();
    const user = userEvent.setup();
    render(<CodeInput value="" onChange={handleChange} />);

    const inputs = screen.getAllByRole('textbox');
    await user.type(inputs[0], '5');

    expect(handleChange).toHaveBeenCalledWith('5');
  });
});

