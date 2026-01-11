import { Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { AuthService, TokenInfo } from '../../services/auth.service';

@Component({
  selector: 'app-login-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule
  ],
  template: `
    <h2 mat-dialog-title>
      <mat-icon class="title-icon">vpn_key</mat-icon>
      Set JWT Token
    </h2>

    <mat-dialog-content>
      <p class="description">
        Paste your JWT token below. This token will be used for authenticated
        calls to microservices. The token is stored in localStorage.
      </p>

      <mat-form-field appearance="outline" class="token-field">
        <mat-label>JWT Token</mat-label>
        <textarea
          matInput
          [formControl]="tokenControl"
          placeholder="eyJhbGciOiJSUzI1NiIs..."
          rows="6"
          class="token-input">
        </textarea>
        <mat-hint>Include or exclude the "Bearer " prefix - both work</mat-hint>
        @if (tokenControl.hasError('required')) {
          <mat-error>Token is required</mat-error>
        }
      </mat-form-field>

      @if (error) {
        <div class="error-message">
          <mat-icon>error</mat-icon>
          {{ error }}
        </div>
      }

      @if (tokenInfo) {
        <div class="token-preview">
          <h4>Token Info</h4>
          <div class="preview-item">
            <span class="label">Preview:</span>
            <code>{{ tokenInfo.preview }}</code>
          </div>
          @if (tokenInfo.claims) {
            @if (tokenInfo.claims['email']) {
              <div class="preview-item">
                <span class="label">Email:</span>
                <span>{{ tokenInfo.claims['email'] }}</span>
              </div>
            }
            @if (tokenInfo.claims['name']) {
              <div class="preview-item">
                <span class="label">Name:</span>
                <span>{{ tokenInfo.claims['name'] }}</span>
              </div>
            }
            @if (tokenInfo.claims['sub'] || tokenInfo.claims['user_id']) {
              <div class="preview-item">
                <span class="label">User ID:</span>
                <span>{{ tokenInfo.claims['user_id'] || tokenInfo.claims['sub'] }}</span>
              </div>
            }
            @if (tokenInfo.claims['exp']) {
              <div class="preview-item">
                <span class="label">Expires:</span>
                <span [class.expired]="isExpired(tokenInfo.claims['exp'])">
                  {{ formatExpiry(tokenInfo.claims['exp']) }}
                </span>
              </div>
            }
          }
        </div>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">Cancel</button>
      <button
        mat-raised-button
        color="primary"
        (click)="onSubmit()"
        [disabled]="!tokenControl.valid">
        <mat-icon>check</mat-icon>
        Set Token
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    :host {
      display: block;
    }

    h2[mat-dialog-title] {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 0;
    }

    .title-icon {
      color: #1976d2;
    }

    mat-dialog-content {
      width: 450px;
      max-width: 100%;
      overflow: visible;
    }

    .description {
      color: #666;
      font-size: 14px;
      margin-bottom: 16px;
      line-height: 1.5;
    }

    .token-field {
      width: 100%;
    }

    .token-input {
      font-family: 'Consolas', 'Monaco', monospace;
      font-size: 12px;
      line-height: 1.4;
    }

    .error-message {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px;
      background: #ffebee;
      color: #c62828;
      border-radius: 4px;
      margin-top: 16px;
      font-size: 14px;
    }

    .error-message mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
    }

    .token-preview {
      margin-top: 16px;
      padding: 12px;
      background: #e3f2fd;
      border-radius: 4px;
      border-left: 4px solid #1976d2;
    }

    .token-preview h4 {
      margin: 0 0 12px 0;
      font-size: 14px;
      color: #1976d2;
    }

    .preview-item {
      display: flex;
      gap: 8px;
      margin-bottom: 6px;
      font-size: 13px;
    }

    .preview-item:last-child {
      margin-bottom: 0;
    }

    .preview-item .label {
      font-weight: 500;
      color: #666;
      min-width: 70px;
    }

    .preview-item code {
      font-family: 'Consolas', 'Monaco', monospace;
      font-size: 12px;
      background: rgba(0, 0, 0, 0.05);
      padding: 2px 6px;
      border-radius: 3px;
      word-break: break-all;
    }

    .expired {
      color: #c62828;
      font-weight: 500;
    }

    mat-dialog-actions {
      padding: 16px 8px 16px 0;
      margin: 0;
      border-top: 1px solid #e0e0e0;
    }

    mat-dialog-actions button mat-icon {
      margin-right: 4px;
    }

    /* Mobile responsive */
    @media (max-width: 500px) {
      mat-dialog-content {
        width: 100%;
      }

      .description {
        font-size: 13px;
      }

      mat-dialog-actions {
        padding: 12px 0 0 0;
      }
    }
  `]
})
export class LoginDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<LoginDialogComponent>);
  private readonly authService = inject(AuthService);

  tokenControl = new FormControl('', [Validators.required]);
  error: string | null = null;
  tokenInfo: TokenInfo | null = null;

  onSubmit(): void {
    if (!this.tokenControl.valid) return;

    const rawToken = this.tokenControl.value!;
    this.error = null;
    this.tokenInfo = null;

    try {
      const info = this.authService.setToken(rawToken);
      this.tokenInfo = info;

      // Close dialog after short delay to show success
      setTimeout(() => {
        this.dialogRef.close(true);
      }, 500);
    } catch (err) {
      this.error = err instanceof Error ? err.message : 'Invalid token';
    }
  }

  onCancel(): void {
    this.dialogRef.close(false);
  }

  isExpired(exp: number): boolean {
    return Date.now() > exp * 1000;
  }

  formatExpiry(exp: number): string {
    const date = new Date(exp * 1000);
    const now = Date.now();

    if (now > exp * 1000) {
      return `Expired: ${date.toLocaleString()}`;
    }

    const diffMs = exp * 1000 - now;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffDays > 0) {
      return `${date.toLocaleString()} (in ${diffDays} days)`;
    } else if (diffHours > 0) {
      return `${date.toLocaleString()} (in ${diffHours} hours)`;
    } else {
      return `${date.toLocaleString()} (in ${diffMins} minutes)`;
    }
  }
}
