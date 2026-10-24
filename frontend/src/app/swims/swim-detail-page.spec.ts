import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { SwimDetail } from './swim';
import { SwimDetailPage } from './swim-detail-page';

const poolSwim: SwimDetail = {
  id: 1, name: 'Lunch Swim', startLocal: '2026-10-11T12:00:00', distance: 2000, movingTime: 2400, elapsedTime: 2700, pace: 120,
  averageHeartRate: 132.4, maxHeartRate: 158, stravaUrl: 'https://www.strava.com/activities/1',
};

describe('SwimDetailPage', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'swims/:id', component: SwimDetailPage }], withComponentInputBinding()),
      ],
    });
  });

  /** `swim` 'error' answers with a server error. */
  async function open(swim: SwimDetail | 'error'): Promise<HTMLElement> {
    const harness = await RouterTestingHarness.create();
    const http = TestBed.inject(HttpTestingController);
    await harness.navigateByUrl('/swims/1');
    harness.detectChanges();
    const request = http.expectOne('/api/swims/1');
    if (swim === 'error') request.flush(null, { status: 500, statusText: 'Server Error' });
    else request.flush(swim);
    await new Promise((resolve) => setTimeout(resolve));
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  it('shows the Swim with its Swim Pace and heart rate', async () => {
    const element = await open(poolSwim);

    expect(element.querySelector('h1')!.textContent).toContain('Lunch Swim');
    const facts = element.querySelector('.summary')!.textContent!;
    expect(facts).toContain('2,000 m');
    expect(facts).toContain('2:00 /100m');
    expect(facts).toContain('132 bpm');
    expect(facts).toContain('158 bpm');
  });

  it('says so when the Swim cannot be loaded', async () => {
    const element = await open('error');

    expect(element.querySelector('[role="alert"]')!.textContent).toContain('This Swim could not be loaded');
  });
});
