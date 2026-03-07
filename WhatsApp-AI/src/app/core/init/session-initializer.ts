import { AuthService } from '../auth/auth.service';

export function initializeSessionFactory(authService: AuthService): () => Promise<void> {
  return () => authService.initializeSession();
}
