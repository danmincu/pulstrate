import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, map } from 'rxjs';

export interface TokenInfo {
  token: string;
  preview: string;
  claims?: JwtClaims;
}

export interface JwtClaims {
  sub?: string;
  user_id?: string;
  email?: string;
  name?: string;
  exp?: number;
  iat?: number;
  [key: string]: unknown;
}

const STORAGE_KEY = 'auth_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenSubject$ = new BehaviorSubject<string | null>(this.loadFromStorage());

  readonly token$ = this.tokenSubject$.asObservable();
  readonly isAuthenticated$ = this.token$.pipe(map(t => !!t));
  readonly tokenInfo$: Observable<TokenInfo | null> = this.token$.pipe(
    map(token => token ? this.parseToken(token) : null)
  );

  getToken(): string | null {
    return this.tokenSubject$.value;
  }

  setToken(rawToken: string): TokenInfo {
    // Strip "Bearer " prefix if present
    const token = rawToken.trim().replace(/^Bearer\s+/i, '');

    // Validate JWT format (3 dot-separated parts)
    const parts = token.split('.');
    if (parts.length !== 3) {
      throw new Error('Invalid JWT format. Token should have 3 parts separated by dots.');
    }

    // Store and persist
    this.tokenSubject$.next(token);
    this.saveToStorage(token);

    return this.parseToken(token);
  }

  clearToken(): void {
    this.tokenSubject$.next(null);
    this.removeFromStorage();
  }

  isAuthenticated(): boolean {
    return !!this.tokenSubject$.value;
  }

  private parseToken(token: string): TokenInfo {
    const preview = `${token.slice(0, 20)}...${token.slice(-10)}`;

    try {
      const payloadBase64 = token.split('.')[1];
      const payloadJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
      const claims = JSON.parse(payloadJson) as JwtClaims;

      return { token, preview, claims };
    } catch {
      // If decoding fails, return without claims
      return { token, preview };
    }
  }

  private loadFromStorage(): string | null {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  }

  private saveToStorage(token: string): void {
    try {
      localStorage.setItem(STORAGE_KEY, token);
    } catch {
      // localStorage might not be available
    }
  }

  private removeFromStorage(): void {
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Ignore
    }
  }
}
