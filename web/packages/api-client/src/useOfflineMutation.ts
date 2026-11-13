import { useMutation, UseMutationOptions, UseMutationResult } from '@tanstack/react-query';
import { v7 as uuidv7 } from 'uuid';
import { apiClient } from './ApiClient';
import { wipeAllLocalData } from './offlineDb';
import { consumeLocalReceiptNumber, enqueueOfflineOperation } from './outboxManager';
import { ProblemDetails } from './ProblemDetails';
import { useSyncStatusStore } from './useSyncStatusStore';

export interface OfflineSyncQueueConfig<TVariables> {
  clinicId: string;
  deviceId: string;
  actionType: string;
  entityType: string;
  resolveEntityId?: (variables: TVariables) => string;
  issueProvisionalReceipt?: boolean;
}

export interface OfflineMutationOptions<TData, TVariables>
  extends Omit<UseMutationOptions<TData, ProblemDetails, TVariables>, 'mutationFn'> {
  endpoint: string;
  method?: 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  onlineOnly?: boolean;
  offlineConfig?: OfflineSyncQueueConfig<TVariables>;
}

/**
 * Standard mutation hook that attaches client-generated GuidV7 OperationId,
 * enforces online-only rules, queues offline-capable writes in IndexedDB outbox,
 * and wipes local data if DEVICE_REVOKED is returned.
 */
export function useOfflineMutation<TData, TVariables>(
  options: OfflineMutationOptions<TData, TVariables>
): UseMutationResult<TData, ProblemDetails, TVariables> {
  const { endpoint, method = 'POST', onlineOnly = false, offlineConfig, ...mutationOptions } = options;

  return useMutation<TData, ProblemDetails, TVariables>({
    mutationFn: async (variables: TVariables) => {
      const offline = typeof navigator !== 'undefined' && navigator.onLine === false;
      if (onlineOnly && offline) {
        const error: ProblemDetails = {
          title: 'Offline Action Forbidden',
          status: 400,
          detail: 'This action requires an active online internet connection.',
          code: 'ONLINE_REQUIRED',
        };
        throw error;
      }

      const operationId = uuidv7();

      if (!onlineOnly && offline && offlineConfig) {
        const entityId = offlineConfig.resolveEntityId
          ? offlineConfig.resolveEntityId(variables)
          : uuidv7();

        let provisionalReceiptNumber: string | undefined;
        if (offlineConfig.issueProvisionalReceipt) {
          const allocated = await consumeLocalReceiptNumber(offlineConfig.clinicId, offlineConfig.deviceId);
          if (allocated.isProvisional) {
            provisionalReceiptNumber = allocated.receiptNumber;
          }
        }

        await enqueueOfflineOperation({
          operationId,
          clinicId: offlineConfig.clinicId,
          deviceId: offlineConfig.deviceId,
          actionType: offlineConfig.actionType,
          entityType: offlineConfig.entityType,
          entityId,
          endpoint,
          method,
          payloadJson: variables ? JSON.stringify(variables) : '{}',
          clientTimestampUtc: new Date().toISOString(),
          clientBaseCursor: useSyncStatusStore.getState().lastCursor,
          provisionalReceiptNumber,
        });

        return {
          pendingUpload: true,
          operationId,
          entityId,
          provisionalReceiptNumber,
        } as unknown as TData;
      }

      try {
        return await apiClient.request<TData>(endpoint, {
          method,
          body: variables ? JSON.stringify(variables) : undefined,
          operationId,
        });
      } catch (err) {
        const problem = err as ProblemDetails | undefined;
        if (problem?.code === 'DEVICE_REVOKED') {
          await wipeAllLocalData();
          useSyncStatusStore.getState().setSyncSnapshot({
            pendingCount: 0,
            provisionalReceiptsCount: 0,
            deviceRevokedWiped: true,
          });
        }
        throw err;
      }
    },
    ...mutationOptions,
  });
}
