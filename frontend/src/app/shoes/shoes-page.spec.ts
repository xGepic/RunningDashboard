import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Shoe } from './shoe';
import { ShoesPage } from './shoes-page';

const shoe = (id: string, name: string, distance: number): Shoe => ({ id, name, distance, retired: false, runs: 10, lastRun: '2026-10-06T20:35:26' });

describe('ShoesPage', () => {
  async function render(shoeMaximumDistance: number, shoes: Shoe[]) {
    TestBed.configureTestingModule({ imports: [ShoesPage], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const fixture = TestBed.createComponent(ShoesPage);
    const http = TestBed.inject(HttpTestingController);
    // The sync status first: once the page shows, a change to it would load the Shoes again.
    http.expectOne('/api/sync').flush({ running: false, importedActivities: 0, lastSyncedAt: null });
    fixture.detectChanges();
    http.expectOne('/api/settings').flush({ distanceUnit: 'km', shoeMaximumDistance });
    http.expectOne('/api/shoes').flush(shoes);
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    /** Each Shoe's wear as "Maximum Distance …: note (status)". */
    const wear = [...element.querySelectorAll('.max')].map((max) => {
      const note = max.querySelector('.note')!;
      const status = note.classList.contains('over') ? 'over' : note.classList.contains('near') ? 'near' : 'ok';
      return `${max.querySelector('.share-label span')!.textContent!.trim()}: ${note.textContent!.trim()} (${status})`;
    });
    return { element, wear };
  }

  it('measures each Shoe against the Maximum Distance from Settings', async () => {
    const { wear } = await render(500_000, [shoe('g1', 'Pegasus', 460_000), shoe('g2', 'Vaporfly', 120_000), shoe('g3', 'Ghost', 530_000)]);

    expect(wear).toEqual([
      'Maximum Distance 500 km: 40 km left (near)',
      'Maximum Distance 500 km: 380 km left (ok)',
      'Maximum Distance 500 km: 30 km over (over)',
    ]);
  });

  it("opens a Shoe's Runs from its name or anywhere on its card", async () => {
    const { element } = await render(700_000, [shoe('g1', 'Pegasus', 460_000), shoe('g2', 'Vaporfly', 120_000)]);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    const names = [...element.querySelectorAll<HTMLAnchorElement>('.shoe h2 a')];
    expect(names.map((a) => a.getAttribute('href'))).toEqual(['/runs?shoe=g1', '/runs?shoe=g2']);
    element.querySelectorAll<HTMLElement>('.shoe .distance')[1].click();
    expect(navigate).toHaveBeenCalledWith(['/runs'], { queryParams: { shoe: 'g2' } });
  });
});
