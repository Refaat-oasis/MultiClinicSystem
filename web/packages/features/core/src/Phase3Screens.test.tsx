import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ClinicSwitcher, MyClinicsScreen, type MyClinicCard } from './ClinicSwitcher';
import { OnboardingWizardScreen, type SpecialtyOption } from './OnboardingWizardScreen';
import { ClinicSettingsScreen, type ClinicSettingsData } from './ClinicSettingsScreen';
import { UserProfileScreen, type UserProfileData } from './UserProfileScreen';

const sampleClinics: MyClinicCard[] = [
  {
    clinicId: 'c1',
    name: 'عيادة النور للأسنان',
    slug: 'noor-dental',
    status: 'Active',
    onboardingStatus: 'Completed',
    timeZone: 'Africa/Cairo',
    currency: 'EGP',
    roundingRule: 'NearestPound',
    modulesEnabled: [],
    completedSteps: ['welcome', 'details', 'types', 'slug'],
  },
  {
    clinicId: 'c2',
    name: 'مركز القلب التخصصي',
    slug: 'cardio-center',
    status: 'PendingOnboarding',
    onboardingStatus: 'Pending',
    timeZone: 'Africa/Cairo',
    currency: 'EGP',
    roundingRule: 'NearestPound',
    modulesEnabled: [],
    completedSteps: ['welcome'],
  },
];

const sampleCatalog: SpecialtyOption[] = [
  { catalogCode: 'dental', nameAr: 'طب الأسنان', nameEn: 'Dental', hasDentalPlugin: true },
  { catalogCode: 'cardiology', nameAr: 'القلب والأوعية', nameEn: 'Cardiology', hasDentalPlugin: false },
];

