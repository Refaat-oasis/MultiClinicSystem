import { create } from 'zustand';
import { wipeAllLocalData } from '@clinic/api-client';

export interface AuthUserState {
  id: string;
  email: string;
  fullName: string;
  role: string;
  clinicId?: string;
}

export interface AuthStore {
  user: AuthUserState | null;
  accessToken: string | null;
  isAuthenticated: boolean;
  setAuth: (user: AuthUserState, token: string) => void;
  logout: () => void;
  setClinic: (clinicId: string) => void;
}

export const useAuthStore = create<AuthStore>((set) => ({
  user: null,
  accessToken: null,
  isAuthenticated: false,
  setAuth: (user, token) =>
    set({
      user,
      accessToken: token,
      isAuthenticated: true,
    }),
  logout: () => {
    void wipeAllLocalData();
    set({
      user: null,
      accessToken: null,
      isAuthenticated: false,
    });
  },
  setClinic: (clinicId) =>
    set((state) => ({
      user: state.user ? { ...state.user, clinicId } : null,
    })),
}));

