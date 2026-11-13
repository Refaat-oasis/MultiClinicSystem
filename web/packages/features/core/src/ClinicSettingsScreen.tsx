import React from 'react';
import { Settings, Lock, ShieldAlert } from 'lucide-react';
import { GlassCard, GlassButton } from '@clinic/ui';

export interface ClinicSettingsData {
  clinicId: string;
  name: string;
  slug: string;
  street: string;
  city: string;
  governorate: string;
  mainPhone: string;
  contactEmail?: string | null;
  timeZone: string;
  currency: string;
  roundingRule: string;
  hasFinancialEntries: boolean;
  sections: { sectionKey: string; titleAr: string; titleEn: string }[];
}

export interface ClinicSettingsScreenProps {
  settings: ClinicSettingsData;
  isOwner: boolean;
  onSave: (payload: {
    name: string;
    street: string;
    city: string;
    governorate: string;
    mainPhone: string;
    slug: string;
    timeZone: string;
    currency: string;
    roundingRule: string;
  }) => void;
}

export const ClinicSettingsScreen: React.FC<ClinicSettingsScreenProps> = ({
  settings,
  isOwner,
  onSave,
}) => {
  const [name, setName] = React.useState(settings.name);
  const [street, setStreet] = React.useState(settings.street);
  const [city, setCity] = React.useState(settings.city);
  const [governorate, setGovernorate] = React.useState(settings.governorate);
  const [mainPhone, setMainPhone] = React.useState(settings.mainPhone);
  const [slug, setSlug] = React.useState(settings.slug);
  const [timeZone, setTimeZone] = React.useState(settings.timeZone);
  const [currency, setCurrency] = React.useState(settings.currency);
  const [roundingRule, setRoundingRule] = React.useState(settings.roundingRule);

  if (!isOwner) {
    return (
      <GlassCard>
        <div role="alert" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#d97706' }}>
          <ShieldAlert size={20} />
          <span>إعدادات العيادة متاحة لمالك العيادة فقط (clinic.settings.edit).</span>
        </div>
      </GlassCard>
    );
  }

  return (
    <GlassCard>
      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
        <Settings size={20} color="var(--color-accent-teal)" />
        <h2 style={{ fontSize: '1.25rem', fontWeight: 700 }}>إعدادات العيادة</h2>
      </div>

      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1rem', flexWrap: 'wrap' }}>
        {settings.sections.map((sec) => (
          <span
            key={sec.sectionKey}
            style={{
              fontSize: '0.8125rem',
              padding: '0.25rem 0.625rem',
              borderRadius: '0.375rem',
              backgroundColor: 'rgba(13, 148, 136, 0.12)',
              color: 'var(--color-accent-teal)',
              fontWeight: 600,
            }}
          >
            {sec.titleAr}
          </span>
        ))}
      </div>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          onSave({ name, street, city, governorate, mainPhone, slug, timeZone, currency, roundingRule });
        }}
        style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}
      >
        <label>
          اسم العيادة
          <input aria-label="اسم العيادة في الإعدادات" value={name} onChange={(e) => setName(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem' }} />
        </label>
        <label>
          الرابط العام (Slug)
          <input aria-label="الرابط العام في الإعدادات" value={slug} onChange={(e) => setSlug(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem', direction: 'ltr' }} />
        </label>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '0.5rem' }}>
          <label>
            الشارع
            <input aria-label="الشارع" value={street} onChange={(e) => setStreet(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem' }} />
          </label>
          <label>
            المدينة
            <input aria-label="المدينة في الإعدادات" value={city} onChange={(e) => setCity(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem' }} />
          </label>
          <label>
            المحافظة
            <input aria-label="المحافظة في الإعدادات" value={governorate} onChange={(e) => setGovernorate(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem' }} />
          </label>
        </div>
        <label>
          الهاتف الرئيسي
          <input aria-label="الهاتف الرئيسي في الإعدادات" value={mainPhone} onChange={(e) => setMainPhone(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem', direction: 'ltr' }} />
        </label>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '0.5rem' }}>
          <label>
            المنطقة الزمنية
            <input aria-label="المنطقة الزمنية" value={timeZone} onChange={(e) => setTimeZone(e.target.value)} disabled={settings.hasFinancialEntries} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem', direction: 'ltr' }} />
          </label>
          <label>
            العملة
            <input aria-label="العملة" value={currency} onChange={(e) => setCurrency(e.target.value)} disabled={settings.hasFinancialEntries} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem', direction: 'ltr' }} />
          </label>
          <label>
            قاعدة التقريب
            <input aria-label="قاعدة التقريب" value={roundingRule} onChange={(e) => setRoundingRule(e.target.value)} disabled={settings.hasFinancialEntries} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem', direction: 'ltr' }} />
          </label>
        </div>

        {settings.hasFinancialEntries && (
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', fontSize: '0.8125rem', color: '#d97706' }}>
            <Lock size={14} />
            <span>المنطقة الزمنية والعملة وقاعدة التقريب مقفلة بعد تسجيل أول معاملة مالية.</span>
          </div>
        )}

        <div>
          <GlassButton type="submit" variant="prominent">حفظ الإعدادات</GlassButton>
        </div>
      </form>
    </GlassCard>
  );
};

