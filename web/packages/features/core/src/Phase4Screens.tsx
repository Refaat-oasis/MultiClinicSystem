import React, { useState } from 'react';
import { GlassCard, GlassButton, GlassInput, AlertBanner } from '@clinic/ui';
import {
  Blocks,
  Shield,
  Users,
  Laptop,
  DoorOpen,
  Stethoscope,
  CalendarClock,
  Hash,
  BarChart3,
  Building2,
  FileSearch,
  AlertTriangle,
  Copy,
  CheckCircle2,
} from 'lucide-react';

export interface ClinicModuleItem {
  moduleKey: string;
  titleAr: string;
  titleEn: string;
  descriptionAr: string;
  descriptionEn: string;
  enabled: boolean;
  prerequisites: string[];
  missingPrerequisites: string[];
  enabledDependents: string[];
}

export interface ModuleMenuItem {
  itemKey: string;
  moduleKey: string;
  titleAr: string;
  titleEn: string;
  route: string;
  requiredPermission: string;
  order: number;
}

export interface ClinicRoleItem {
  roleId: string;
  name: string;
  nameAr: string;
  nameEn: string;
  isSystemOwner: boolean;
  isEditable: boolean;
  assignedUsersCount: number;
  permissions: string[];
  effectivePermissions: string[];
  dormantPermissions: string[];
}

export interface PermissionDefItem {
  key: string;
  moduleKey: string;
  group: string;
  titleAr: string;
  titleEn: string;
  isReserved: boolean;
}

export interface PermissionOverrideItem {
  permissionKey: string;
  effect: 'Grant' | 'Deny';
}

export interface StaffMemberItem {
  userId: string;
  fullName: string;
  email: string;
  roleName: string;
  isOwner: boolean;
  isActive: boolean;
  effectivePermissions: string[];
  dormantPermissions: string[];
  overrides: PermissionOverrideItem[];
}

export interface ClinicDeviceItem {
  id: string;
  userId: string;
  staffName: string;
  staffEmail: string;
  deviceId: string;
  deviceName: string;
  isApproved: boolean;
  isRevoked: boolean;
}

export interface ClinicRoomItem {
  id: string;
  roomCode: string;
  nameAr: string;
  nameEn: string;
  specialtyCode?: string | null;
  floorOrLocation?: string | null;
  isActive: boolean;
}

export interface ClinicDoctorItem {
  id: string;
  fullName: string;
  titleAr: string;
  titleEn: string;
  specialtyCodes: string[];
  assignedRoomIds: string[];
  slotDurationMinutes: number;
  isActive: boolean;
}

export interface WorkingHourItem {
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  isClosed: boolean;
}

export interface DayOffItem {
  id: string;
  startDate: string;
  endDate: string;
  reasonAr: string;
  reasonEn: string;
}

export interface NumberBlockItem {
  id: string;
  deviceId: string;
  kind: 'Receipt' | 'PatientFile';
  fromNumber: number;
  toNumber: number;
  nextNumber: number;
  status: string;
}

export interface DashboardWidgetTile {
  widgetKey: string;
  moduleKey: string;
  titleAr: string;
  titleEn: string;
  primaryValue?: number;
  unitLabel?: string;
  errorMessage?: string;
}

export interface OwnerPortfolioClinicCard {
  clinicId: string;
  name: string;
  slug: string;
  status: string;
  staffCount: number;
  roomsCount: number;
  doctorsCount: number;
  enabledModulesCount: number;
}

export interface OwnerPortfolioData {
  ownerId: string;
  totalClinics: number;
  activeClinics: number;
  totalStaffAcrossClinics: number;
  totalDoctorsAcrossClinics: number;
  clinics: OwnerPortfolioClinicCard[];
}

export interface ClinicAuditEntry {
  id: string;
  action: string;
  entityName: string;
  entityId?: string | null;
  timestampUtc: string;
}

