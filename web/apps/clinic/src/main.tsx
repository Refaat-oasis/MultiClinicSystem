import React, { Suspense, lazy } from 'react';
import ReactDOM from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@clinic/ui/src/styles/liquid-glass.css';

const StaffApp = lazy(() => import('./App').then((m) => ({ default: m.App })));
const PatientPortalApp = lazy(() => import('./portal/PortalApp'));

const isPortalRoute =
  typeof window !== 'undefined' && /^\/c\/[^/]+/i.test(window.location.pathname);

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <Suspense fallback={<div dir="rtl" style={{ padding: '2rem', textAlign: 'center' }}>جاري التحميل...</div>}>
        {isPortalRoute ? <PatientPortalApp /> : <StaffApp />}
      </Suspense>
    </QueryClientProvider>
  </React.StrictMode>
);

