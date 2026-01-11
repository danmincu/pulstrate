import { Component, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { AuthService, TokenInfo } from '../../services/auth.service';
import { LoginDialogComponent } from '../login-dialog/login-dialog.component';
import { Observable, map } from 'rxjs';

@Component({
  selector: 'app-auth-button',
  standalone: true,
  imports: [
    AsyncPipe,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule
  ],
  template: `
    @if (tokenInfo$ | async; as info) {
      <div class="auth-info">
        <span class="token-badge" [matTooltip]="getTooltip(info)">
          <mat-icon class="auth-icon">verified_user</mat-icon>
          <span class="user-label">{{ getUserLabel(info) }}</span>
        </span>
        <button
          mat-icon-button
          (click)="onLogout()"
          matTooltip="Clear token">
          <mat-icon>logout</mat-icon>
        </button>
      </div>
    } @else {
      <button
        mat-stroked-button
        (click)="onLogin()"
        matTooltip="Set JWT token for authenticated microservice calls">
        <mat-icon>login</mat-icon>
        Set Token
      </button>
    }
  `,
  styles: [`
    :host {
      display: flex;
      align-items: center;
    }

    .auth-info {
      display: flex;
      align-items: center;
      gap: 4px;
    }

    .token-badge {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 4px 12px;
      background: rgba(255, 255, 255, 0.15);
      border-radius: 16px;
      font-size: 13px;
      cursor: default;
    }

    .auth-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
      color: #69f0ae;
    }

    .user-label {
      max-width: 150px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    button[mat-stroked-button] {
      border-color: rgba(255, 255, 255, 0.5);
      color: white;
    }

    button[mat-stroked-button] mat-icon {
      margin-right: 4px;
    }

    button[mat-icon-button] {
      color: rgba(255, 255, 255, 0.8);
    }

    button[mat-icon-button]:hover {
      color: white;
    }
  `]
})
export class AuthButtonComponent {
  private readonly dialog = inject(MatDialog);
  private readonly authService = inject(AuthService);

  readonly tokenInfo$: Observable<TokenInfo | null> = this.authService.tokenInfo$;

  onLogin(): void {
    this.dialog.open(LoginDialogComponent, {
      width: '550px',
      disableClose: false
    });
  }

  onLogout(): void {
    this.authService.clearToken();
  }

  getUserLabel(info: TokenInfo): string {
    if (info.claims?.email) {
      return info.claims.email as string;
    }
    if (info.claims?.name) {
      return info.claims.name as string;
    }
    if (info.claims?.sub) {
      const sub = info.claims.sub as string;
      return sub.length > 15 ? `${sub.slice(0, 12)}...` : sub;
    }
    return 'Authenticated';
  }

  getTooltip(info: TokenInfo): string {
    const lines: string[] = ['JWT Token Active'];

    if (info.claims?.email) {
      lines.push(`Email: ${info.claims.email}`);
    }
    if (info.claims?.name) {
      lines.push(`Name: ${info.claims.name}`);
    }
    if (info.claims?.sub || info.claims?.user_id) {
      lines.push(`User ID: ${info.claims.user_id || info.claims.sub}`);
    }
    if (info.claims?.exp) {
      const exp = info.claims.exp as number;
      const date = new Date(exp * 1000);
      const isExpired = Date.now() > exp * 1000;
      lines.push(`${isExpired ? 'Expired' : 'Expires'}: ${date.toLocaleString()}`);
    }

    lines.push('', `Token: ${info.preview}`);

    return lines.join('\n');
  }
}
