import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';
import { SyncIndicator } from '../sync/sync-indicator';
import { ConnectErrorReason, ConnectedAthlete, Connection, connectErrorMessages } from './connection';

@Component({
  selector: 'app-connection-status',
  imports: [SyncIndicator],
  templateUrl: './connection-status.html',
  styleUrl: './connection-status.scss',
})
export class ConnectionStatus {
  protected readonly connection = httpResource<Connection>(() => '/api/connection');

  private readonly reason = toSignal(
    inject(ActivatedRoute).queryParamMap.pipe(
      map((params) => (params.get('strava') === 'error' ? params.get('reason') : null)),
    ),
  );

  protected readonly errorMessage = computed(() => {
    const reason = this.reason();
    if (!reason) return null;
    return connectErrorMessages[reason as ConnectErrorReason] ?? 'Connecting to Strava failed. Please try again.';
  });

  /** "SS" for Stefan Simanek. */
  protected initials(athlete: ConnectedAthlete): string {
    return `${athlete.firstName.charAt(0)}${athlete.lastName.charAt(0)}`.toUpperCase();
  }
}