const DAY_NAMES_AR = ['الأحد', 'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];

// 1. My Features Screen (Read-Only Enabled Modules & Dynamic Menu)
export interface MyFeaturesScreenProps {
  modules: ClinicModuleItem[];
  menuItems: ModuleMenuItem[];
  effectivePermissions: string[];
}

export const MyFeaturesScreen: React.FC<MyFeaturesScreenProps> = ({
  modules,
  menuItems,
  effectivePermissions,
}) => {
  const enabledCount = modules.filter((m) => m.enabled).length;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <Blocks size={22} color="var(--color-accent-teal)" />
            <div>
              <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>الميزات المفعّلة للعيادة (My Features)</h2>
              <p style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)' }}>
                يتم تفعيل أو إيقاف الوحدات بواسطة مشرف المنصة فقط — عرض للقراءة فقط ({enabledCount} / {modules.length})
              </p>
            </div>
          </div>
          <span
            style={{
              paddingInline: '0.75rem',
              paddingBlock: '0.25rem',
              borderRadius: '999px',
              fontSize: '0.8125rem',
              background: 'rgba(13, 148, 136, 0.12)',
              color: 'var(--color-accent-teal)',
              fontWeight: 600,
            }}
          >
            الصلاحيات الفعالة: {effectivePermissions.length}
          </span>
        </div>

        {menuItems.length > 0 && (
          <div style={{ marginTop: '1rem', display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            {menuItems.map((mi) => (
              <span
                key={mi.itemKey}
                style={{
                  paddingInline: '0.625rem',
                  paddingBlock: '0.25rem',
                  borderRadius: '0.5rem',
                  background: 'var(--glass-surface)',
                  fontSize: '0.8125rem',
                }}
              >
                {mi.titleAr} ({mi.route})
              </span>
            ))}
          </div>
        )}
      </GlassCard>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: '1rem' }}>
        {modules.map((m) => (
          <GlassCard key={m.moduleKey}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
              <strong>{m.titleAr}</strong>
              <span
                style={{
                  fontSize: '0.75rem',
                  paddingInline: '0.5rem',
                  paddingBlock: '0.125rem',
                  borderRadius: '999px',
                  background: m.enabled ? 'rgba(16, 185, 129, 0.16)' : 'rgba(148, 163, 184, 0.2)',
                  color: m.enabled ? '#059669' : 'var(--color-text-muted)',
                  fontWeight: 600,
                }}
              >
                {m.enabled ? 'مفعّل' : 'غير مفعّل'}
              </span>
            </div>
            <p style={{ fontSize: '0.8125rem', color: 'var(--color-text-secondary)', marginBottom: '0.5rem' }}>
              {m.descriptionAr}
            </p>
            {m.prerequisites.length > 0 && (
              <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)' }}>
                المتطلبات: {m.prerequisites.join(', ')}
              </div>
            )}
          </GlassCard>
        ))}
      </div>
    </div>
  );
};

// 2. Roles & Permissions Screen (Clone, Matrix, Dormant Badges, Overrides)
export interface RolesPermissionsScreenProps {
  roles: ClinicRoleItem[];
  catalog: PermissionDefItem[];
  staff: StaffMemberItem[];
  onCloneRole: (sourceRoleId: string, newName: string) => void;
  onUpdateRolePermissions: (roleId: string, permissions: string[]) => void;
  onSetUserOverrides: (staffUserId: string, overrides: PermissionOverrideItem[]) => void;
  feedback?: { variant: 'success' | 'error'; message: string } | null;
}

