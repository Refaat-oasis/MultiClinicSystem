import React, { useState } from 'react';
import { GlassCard, GlassButton, AlertBanner } from '@clinic/ui';
import { apiClient } from '@clinic/api-client';
import { Smartphone, Laptop, Trash2, Shield, CheckCircle2 } from 'lucide-react';

export interface DeviceInfo {
  deviceId: string;
  deviceName: string;
  deviceType?: string;
  lastIpAddress?: string;
  lastActiveUtc?: string;
  isCurrentDevice?: boolean;
}

export interface DeviceManagementScreenProps {
  devices?: DeviceInfo[];
  onDeviceRevoked?: (deviceId: string) => void;
}

export const DeviceManagementScreen: React.FC<DeviceManagementScreenProps> = ({
  devices: initialDevices,
  onDeviceRevoked,
}) => {
  const currentDeviceId = `web-${window.navigator.userAgent.replace(/[^a-zA-Z0-9]/g, '').slice(0, 16)}`;
  const [devices, setDevices] = useState<DeviceInfo[]>(
    initialDevices ?? [
      {
        deviceId: currentDeviceId,
        deviceName: 'المتصفح الحالي (Chrome - Windows)',
        deviceType: 'Desktop',
        lastIpAddress: '127.0.0.1',
        lastActiveUtc: new Date().toISOString(),
        isCurrentDevice: true,
      },
    ]
  );

  const [revokingId, setRevokingId] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleRevoke = async (deviceId: string) => {
    setRevokingId(deviceId);
    setErrorMessage(null);
    setSuccessMessage(null);

    try {
      await apiClient.post('/api/v1/auth/revoke-device', { deviceId });
      setDevices((prev) => prev.filter((d) => d.deviceId !== deviceId));
      setSuccessMessage('تم إلغاء صلاحية الجهاز بنجاح وإنهاء جلسته النشطة');
      onDeviceRevoked?.(deviceId);
    } catch (err: unknown) {
      const problem = err as { detail?: string; title?: string };
      setErrorMessage(problem.detail || problem.title || 'تعذر إلغاء الجهاز في الوقت الحالي');
    } finally {
      setRevokingId(null);
    }
  };

  return (
    <div style={{ maxWidth: '680px', marginInline: 'auto', width: '100%' }}>
      <GlassCard>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '1.5rem' }}>
          <div
            style={{
              padding: '0.625rem',
              borderRadius: '0.75rem',
              backgroundColor: 'rgba(13, 148, 136, 0.12)',
              color: 'var(--color-accent-teal)',
            }}
          >
            <Shield size={24} />
          </div>
          <div>
            <h2 style={{ fontSize: '1.25rem', fontWeight: 700, color: 'var(--color-text-primary)' }}>
              إدارة الأجهزة والجلسات النشطة
            </h2>
            <p style={{ fontSize: '0.875rem', color: 'var(--color-text-secondary)' }}>
              قائمة بالأجهزة المعتمدة المصرح لها بالوصول إلى حسابك في المنظومة
            </p>
          </div>
        </div>

        {errorMessage && (
          <div style={{ marginBottom: '1.25rem' }}>
            <AlertBanner variant="error" message={errorMessage} onDismiss={() => setErrorMessage(null)} />
          </div>
        )}

        {successMessage && (
          <div style={{ marginBottom: '1.25rem' }}>
            <AlertBanner variant="success" message={successMessage} onDismiss={() => setSuccessMessage(null)} />
          </div>
        )}

        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.875rem' }}>
          {devices.map((device) => {
            const isDesktop = device.deviceType?.toLowerCase().includes('desktop') || true;
            return (
              <div
                key={device.deviceId}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '1rem',
                  borderRadius: '0.75rem',
                  backgroundColor: 'var(--glass-surface-prominent)',
                  border: '1px solid var(--glass-border)',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.875rem' }}>
                  <div style={{ color: 'var(--color-accent-teal)' }}>
                    {isDesktop ? <Laptop size={28} /> : <Smartphone size={28} />}
                  </div>
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                      <span style={{ fontWeight: 600, fontSize: '0.9375rem' }}>{device.deviceName}</span>
                      {device.isCurrentDevice && (
                        <span
                          style={{
                            fontSize: '0.6875rem',
                            fontWeight: 600,
                            padding: '0.125rem 0.5rem',
                            borderRadius: '9999px',
                            backgroundColor: 'rgba(13, 148, 136, 0.15)',
                            color: 'var(--color-accent-teal)',
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '0.25rem',
                          }}
                        >
                          <CheckCircle2 size={12} />
                          الجهاز الحالي
                        </span>
                      )}
                    </div>
                    <div style={{ fontSize: '0.8125rem', color: 'var(--color-text-muted)', marginTop: '0.125rem' }}>
                      المعرف: {device.deviceId.slice(0, 16)}... | IP: {device.lastIpAddress ?? '127.0.0.1'}
                    </div>
                  </div>
                </div>

                {!device.isCurrentDevice && (
                  <GlassButton
                    type="button"
                    onClick={() => handleRevoke(device.deviceId)}
                    disabled={revokingId === device.deviceId}
                    style={{ color: '#ef4444', borderColor: 'rgba(239, 68, 68, 0.3)' }}
                  >
                    <Trash2 size={16} />
                    {revokingId === device.deviceId ? 'جاري الإلغاء...' : 'إنهاء الجلسة'}
                  </GlassButton>
                )}
              </div>
            );
          })}
        </div>
      </GlassCard>
    </div>
  );
};

