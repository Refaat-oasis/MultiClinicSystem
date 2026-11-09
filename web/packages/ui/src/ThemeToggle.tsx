import React, { useEffect, useState } from 'react';
import { Sun, Moon } from 'lucide-react';
import { GlassButton } from './GlassButton';

export const ThemeToggle: React.FC = () => {
  const [theme, setTheme] = useState<'light' | 'dark'>('light');

  useEffect(() => {
    const current = document.documentElement.getAttribute('data-theme') || 'light';
    setTheme(current as 'light' | 'dark');
  }, []);

  const toggle = () => {
    const next = theme === 'light' ? 'dark' : 'light';
    setTheme(next);
    document.documentElement.setAttribute('data-theme', next);
  };

  return (
    <GlassButton
      onClick={toggle}
      aria-label={theme === 'light' ? 'Switch to Dark Theme' : 'Switch to Light Theme'}
    >
      {theme === 'light' ? <Moon size={18} /> : <Sun size={18} />}
    </GlassButton>
  );
};

