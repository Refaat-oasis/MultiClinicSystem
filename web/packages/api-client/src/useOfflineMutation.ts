import { useMutation, UseMutationOptions, UseMutationResult } from '@tanstack/react-query';
import { v7 as uuidv7 } from 'uuid';
import { apiClient } from './ApiClient';
import { ProblemDetails } from './ProblemDetails';

export interface OfflineMutationOptions<TData, TVariables>
  extends Omit<UseMutationOptions<TData, ProblemDetails, TVariables>, 'mutationFn'> {
  endpoint: string;
  method?: 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  onlineOnly?: boolean;
}

/**
 * Standard mutation hook that attaches client-generated GuidV7 OperationId
 * and handles online/offline constraints per AGENTS.md.
 */
export function useOfflineMutation<TData, TVariables>(
  options: OfflineMutationOptions<TData, TVariables>
): UseMutationResult<TData, ProblemDetails, TVariables> {
  const { endpoint, method = 'POST', onlineOnly = false, ...mutationOptions } = options;

  return useMutation<TData, ProblemDetails, TVariables>({
    mutationFn: async (variables: TVariables) => {
      if (onlineOnly && typeof navigator !== 'undefined' && !navigator.onLine) {
        const error: ProblemDetails = {
          title: 'Offline Action Forbidden',
          status: 400,
          detail: 'This action requires an active online internet connection.',
          code: 'ONLINE_REQUIRED',
        };
        throw error;
      }

      const operationId = uuidv7();

      return apiClient.request<TData>(endpoint, {
        method,
        body: variables ? JSON.stringify(variables) : undefined,
        operationId,
      });
    },
    ...mutationOptions,
  });
}