export const RolesPermissionsScreen: React.FC<RolesPermissionsScreenProps> = ({
  roles,
  catalog,
  staff,
  onCloneRole,
  onUpdateRolePermissions,
  onSetUserOverrides,
  feedback,
}) => {
  const [selectedRoleId, setSelectedRoleId] = useState<string>(roles[0]?.roleId ?? '');
  const [cloneName, setCloneName] = useState('');
  const [selectedStaffId, setSelectedStaffId] = useState<string>(staff.find((s) => !s.isOwner)?.userId ?? staff[0]?.userId ?? '');
  const [overrideKey, setOverrideKey] = useState(catalog[0]?.key ?? 'billing.discount');
  const [overrideEffect, setOverrideEffect] = useState<'Grant' | 'Deny'>('Grant');

  const selectedRole = roles.find((r) => r.roleId === selectedRoleId) ?? roles[0];
  const selectedStaff = staff.find((s) => s.userId === selectedStaffId) ?? staff[0];

  const handleTogglePermission = (permKey: string) => {
    if (!selectedRole || !selectedRole.isEditable) return;
    const exists = selectedRole.permissions.includes(permKey);
    const next = exists
      ? selectedRole.permissions.filter((p) => p !== permKey)
      : [...selectedRole.permissions, permKey];
    onUpdateRolePermissions(selectedRole.roleId, next);
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      {feedback && <AlertBanner variant={feedback.variant} message={feedback.message} />}

      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
          <Shield size={22} color="var(--color-accent-teal)" />
          <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>الأدوار ومصفوفة الصلاحيات (Roles & Claims)</h2>
        </div>

        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '1rem' }}>
          {roles.map((r) => (
            <GlassButton
              key={r.roleId}
              variant={selectedRole?.roleId === r.roleId ? 'prominent' : 'default'}
              onClick={() => setSelectedRoleId(r.roleId)}
            >
              <span>{r.nameAr || r.name}</span>
              <span style={{ fontSize: '0.75rem', opacity: 0.8 }}>({r.assignedUsersCount})</span>
            </GlassButton>
          ))}
        </div>

        {selectedRole && (
          <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '1.25rem' }}>
            <div style={{ flex: '1 1 220px' }}>
              <GlassInput
                label="اسم الدور الجديد عند النسخ (Clone Role Name)"
                value={cloneName}
                onChange={(e) => setCloneName(e.target.value)}
                placeholder="مثال: Senior Receptionist"
              />
            </div>
            <GlassButton
              onClick={() => {
                if (cloneName.trim()) {
                  onCloneRole(selectedRole.roleId, cloneName.trim());
                  setCloneName('');
                }
              }}
              disabled={!cloneName.trim()}
            >
              <Copy size={16} />
              <span>نسخ الدور المحدد (Clone)</span>
            </GlassButton>
          </div>
        )}

        {selectedRole && (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: '0.75rem' }}>
            {catalog.map((perm) => {
              const checked = selectedRole.permissions.includes(perm.key);
              const isDormant = selectedRole.dormantPermissions.includes(perm.key);
              const disabled = !selectedRole.isEditable || perm.isReserved;
              return (
                <label
                  key={perm.key}
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    gap: '0.5rem',
                    padding: '0.625rem 0.75rem',
                    borderRadius: '0.5rem',
                    background: 'var(--glass-surface)',
                    opacity: disabled && !checked ? 0.65 : 1,
                    cursor: disabled ? 'not-allowed' : 'pointer',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <input
                      type="checkbox"
                      checked={checked}
                      disabled={disabled}
                      onChange={() => handleTogglePermission(perm.key)}
                    />
                    <div>
                      <div style={{ fontSize: '0.875rem', fontWeight: 600 }}>{perm.titleAr}</div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)' }}>
                        {perm.key} • {perm.moduleKey}
                      </div>
                    </div>
                  </div>
                  <div style={{ display: 'flex', gap: '0.25rem' }}>
                    {perm.isReserved && (
                      <span style={{ fontSize: '0.7rem', padding: '2px 6px', borderRadius: '4px', background: 'rgba(239, 68, 68, 0.15)', color: '#dc2626' }}>
                        للمالك فقط
                      </span>
                    )}
                    {isDormant && (
                      <span style={{ fontSize: '0.7rem', padding: '2px 6px', borderRadius: '4px', background: 'rgba(245, 158, 11, 0.18)', color: '#d97706' }}>
                        خاملة (Dormant)
                      </span>
                    )}
                  </div>
                </label>
              );
            })}
          </div>
        )}
      </GlassCard>

      <GlassCard>
        <h3 style={{ fontSize: '1rem', fontWeight: 700, marginBottom: '0.75rem' }}>
          استثناءات المستخدم الفردية (Grant / Deny — المنع يغلب دائماً)
        </h3>
        {staff.length === 0 ? (
          <p style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>لا يوجد طاقم مسجل بعد.</p>
        ) : (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '0.75rem', alignItems: 'end' }}>
            <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.875rem' }}>
              <span>عضو الفريق</span>
              <select
                value={selectedStaff?.userId ?? ''}
                onChange={(e) => setSelectedStaffId(e.target.value)}
                style={{ padding: '0.5rem', borderRadius: '0.5rem', border: '1px solid rgba(148,163,184,0.3)', background: 'var(--glass-surface)' }}
              >
                {staff.map((s) => (
                  <option key={s.userId} value={s.userId}>
                    {s.fullName} ({s.roleName})
                  </option>
                ))}
              </select>
            </label>

            <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.875rem' }}>
              <span>الصلاحية</span>
              <select
                value={overrideKey}
                onChange={(e) => setOverrideKey(e.target.value)}
                style={{ padding: '0.5rem', borderRadius: '0.5rem', border: '1px solid rgba(148,163,184,0.3)', background: 'var(--glass-surface)' }}
              >
                {catalog.map((c) => (
                  <option key={c.key} value={c.key}>
                    {c.titleAr} ({c.key})
                  </option>
                ))}
              </select>
            </label>

            <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.875rem' }}>
              <span>الأثر (Effect)</span>
              <select
                value={overrideEffect}
                onChange={(e) => setOverrideEffect(e.target.value as 'Grant' | 'Deny')}
                style={{ padding: '0.5rem', borderRadius: '0.5rem', border: '1px solid rgba(148,163,184,0.3)', background: 'var(--glass-surface)' }}
              >
                <option value="Grant">منح (Grant)</option>
                <option value="Deny">منع (Deny Wins)</option>
              </select>
            </label>

            <GlassButton
              variant="prominent"
              onClick={() => {
                if (!selectedStaff) return;
                const filtered = selectedStaff.overrides.filter((o) => o.permissionKey !== overrideKey);
                onSetUserOverrides(selectedStaff.userId, [...filtered, { permissionKey: overrideKey, effect: overrideEffect }]);
              }}
            >
              حفظ الاستثناء
            </GlassButton>
          </div>
        )}
      </GlassCard>
    </div>
  );
};

