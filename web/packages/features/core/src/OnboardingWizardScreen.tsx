import React from 'react';
import { Sparkles, Check, AlertCircle } from 'lucide-react';
import { GlassCard, GlassButton } from '@clinic/ui';

export interface SpecialtyOption {
  catalogCode: string;
  nameAr: string;
  nameEn: string;
  hasDentalPlugin: boolean;
}

export interface OnboardingWizardState {
  clinicId: string;
  name: string;
  slug: string;
  onboardingStatus: string;
  completedSteps: string[];
  street?: string | null;
  city?: string | null;
  governorate?: string | null;
  mainPhone?: string | null;
  contactEmail?: string | null;
  specialties: SpecialtyOption[];
}

export interface OnboardingWizardScreenProps {
  ownerName: string;
  state: OnboardingWizardState;
  catalog: SpecialtyOption[];
  onSaveDetails: (details: {
    name: string;
    street: string;
    city: string;
    governorate: string;
    mainPhone: string;
    contactEmail?: string;
  }) => void;
  onSaveTypes: (catalogCodes: string[], customOtherName?: string) => void;
  onSaveSlug: (slug: string) => void;
  onCompleteOnboarding: () => void;
}

export const OnboardingWizardScreen: React.FC<OnboardingWizardScreenProps> = ({
  ownerName,
  state,
  catalog,
  onSaveDetails,
  onSaveTypes,
  onSaveSlug,
  onCompleteOnboarding,
}) => {
  const getInitialStep = (): number => {
    if (!state.completedSteps.includes('welcome')) return 1;
    if (!state.completedSteps.includes('details')) return 2;
    if (!state.completedSteps.includes('types')) return 3;
    if (!state.completedSteps.includes('slug')) return 4;
    return 5;
  };

  const [step, setStep] = React.useState<number>(getInitialStep);
  const [name, setName] = React.useState(state.name);
  const [street, setStreet] = React.useState(state.street ?? '');
  const [city, setCity] = React.useState(state.city ?? '');
  const [governorate, setGovernorate] = React.useState(state.governorate ?? '');
  const [mainPhone, setMainPhone] = React.useState(state.mainPhone ?? '');
  const [contactEmail, setContactEmail] = React.useState(state.contactEmail ?? '');
  const [selectedCodes, setSelectedCodes] = React.useState<string[]>(
    state.specialties.map((s) => s.catalogCode)
  );
  const [slug, setSlug] = React.useState(state.slug);
  const [validationError, setValidationError] = React.useState<string | null>(null);

  const toggleSpecialty = (code: string) => {
    setSelectedCodes((prev) =>
      prev.includes(code) ? prev.filter((c) => c !== code) : [...prev, code]
    );
  };

  const handleDetailsNext = () => {
    if (!name.trim() || !street.trim() || !city.trim() || !governorate.trim() || !mainPhone.trim()) {
      setValidationError('جميع الحقول الأساسية مطلوبة: الاسم، الشارع، المدينة، المحافظة، ورقم الهاتف الرئيسي.');
      return;
    }
    setValidationError(null);
    onSaveDetails({ name, street, city, governorate, mainPhone, contactEmail });
    setStep(3);
  };

  const handleTypesNext = () => {
    if (selectedCodes.length === 0) {
      setValidationError('يرجى اختيار تخصص واحد على الأقل للعيادة.');
      return;
    }
    setValidationError(null);
    onSaveTypes(selectedCodes);
    setStep(4);
  };

  const handleSlugNext = () => {
    const trimmed = slug.trim().toLowerCase();
    if (!/^[a-z0-9](?:[a-z0-9-]*[a-z0-9])$/.test(trimmed) || trimmed.includes('--') || trimmed.length < 3) {
      setValidationError('الرابط العام يجب أن يحتوي على حروف إنجليزية صغيرة وأرقام وشرطة مفردة فقط (3-40 حرفًا).');
      return;
    }
    setValidationError(null);
    onSaveSlug(trimmed);
    setStep(5);
  };

  return (
    <GlassCard>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem', flexWrap: 'wrap', gap: '0.5rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <Sparkles size={20} color="var(--color-accent-teal)" />
          <h2 style={{ fontSize: '1.25rem', fontWeight: 700 }}>معالج تأسيس العيادة ({state.name})</h2>
        </div>
        <span style={{ fontSize: '0.875rem', fontWeight: 600 }}>الخطوة {step} من 5</span>
      </div>

      {validationError && (
        <div role="alert" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#ef4444', marginBottom: '1rem' }}>
          <AlertCircle size={16} />
          <span>{validationError}</span>
        </div>
      )}

      {step === 1 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <h3 style={{ fontSize: '1.125rem', fontWeight: 600 }}>مرحباً {ownerName}</h3>
          <p style={{ color: 'var(--color-text-secondary)' }}>
            سنقوم في 5 خطوات بسيطة بتجهيز بيانات عيادتك، التخصصات الطبية، والرابط العام الخاص بالحجز.
          </p>
          <div>
            <GlassButton variant="prominent" onClick={() => setStep(2)}>
              ابدأ التأسيس
            </GlassButton>
          </div>
        </div>
      )}

      {step === 2 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
          <label>
            اسم العيادة *
            <input aria-label="اسم العيادة" value={name} onChange={(e) => setName(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem' }} />
          </label>
          <label>
            العنوان (الشارع) *
            <input aria-label="العنوان" value={street} onChange={(e) => setStreet(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem' }} />
          </label>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
            <label>
              المدينة *
              <input aria-label="المدينة" value={city} onChange={(e) => setCity(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem' }} />
            </label>
            <label>
              المحافظة *
              <input aria-label="المحافظة" value={governorate} onChange={(e) => setGovernorate(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem' }} />
            </label>
          </div>
          <label>
            الهاتف الرئيسي *
            <input aria-label="الهاتف الرئيسي" value={mainPhone} onChange={(e) => setMainPhone(e.target.value)} placeholder="01012345678" style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem', direction: 'ltr' }} />
          </label>
          <label>
            بريد التواصل (اختياري)
            <input aria-label="بريد التواصل" value={contactEmail} onChange={(e) => setContactEmail(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem', direction: 'ltr' }} />
          </label>
          <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.5rem' }}>
            <GlassButton onClick={() => setStep(1)}>السابق</GlassButton>
            <GlassButton variant="prominent" onClick={handleDetailsNext}>حفظ والمتابعة</GlassButton>
          </div>
        </div>
      )}

      {step === 3 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
          <p>اختر التخصصات الطبية للعيادة:</p>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '0.5rem' }}>
            {catalog.map((item) => {
              const selected = selectedCodes.includes(item.catalogCode);
              return (
                <button
                  key={item.catalogCode}
                  type="button"
                  onClick={() => toggleSpecialty(item.catalogCode)}
                  style={{
                    padding: '0.75rem',
                    borderRadius: '0.5rem',
                    border: selected ? '2px solid var(--color-accent-teal)' : '1px solid rgba(148, 163, 184, 0.35)',
                    background: selected ? 'rgba(13, 148, 136, 0.12)' : 'transparent',
                    textAlign: 'start',
                    cursor: 'pointer',
                  }}
                >
                  <strong>{item.nameAr}</strong>
                  <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)' }}>{item.nameEn}</div>
                </button>
              );
            })}
          </div>
          <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.5rem' }}>
            <GlassButton onClick={() => setStep(2)}>السابق</GlassButton>
            <GlassButton variant="prominent" onClick={handleTypesNext}>حفظ التخصصات</GlassButton>
          </div>
        </div>
      )}

      {step === 4 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
          <label>
            الرابط العام للعيادة (Slug)
            <input aria-label="الرابط العام" value={slug} onChange={(e) => setSlug(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem', direction: 'ltr' }} />
          </label>
          <div style={{ fontSize: '0.8125rem', direction: 'ltr', color: 'var(--color-text-muted)' }}>
            معاينة الرابط: https://clinic.local/c/{slug || 'clinic-slug'}
          </div>
          <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.5rem' }}>
            <GlassButton onClick={() => setStep(3)}>السابق</GlassButton>
            <GlassButton variant="prominent" onClick={handleSlugNext}>تأكيد الرابط</GlassButton>
          </div>
        </div>
      )}

      {step === 5 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
          <h3 style={{ fontSize: '1.0625rem', fontWeight: 600 }}>مراجعة وإنهاء التأسيس</h3>
          <p><strong>اسم العيادة:</strong> {name}</p>
          <p><strong>العنوان:</strong> {street}، {city}، {governorate}</p>
          <p><strong>الرابط العام:</strong> /c/{slug}</p>
          <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.5rem' }}>
            <GlassButton onClick={() => setStep(4)}>السابق</GlassButton>
            <GlassButton variant="prominent" onClick={onCompleteOnboarding}>
              <Check size={16} />
              إنهاء التأسيس وفتح لوحة العيادة
            </GlassButton>
          </div>
        </div>
      )}
    </GlassCard>
  );
};

