import React from 'react';
import { User, Mail, KeyRound, Laptop } from 'lucide-react';
import { GlassCard, GlassButton, CodeInput } from '@clinic/ui';

export interface UserProfileData {
  email: string;
  fullName: string;
  phoneNumber?: string | null;
  locale: string;
  theme: string;
  devices: { deviceId: string; deviceName: string; isRevoked: boolean }[];
  clinics: { clinicId: string; clinicName: string; role: string; status: string }[];
}

export interface UserProfileScreenProps {
  profile: UserProfileData;
  onUpdateProfile: (payload: { fullName: string; phoneNumber?: string; locale: string; theme: string }) => void;
  onRequestEmailChange: (newEmail: string) => void;
  onConfirmEmailChange: (code: string) => void;
  onChangePassword: (payload: { currentPassword: string; newPassword: string; signOutOtherDevices: boolean }) => void;
  onRevokeDevice: (deviceId: string) => void;
}

export const UserProfileScreen: React.FC<UserProfileScreenProps> = ({
  profile,
  onUpdateProfile,
  onRequestEmailChange,
  onConfirmEmailChange,
  onChangePassword,
  onRevokeDevice,
}) => {
  const [fullName, setFullName] = React.useState(profile.fullName);
  const [phoneNumber, setPhoneNumber] = React.useState(profile.phoneNumber ?? '');
  const [locale, setLocale] = React.useState(profile.locale);
  const [theme, setTheme] = React.useState(profile.theme);

  const [newEmail, setNewEmail] = React.useState('');
  const [emailCode, setEmailCode] = React.useState('');

  const [currentPassword, setCurrentPassword] = React.useState('');
  const [newPassword, setNewPassword] = React.useState('');
  const [signOutOthers, setSignOutOthers] = React.useState(true);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
          <User size={20} color="var(--color-accent-teal)" />
          <h2 style={{ fontSize: '1.25rem', fontWeight: 700 }}>الملف الشخصي</h2>
        </div>

        <form
          onSubmit={(e) => {
            e.preventDefault();
            onUpdateProfile({ fullName, phoneNumber, locale, theme });
          }}
          style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}
        >
          <label>
            الاسم الكامل
            <input aria-label="الاسم الكامل" value={fullName} onChange={(e) => setFullName(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem' }} />
          </label>
          <label>
            رقم الهاتف
            <input aria-label="رقم هاتف الملف الشخصي" value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem', direction: 'ltr' }} />
          </label>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem' }}>
            <label>
              اللغة
              <select aria-label="اللغة" value={locale} onChange={(e) => setLocale(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem' }}>
                <option value="ar">العربية</option>
                <option value="en">English</option>
              </select>
            </label>
            <label>
              المظهر
              <select aria-label="المظهر" value={theme} onChange={(e) => setTheme(e.target.value)} style={{ display: 'block', width: '100%', padding: '0.5rem', marginTop: '0.25rem' }}>
                <option value="light">فاتح</option>
                <option value="dark">داكن</option>
                <option value="system">النظام</option>
              </select>
            </label>
          </div>
          <div>
            <GlassButton type="submit" variant="prominent">حفظ البيانات الشخصية</GlassButton>
          </div>
        </form>
      </GlassCard>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: '1rem' }}>
        <GlassCard>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
            <Mail size={18} color="var(--color-accent-teal)" />
            <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>تغيير البريد الإلكتروني</h3>
          </div>
          <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', marginBottom: '0.5rem' }}>
            البريد الحالي: <span style={{ direction: 'ltr', display: 'inline-block' }}>{profile.email}</span>
          </p>
          <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.75rem' }}>
            <input aria-label="البريد الإلكتروني الجديد" value={newEmail} onChange={(e) => setNewEmail(e.target.value)} placeholder="new@clinic.local" style={{ flex: 1, padding: '0.5rem', direction: 'ltr' }} />
            <GlassButton onClick={() => onRequestEmailChange(newEmail)}>إرسال الرمز</GlassButton>
          </div>
          <CodeInput value={emailCode} onChange={setEmailCode} />
          <div style={{ marginTop: '0.75rem' }}>
            <GlassButton variant="prominent" onClick={() => onConfirmEmailChange(emailCode)}>تأكيد تغيير البريد</GlassButton>
          </div>
        </GlassCard>

        <GlassCard>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
            <KeyRound size={18} color="var(--color-accent-teal)" />
            <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>تغيير كلمة المرور</h3>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            <input type="password" aria-label="كلمة المرور الحالية" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} placeholder="كلمة المرور الحالية" style={{ padding: '0.5rem', direction: 'ltr' }} />
            <input type="password" aria-label="كلمة المرور الجديدة" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} placeholder="كلمة المرور الجديدة" style={{ padding: '0.5rem', direction: 'ltr' }} />
            <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.8125rem' }}>
              <input type="checkbox" checked={signOutOthers} onChange={(e) => setSignOutOthers(e.target.checked)} />
              تسجيل الخروج من جميع الأجهزة الأخرى
            </label>
            <div>
              <GlassButton variant="prominent" onClick={() => onChangePassword({ currentPassword, newPassword, signOutOtherDevices: signOutOthers })}>
                تحديث كلمة المرور
              </GlassButton>
            </div>
          </div>
        </GlassCard>
      </div>

      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
          <Laptop size={18} color="var(--color-accent-teal)" />
          <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>الأجهزة والعضويات (للقراءة فقط)</h3>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: '1rem' }}>
          <div>
            <strong>الأجهزة النشطة:</strong>
            <ul style={{ listStyle: 'none', marginTop: '0.5rem', display: 'flex', flexDirection: 'column', gap: '0.375rem' }}>
              {profile.devices.map((d) => (
                <li key={d.deviceId} style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.875rem' }}>
                  <span>{d.deviceName} {d.isRevoked ? '(ملغي)' : ''}</span>
                  {!d.isRevoked && (
                    <GlassButton onClick={() => onRevokeDevice(d.deviceId)}>إبطال</GlassButton>
                  )}
                </li>
              ))}
            </ul>
          </div>
          <div>
            <strong>العيادات المنتمي إليها:</strong>
            <ul style={{ listStyle: 'none', marginTop: '0.5rem', display: 'flex', flexDirection: 'column', gap: '0.375rem' }}>
              {profile.clinics.map((c) => (
                <li key={c.clinicId} style={{ fontSize: '0.875rem' }}>
                  {c.clinicName} — {c.role} ({c.status})
                </li>
              ))}
            </ul>
          </div>
        </div>
      </GlassCard>
    </div>
  );
};

