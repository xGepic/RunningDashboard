import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';
import { SyncButton } from '../sync/sync-button';
import { ConnectErrorReason, Connection, connectErrorMessages } from './connection';

@Component({
  selector: 'app-connection-status',
  imports: [SyncButton],
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
}
