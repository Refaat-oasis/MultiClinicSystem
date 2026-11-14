/**
 * Super Admin Strongly-Typed Configuration & Credentials
 * User can change these values anytime.
 */
export interface SuperAdminConfig {
  email: string;
  password: string;
  fullName: string;
  sovereignNode: string;
  apiBaseUrl: string;
}

export const SUPER_ADMIN_CREDENTIALS: SuperAdminConfig = {
  email: 'ahmed@platform.local',
  password: 'AdminInitial#2026',
  fullName: 'Ahmed (Platform Super Admin)',
  sovereignNode: 'KSA-SEC-01',
  apiBaseUrl: 'http://localhost:5000',
};
