import React, { useEffect, useState } from 'react';
import { Languages } from 'lucide-react';
import { GlassButton } from './GlassButton';

export const LanguageSwitcher: React.FC = () => {
  const [lang, setLang] = useState<'ar' | 'en'>('ar');

  useEffect(() => {
    const current = (document.documentElement.getAttribute('lang') || 'ar') as 'ar' | 'en';
    setLang(current);
  }, []);

  const toggle = () => {
    const next = lang === 'ar' ? 'en' : 'ar';
    setLang(next);
    document.documentElement.setAttribute('lang', next);
    document.documentElement.setAttribute('dir', next === 'ar' ? 'rtl' : 'ltr');
  };

  return (
    <GlassButton
      onClick={toggle}
      aria-label={lang === 'ar' ? 'تغيير اللغة إلى الإنجليزية' : 'Switch language to Arabic'}
    >
      <Languages size={18} />
      <span>{lang === 'ar' ? 'English' : 'العربية'}</span>
    </GlassButton>
  );
};