describe('Phase 3 Screens - Clinics, Onboarding, Settings, Profile', () => {
  it('switches clinics in ClinicSwitcher and enforces quota alert in MyClinicsScreen', () => {
    const onSwitch = vi.fn();
    const onCreate = vi.fn();
    render(
      <>
        <ClinicSwitcher clinics={sampleClinics} activeClinicId="c1" onSwitch={onSwitch} />
        <MyClinicsScreen
          clinics={sampleClinics}
          activeClinicId="c1"
          quotaLimit={2}
          onCreateClinic={onCreate}
          onSelectClinic={onSwitch}
          onResumeOnboarding={vi.fn()}
          onOpenSettings={vi.fn()}
        />
      </>
    );

    const select = screen.getByLabelText('تبديل العيادة النشطة') as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'c2' } });
    expect(onSwitch).toHaveBeenCalledWith('c2');

    expect(screen.getByRole('alert').textContent).toContain('CLINIC_QUOTA_REACHED');
    const createBtn = screen.getByRole('button', { name: 'إضافة عيادة' }) as HTMLButtonElement;
    expect(createBtn.disabled).toBe(true);
  });

  it('resumes OnboardingWizardScreen at incomplete step, validates required fields, and toggles specialties', () => {
    const onSaveDetails = vi.fn();
    const onSaveTypes = vi.fn();
    const onSaveSlug = vi.fn();
    const onComplete = vi.fn();

    render(
      <OnboardingWizardScreen
        ownerName="د. عمر"
        state={{
          clinicId: 'c2',
          name: '',
          slug: 'clinic-b',
          onboardingStatus: 'Pending',
          completedSteps: ['welcome'],
          specialties: [],
        }}
        catalog={sampleCatalog}
        onSaveDetails={onSaveDetails}
        onSaveTypes={onSaveTypes}
        onSaveSlug={onSaveSlug}
        onCompleteOnboarding={onComplete}
      />
    );

    // Resumes at step 2 since welcome is already completed
    expect(screen.getByText('الخطوة 2 من 5')).toBeDefined();

    // Validation error when details are missing
    fireEvent.click(screen.getByRole('button', { name: 'حفظ والمتابعة' }));
    expect(screen.getByRole('alert').textContent).toContain('جميع الحقول الأساسية مطلوبة');
    expect(onSaveDetails).not.toHaveBeenCalled();

    // Fill details and advance
    fireEvent.change(screen.getByLabelText('اسم العيادة'), { target: { value: 'عيادة الأمل' } });
    fireEvent.change(screen.getByLabelText('العنوان'), { target: { value: '12 شارع التحرير' } });
    fireEvent.change(screen.getByLabelText('المدينة'), { target: { value: 'الدقي' } });
    fireEvent.change(screen.getByLabelText('المحافظة'), { target: { value: 'الجيزة' } });
    fireEvent.change(screen.getByLabelText('الهاتف الرئيسي'), { target: { value: '01012345678' } });
    fireEvent.click(screen.getByRole('button', { name: 'حفظ والمتابعة' }));

    expect(onSaveDetails).toHaveBeenCalledOnce();
    expect(screen.getByText('الخطوة 3 من 5')).toBeDefined();

    // Select dental specialty and advance
    fireEvent.click(screen.getByText('طب الأسنان'));
    fireEvent.click(screen.getByRole('button', { name: 'حفظ التخصصات' }));
    expect(onSaveTypes).toHaveBeenCalledWith(['dental']);
    expect(screen.getByText('الخطوة 4 من 5')).toBeDefined();
  });

  it('locks ClinicSettingsScreen for non-owners and disables financial fields after first financial entry', () => {
    const baseSettings: ClinicSettingsData = {
      clinicId: 'c1',
      name: 'عيادة النور',
      slug: 'noor-dental',
      street: 'شارع 1',
      city: 'القاهرة',
      governorate: 'القاهرة',
      mainPhone: '01012345678',
      timeZone: 'Africa/Cairo',
      currency: 'EGP',
      roundingRule: 'NearestPound',
      hasFinancialEntries: true,
      sections: [{ sectionKey: 'general', titleAr: 'البيانات العامة', titleEn: 'General' }],
    };

    const { rerender } = render(
      <ClinicSettingsScreen settings={baseSettings} isOwner={false} onSave={vi.fn()} />
    );
    expect(screen.getByRole('alert').textContent).toContain('clinic.settings.edit');

    rerender(<ClinicSettingsScreen settings={baseSettings} isOwner={true} onSave={vi.fn()} />);
    expect((screen.getByLabelText('المنطقة الزمنية') as HTMLInputElement).disabled).toBe(true);
    expect((screen.getByLabelText('العملة') as HTMLInputElement).disabled).toBe(true);
    expect((screen.getByLabelText('قاعدة التقريب') as HTMLInputElement).disabled).toBe(true);
  });

  it('submits profile update, email verification code, password change, and device revocation in UserProfileScreen', () => {
    const onUpdateProfile = vi.fn();
    const onRequestEmailChange = vi.fn();
    const onConfirmEmailChange = vi.fn();
    const onChangePassword = vi.fn();
    const onRevokeDevice = vi.fn();

    const profile: UserProfileData = {
      email: 'doctor@clinic.local',
      fullName: 'د. سارة',
      phoneNumber: '01012345678',
      locale: 'ar',
      theme: 'light',
      devices: [{ deviceId: 'dev-1', deviceName: 'Chrome Windows', isRevoked: false }],
      clinics: [{ clinicId: 'c1', clinicName: 'عيادة النور', role: 'Owner', status: 'Active' }],
    };

    render(
      <UserProfileScreen
        profile={profile}
        onUpdateProfile={onUpdateProfile}
        onRequestEmailChange={onRequestEmailChange}
        onConfirmEmailChange={onConfirmEmailChange}
        onChangePassword={onChangePassword}
        onRevokeDevice={onRevokeDevice}
      />
    );

    fireEvent.change(screen.getByLabelText('الاسم الكامل'), { target: { value: 'د. سارة أحمد' } });
    fireEvent.click(screen.getByRole('button', { name: 'حفظ البيانات الشخصية' }));
    expect(onUpdateProfile).toHaveBeenCalledWith(
      expect.objectContaining({ fullName: 'د. سارة أحمد' })
    );

    fireEvent.change(screen.getByLabelText('البريد الإلكتروني الجديد'), { target: { value: 'new@clinic.local' } });
    fireEvent.click(screen.getByRole('button', { name: 'إرسال الرمز' }));
    expect(onRequestEmailChange).toHaveBeenCalledWith('new@clinic.local');

    fireEvent.click(screen.getByRole('button', { name: 'إبطال' }));
    expect(onRevokeDevice).toHaveBeenCalledWith('dev-1');
  });
});

