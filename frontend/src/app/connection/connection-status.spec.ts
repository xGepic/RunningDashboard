import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { ConnectionStatus } from './connection-status';

describe('ConnectionStatus', () => {
  async function render(queryParams: Record<string, string> = {}) {
    TestBed.configureTestingModule({
      imports: [ConnectionStatus],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { queryParamMap: of(convertToParamMap(queryParams)) } },
      ],
    });
    const fixture = TestBed.createComponent(ConnectionStatus);
    fixture.detectChanges();
    return { fixture, http: TestBed.inject(HttpTestingController) };
  }

  async function answerConnection(
    { fixture, http }: Awaited<ReturnType<typeof render>>,
    body: object,
  ): Promise<HTMLElement> {
    http.expectOne('/api/connection').flush(body);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('offers to connect Strava when no athlete is connected', async () => {
    const element = await answerConnection(await render(), { connected: false, athlete: null, connectedAt: null });

    const link = element.querySelector('a');
    expect(link?.textContent).toContain('Connect Strava');
    expect(link?.getAttribute('href')).toBe('/api/strava/connect');
  });

  it('shows who is connected', async () => {
    const element = await answerConnection(await render(), {
      connected: true,
      athlete: { id: 133415890, firstName: 'Stefan', lastName: 'Simanek' },
      connectedAt: '2026-10-09T18:00:00+00:00',
    });

    expect(element.textContent).toContain('Connected as Stefan Simanek');
    expect(element.querySelector('a')).toBeNull();
  });

  it('explains why connecting failed when Strava sends the athlete back with an error', async () => {
    const element = await answerConnection(
      await render({ strava: 'error', reason: 'missing-permissions' }),
      { connected: false, athlete: null, connectedAt: null },
    );

    expect(element.textContent).toContain('allow all requested permissions');
    expect(element.querySelector('a')?.textContent).toContain('Connect Strava');
  });

  it('asks for the Strava API app when connecting found none', async () => {
    const element = await answerConnection(
      await render({ strava: 'error', reason: 'no-strava-app' }),
      { connected: false, athlete: null, connectedAt: null, stravaApp: null },
    );

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Enter your Strava API app');
  });

  it('explains a refused different athlete while staying connected as the first one', async () => {
    const element = await answerConnection(await render({ strava: 'error', reason: 'different-athlete' }), {
      connected: true,
      athlete: { id: 133415890, firstName: 'Stefan', lastName: 'Simanek' },
      connectedAt: '2026-10-09T18:00:00+00:00',
    });

    expect(element.textContent).toContain('already connected to a different Strava athlete');
    expect(element.textContent).toContain('Connected as Stefan Simanek');
  });

  it('says so when the backend cannot be reached', async () => {
    const { fixture, http } = await render();

    http.expectOne('/api/connection').flush('down', { status: 502, statusText: 'Bad Gateway' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('The backend is not reachable.');
  });
});
