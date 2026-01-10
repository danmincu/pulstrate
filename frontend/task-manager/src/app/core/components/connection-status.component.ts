import { Component, inject } from '@angular/core';
import { AsyncPipe, DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatButtonModule } from '@angular/material/button';
import { SignalRService, ConnectionInfo, ConnectionStatus } from '../services/signalr.service';

@Component({
  selector: 'app-connection-status',
  standalone: true,
  imports: [AsyncPipe, DatePipe, MatIconModule, MatTooltipModule, MatButtonModule],
  template: `
    @if (connectionInfo$ | async; as info) {
      <button mat-icon-button
              class="status-button"
              [class.connected]="info.status === 'connected'"
              [class.connecting]="info.status === 'connecting'"
              [class.reconnecting]="info.status === 'reconnecting'"
              [class.disconnected]="info.status === 'disconnected'"
              [matTooltip]="getTooltip(info)"
              matTooltipPosition="below"
              (click)="onStatusClick(info)">
        <div class="status-indicator">
          @switch (info.status) {
            @case ('connected') {
              <mat-icon class="status-icon">wifi</mat-icon>
            }
            @case ('connecting') {
              <mat-icon class="status-icon spinning">sync</mat-icon>
            }
            @case ('reconnecting') {
              <mat-icon class="status-icon spinning">sync</mat-icon>
            }
            @case ('disconnected') {
              <mat-icon class="status-icon">wifi_off</mat-icon>
            }
          }
          <span class="status-dot"></span>
        </div>
      </button>
    }
  `,
  styles: [`
    .status-button {
      position: relative;
    }

    .status-indicator {
      position: relative;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .status-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
    }

    .status-icon.spinning {
      animation: spin 1.5s linear infinite;
    }

    @keyframes spin {
      from { transform: rotate(0deg); }
      to { transform: rotate(360deg); }
    }

    .status-dot {
      position: absolute;
      bottom: -2px;
      right: -2px;
      width: 8px;
      height: 8px;
      border-radius: 50%;
      border: 1.5px solid white;
    }

    .connected .status-icon {
      color: rgba(255, 255, 255, 0.9);
    }

    .connected .status-dot {
      background-color: #4caf50;
    }

    .connecting .status-icon,
    .reconnecting .status-icon {
      color: rgba(255, 255, 255, 0.7);
    }

    .connecting .status-dot,
    .reconnecting .status-dot {
      background-color: #ff9800;
      animation: pulse 1s ease-in-out infinite;
    }

    @keyframes pulse {
      0%, 100% { opacity: 1; transform: scale(1); }
      50% { opacity: 0.5; transform: scale(0.8); }
    }

    .disconnected .status-icon {
      color: rgba(255, 255, 255, 0.6);
    }

    .disconnected .status-dot {
      background-color: #f44336;
    }

    /* Hover effects */
    .status-button:hover .status-icon {
      color: white;
    }

    .disconnected:hover,
    .reconnecting:hover {
      cursor: pointer;
    }
  `]
})
export class ConnectionStatusComponent {
  private readonly signalR = inject(SignalRService);

  connectionInfo$ = this.signalR.connectionInfo$;

  getTooltip(info: ConnectionInfo): string {
    const lines: string[] = [];

    switch (info.status) {
      case 'connected':
        lines.push('Connected to server');
        if (info.lastConnectedAt) {
          lines.push(`Since: ${this.formatTime(info.lastConnectedAt)}`);
        }
        break;

      case 'connecting':
        lines.push('Connecting to server...');
        break;

      case 'reconnecting':
        lines.push(`Reconnecting... (attempt ${info.reconnectAttempt})`);
        if (info.lastError) {
          lines.push(`Error: ${info.lastError}`);
        }
        break;

      case 'disconnected':
        lines.push('Disconnected from server');
        if (info.lastError) {
          lines.push(`Error: ${info.lastError}`);
        }
        if (info.lastDisconnectedAt) {
          lines.push(`Since: ${this.formatTime(info.lastDisconnectedAt)}`);
        }
        lines.push('Click to reconnect');
        break;
    }

    return lines.join('\n');
  }

  onStatusClick(info: ConnectionInfo): void {
    // Only allow manual reconnect when disconnected or reconnecting has stalled
    if (info.status === 'disconnected' ||
        (info.status === 'reconnecting' && info.reconnectAttempt > 3)) {
      this.signalR.reconnect();
    }
  }

  private formatTime(date: Date): string {
    return date.toLocaleTimeString();
  }
}