// 3. Staff & Device Approval Screen
export interface StaffDevicesScreenProps {
  staff: StaffMemberItem[];
  roles: ClinicRoleItem[];
  devices: ClinicDeviceItem[];
  onInviteStaff: (payload: { fullName: string; email: string; roleName: string }) => void;
  onApproveDevice: (deviceId: string) => void;
}

export const StaffDevicesScreen: React.FC<StaffDevicesScreenProps> = ({
  staff,
  roles,
  devices,
  onInviteStaff,
  onApproveDevice,
}) => {
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [roleName, setRoleName] = useState(roles.find((r) => !r.isSystemOwner)?.name ?? 'Receptionist');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!fullName.trim() || !email.trim()) return;
    onInviteStaff({ fullName: fullName.trim(), email: email.trim(), roleName });
    setFullName('');
    setEmail('');
  };

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1.25rem' }}>
      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
          <Users size={22} color="var(--color-accent-teal)" />
          <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>دعوة موظف جديد (Invite Staff)</h2>
        </div>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
          <GlassInput label="الاسم الكامل للموظف" value={fullName} onChange={(e) => setFullName(e.target.value)} required />
          <GlassInput label="البريد الإلكتروني" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.875rem' }}>
            <span>الدور الوظيفي</span>
            <select
              value={roleName}
              onChange={(e) => setRoleName(e.target.value)}
              style={{ padding: '0.5rem', borderRadius: '0.5rem', border: '1px solid rgba(148,163,184,0.3)', background: 'var(--glass-surface)' }}
            >
              {roles.filter((r) => !r.isSystemOwner).map((r) => (
                <option key={r.roleId} value={r.name}>
                  {r.nameAr || r.name}
                </option>
              ))}
            </select>
          </label>
          <GlassButton type="submit" variant="prominent">
            إرسال كود التفعيل للموظف
          </GlassButton>
        </form>

        <div style={{ marginTop: '1rem', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
          {staff.map((m) => (
            <div
              key={m.userId}
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                padding: '0.5rem 0.75rem',
                borderRadius: '0.5rem',
                background: 'var(--glass-surface)',
              }}
            >
              <div>
                <strong>{m.fullName}</strong>
                <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)' }}>{m.email}</div>
              </div>
              <span style={{ fontSize: '0.8125rem', color: 'var(--color-accent-teal)', fontWeight: 600 }}>{m.roleName}</span>
            </div>
          ))}
        </div>
      </GlassCard>

      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
          <Laptop size={22} color="var(--color-accent-teal)" />
          <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>أجهزة الطاقم واعتمادها (Device Approval)</h2>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.625rem' }}>
          {devices.length === 0 ? (
            <p style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>لا توجد أجهزة مسجلة بعد.</p>
          ) : (
            devices.map((d) => (
              <div
                key={d.id}
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  gap: '0.5rem',
                  padding: '0.625rem 0.75rem',
                  borderRadius: '0.5rem',
                  background: 'var(--glass-surface)',
                }}
              >
                <div>
                  <div style={{ fontWeight: 600 }}>{d.staffName} — {d.deviceName}</div>
                  <div style={{ fontSize: '0.75rem', color: 'var(--color-text-muted)' }}>
                    {d.deviceId} • {d.isApproved ? 'معتمد' : 'بانتظار موافقة المالك'}
                  </div>
                </div>
                {!d.isApproved && !d.isRevoked ? (
                  <GlassButton variant="prominent" onClick={() => onApproveDevice(d.deviceId)}>
                    <CheckCircle2 size={16} />
                    <span>اعتماد الجهاز</span>
                  </GlassButton>
                ) : (
                  <span style={{ fontSize: '0.75rem', color: d.isRevoked ? '#dc2626' : '#059669', fontWeight: 600 }}>
                    {d.isRevoked ? 'ملغى' : 'نشط ومعتمد'}
                  </span>
                )}
              </div>
            ))
          )}
        </div>
      </GlassCard>
    </div>
  );
};

