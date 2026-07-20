import { useQuery } from '@tanstack/react-query';
import { resolveQRToken, QRResolution } from '../services/api';

export function useQRResolution(qrToken: string | null) {
  return useQuery({
    queryKey: ['qr', qrToken],
    queryFn: () => resolveQRToken(qrToken!),
    enabled: !!qrToken,
    retry: false, // Don't retry if the token is invalid/inactive, we want to fail fast
  });
}