// 4. Clinic Structure Screen (Rooms, Doctors, Hours, Days Off, Number Blocks)
export interface ClinicStructureScreenProps {
  rooms: ClinicRoomItem[];
  doctors: ClinicDoctorItem[];
  workingHours: WorkingHourItem[];
  daysOff: DayOffItem[];
  numberBlocks: NumberBlockItem[];
  onCreateRoom: (payload: { code: string; nameAr: string; nameEn: string }) => void;
  onCreateDoctor: (payload: { fullName: string; titleAr: string; slotDurationMinutes: number }) => void;
  onSaveDefaultHours: (items: WorkingHourItem[]) => void;
  onCreateDayOff: (payload: { startDate: string; endDate: string; reasonAr: string }) => void;
  onReserveBlock: (kind: 'Receipt' | 'PatientFile') => void;
}

export const ClinicStructureScreen: React.FC<ClinicStructureScreenProps> = ({
  rooms,
  doctors,
  workingHours,
  daysOff,
  numberBlocks,
  onCreateRoom,
  onCreateDoctor,
  onSaveDefaultHours,
  onCreateDayOff,
  onReserveBlock,
}) => {
  const [roomCode, setRoomCode] = useState('');
  const [roomNameAr, setRoomNameAr] = useState('');
  const [doctorName, setDoctorName] = useState('');
  const [doctorTitle, setDoctorTitle] = useState('أخصائي');
  const [dayOffStart, setDayOffStart] = useState('2026-12-25');
  const [dayOffEnd, setDayOffEnd] = useState('2026-12-26');
  const [dayOffReason, setDayOffReason] = useState('إجازة رسمية');

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1.25rem' }}>
      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
          <DoorOpen size={20} color="var(--color-accent-teal)" />
          <h2 style={{ fontSize: '1.0625rem', fontWeight: 700 }}>غرف الكشف (Rooms)</h2>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', marginBottom: '0.75rem' }}>
          <GlassInput label="كود الغرفة" value={roomCode} onChange={(e) => setRoomCode(e.target.value)} placeholder="R-101" />
          <GlassInput label="اسم الغرفة" value={roomNameAr} onChange={(e) => setRoomNameAr(e.target.value)} placeholder="غرفة الأسنان 1" />
          <GlassButton
            variant="prominent"
            onClick={() => {
              if (roomCode.trim() && roomNameAr.trim()) {
                onCreateRoom({ code: roomCode.trim(), nameAr: roomNameAr.trim(), nameEn: roomNameAr.trim() });
                setRoomCode('');
                setRoomNameAr('');
              }
            }}
          >
            إضافة غرفة
          </GlassButton>
        </div>
        {rooms.map((r) => (
          <div key={r.id} style={{ padding: '0.5rem', borderRadius: '0.5rem', background: 'var(--glass-surface)', marginBottom: '0.375rem', fontSize: '0.875rem' }}>
            {r.roomCode} — {r.nameAr}
          </div>
        ))}
      </GlassCard>

      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
          <Stethoscope size={20} color="var(--color-accent-teal)" />
          <h2 style={{ fontSize: '1.0625rem', fontWeight: 700 }}>الأطباء ومدة الكشف (Doctors)</h2>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', marginBottom: '0.75rem' }}>
          <GlassInput label="اسم الطبيب" value={doctorName} onChange={(e) => setDoctorName(e.target.value)} placeholder="د. سارة محمود" />
          <GlassInput label="التخصص / المسمى" value={doctorTitle} onChange={(e) => setDoctorTitle(e.target.value)} />
          <GlassButton
            variant="prominent"
            onClick={() => {
              if (doctorName.trim()) {
                onCreateDoctor({ fullName: doctorName.trim(), titleAr: doctorTitle.trim(), slotDurationMinutes: 20 });
                setDoctorName('');
              }
            }}
          >
            إضافة طبيب (20 دقيقة)
          </GlassButton>
        </div>
        {doctors.map((d) => (
          <div key={d.id} style={{ padding: '0.5rem', borderRadius: '0.5rem', background: 'var(--glass-surface)', marginBottom: '0.375rem', fontSize: '0.875rem' }}>
            {d.fullName} ({d.titleAr}) — {d.slotDurationMinutes} د
          </div>
        ))}
      </GlassCard>

      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
          <CalendarClock size={20} color="var(--color-accent-teal)" />
          <h2 style={{ fontSize: '1.0625rem', fontWeight: 700 }}>ساعات العمل والإجازات (Hours & Days Off)</h2>
        </div>
        <GlassButton
          onClick={() =>
            onSaveDefaultHours([
              { dayOfWeek: 0, startTime: '09:00', endTime: '17:00', isClosed: false },
              { dayOfWeek: 1, startTime: '09:00', endTime: '17:00', isClosed: false },
              { dayOfWeek: 5, startTime: '00:00', endTime: '00:00', isClosed: true },
            ])
          }
        >
          حفظ جدول دوام قياسي (9:00 - 17:00)
        </GlassButton>
        <div style={{ marginTop: '0.5rem', fontSize: '0.8125rem', color: 'var(--color-text-secondary)' }}>
          {workingHours.map((h) => (
            <div key={h.dayOfWeek}>
              {DAY_NAMES_AR[h.dayOfWeek] ?? h.dayOfWeek}: {h.isClosed ? 'مغلق' : `${h.startTime} - ${h.endTime}`}
            </div>
          ))}
        </div>

        <div style={{ marginTop: '1rem', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
          <GlassInput label="من تاريخ" value={dayOffStart} onChange={(e) => setDayOffStart(e.target.value)} />
          <GlassInput label="إلى تاريخ" value={dayOffEnd} onChange={(e) => setDayOffEnd(e.target.value)} />
          <GlassInput label="سبب الإجازة" value={dayOffReason} onChange={(e) => setDayOffReason(e.target.value)} />
          <GlassButton onClick={() => onCreateDayOff({ startDate: dayOffStart, endDate: dayOffEnd, reasonAr: dayOffReason })}>
            إضافة إجازة
          </GlassButton>
          {daysOff.map((off) => (
            <div key={off.id} style={{ fontSize: '0.8125rem', padding: '0.375rem', borderRadius: '0.375rem', background: 'var(--glass-surface)' }}>
              {off.startDate} → {off.endDate} ({off.reasonAr})
            </div>
          ))}
        </div>
      </GlassCard>

      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
          <Hash size={20} color="var(--color-accent-teal)" />
          <h2 style={{ fontSize: '1.0625rem', fontWeight: 700 }}>نطاقات الأرقام المتسلسلة (Number Blocks)</h2>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
          <GlassButton variant="prominent" onClick={() => onReserveBlock('Receipt')}>
            حجز 500 رقم إيصال
          </GlassButton>
          <GlassButton onClick={() => onReserveBlock('PatientFile')}>
            حجز 500 رقم ملف مريض
          </GlassButton>
        </div>
        {numberBlocks.map((b) => (
          <div key={b.id} style={{ padding: '0.5rem', borderRadius: '0.5rem', background: 'var(--glass-surface)', marginBottom: '0.375rem', fontSize: '0.8125rem' }}>
            {b.kind}: {b.fromNumber} – {b.toNumber} (التالي: {b.nextNumber}) • {b.status}
          </div>
        ))}
      </GlassCard>
    </div>
  );
};

// 5. Modular Dashboard Widgets, Owner Portfolio & Clinic Audit Viewer
export interface OwnerPortfolioAndWidgetsScreenProps {
  widgets: DashboardWidgetTile[];
  portfolio: OwnerPortfolioData;
  auditLogs: ClinicAuditEntry[];
}

export const OwnerPortfolioAndWidgetsScreen: React.FC<OwnerPortfolioAndWidgetsScreenProps> = ({
  widgets,
  portfolio,
  auditLogs,
}) => {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
          <BarChart3 size={22} color="var(--color-accent-teal)" />
          <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>مؤشرات العيادة الذكية (Modular Widgets)</h2>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '0.75rem' }}>
          {widgets.map((w) => (
            <div
              key={w.widgetKey}
              style={{
                padding: '1rem',
                borderRadius: '0.75rem',
                background: w.errorMessage ? 'rgba(239, 68, 68, 0.08)' : 'var(--glass-surface)',
                border: w.errorMessage ? '1px solid rgba(239, 68, 68, 0.35)' : '1px solid rgba(255, 255, 255, 0.16)',
              }}
            >
              <div style={{ fontSize: '0.875rem', fontWeight: 600, marginBottom: '0.375rem' }}>{w.titleAr}</div>
              {w.errorMessage ? (
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', color: '#dc2626', fontSize: '0.8125rem' }}>
                  <AlertTriangle size={16} />
                  <span>تعذر تحميل المؤشر: {w.errorMessage}</span>
                </div>
              ) : (
                <div style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--color-accent-teal)' }}>
                  {w.primaryValue ?? 0}{' '}
                  <span style={{ fontSize: '0.8125rem', fontWeight: 400, color: 'var(--color-text-muted)' }}>
                    {w.unitLabel ?? 'count'}
                  </span>
                </div>
              )}
            </div>
          ))}
        </div>
      </GlassCard>

      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
          <Building2 size={22} color="var(--color-accent-teal)" />
          <h2 style={{ fontSize: '1.125rem', fontWeight: 700 }}>محفظة المالك متعددة الفروع (Owner Portfolio — إجماليات فقط)</h2>
        </div>
        <div style={{ display: 'flex', gap: '1.5rem', flexWrap: 'wrap', marginBottom: '1rem', fontSize: '0.9375rem' }}>
          <span>إجمالي العيادات: <strong>{portfolio.totalClinics}</strong></span>
          <span>النشطة: <strong>{portfolio.activeClinics}</strong></span>
          <span>الطاقم: <strong>{portfolio.totalStaffAcrossClinics}</strong></span>
          <span>الأطباء: <strong>{portfolio.totalDoctorsAcrossClinics}</strong></span>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '0.75rem' }}>
          {portfolio.clinics.map((c) => (
            <div key={c.clinicId} style={{ padding: '0.75rem', borderRadius: '0.625rem', background: 'var(--glass-surface)' }}>
              <div style={{ fontWeight: 700 }}>{c.name}</div>
              <div style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)' }}>
                طاقم: {c.staffCount} • غرف: {c.roomsCount} • أطباء: {c.doctorsCount} • ميزات: {c.enabledModulesCount}
              </div>
            </div>
          ))}
        </div>
      </GlassCard>

      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
          <FileSearch size={20} color="var(--color-accent-teal)" />
          <h3 style={{ fontSize: '1rem', fontWeight: 700 }}>سجل تدقيق العيادة (Clinic Audit Log)</h3>
        </div>
        {auditLogs.length === 0 ? (
          <p style={{ fontSize: '0.875rem', color: 'var(--color-text-muted)' }}>لا توجد أحداث مسجلة بعد.</p>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.375rem' }}>
            {auditLogs.map((log) => (
              <div key={log.id} style={{ fontSize: '0.8125rem', padding: '0.5rem', borderRadius: '0.375rem', background: 'var(--glass-surface)' }}>
                <strong>{log.action}</strong> — {log.entityName} ({new Date(log.timestampUtc).toUTCString()})
              </div>
            ))}
          </div>
        )}
      </GlassCard>
    </div>
  );
};

