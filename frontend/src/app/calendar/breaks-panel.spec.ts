import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Break } from '../shared/breaks';
import { BreaksPanel } from './breaks-panel';

const injury: Break = { id: 1, kind: 'injury', start: '2026-09-10', end: '2026-09-16', note: 'Calf strain' };
const illness: Break = { id: 2, kind: 'illness', start: '2026-09-28', end: '2026-09-28', note: null };
const earlier: Break = { id: 3, kind: 'holiday', start: '2026-07-01', end: '2026-07-14', note: null };

/** Friday 9 Oct 2026. */
describe('BreaksPanel', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 8, 18, 12));
    // jsdom has no modal dialogs.
    HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    });
    HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    });
    TestBed.configureTestingModule({ imports: [BreaksPanel], providers: [provideHttpClient(), provideHttpClientTesting()] });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  async function render(breaks: Break[] = [injury, illness, earlier], from = '2026-09-01', to = '2026-09-30') {
    const fixture = TestBed.createComponent(BreaksPanel);
    fixture.componentRef.setInput('from', from);
    fixture.componentRef.setInput('to', to);
    fixture.componentRef.setInput('period', 'September 2026');
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    const answer = async (list: Break[]) => {
      // A reload asks again once its effect has run.
      TestBed.tick();
      http.expectOne((r) => r.method === 'GET' && r.url === '/api/breaks').flush(list);
      await fixture.whenStable();
      fixture.detectChanges();
    };
    await answer(breaks);
    const element = fixture.nativeElement as HTMLElement;
    const click = (text: string, within: ParentNode = element) => {
      [...within.querySelectorAll<HTMLButtonElement>('button')].find((b) => b.textContent!.trim() === text)!.click();
      fixture.detectChanges();
    };
    const field = (name: string) => element.querySelector<HTMLInputElement | HTMLSelectElement>(`dialog [name=${name}]`)!;
    const fill = (name: string, value: string) => {
      const input = field(name);
      input.value = value;
      input.dispatchEvent(new Event(input instanceof HTMLSelectElement ? 'change' : 'input'));
      fixture.detectChanges();
    };
    const submit = () => {
      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      fixture.detectChanges();
    };
    const opened = async () => {
      await fixture.whenStable();
      fixture.detectChanges();
    };
    return { fixture, element, http, answer, click, field, fill, submit, opened };
  }

  const rows = (element: HTMLElement) =>
    [...element.querySelectorAll('.list li')].map((li) => [...li.children].slice(0, 4).map((part) => part.textContent!.trim()));

  it('lists the Breaks with days in the period, with their days, length and note', async () => {
    const { element } = await render();

    expect(rows(element)).toEqual([
      ['Injury', '10 Sept – 16 Sept 2026', '7 days', 'Calf strain'],
      ['Illness', '28 Sept 2026', '1 day', ''],
    ]);
    expect(element.querySelector('.list li')!.classList).toContain('break-injury');
  });

  it('says when the period has none', async () => {
    const { element } = await render([earlier]);

    expect(element.querySelector('.empty')!.textContent!.trim()).toBe('No Breaks in September 2026.');
  });

  it('marks a Break from today, and gets the Breaks again once it is saved', async () => {
    const page = await render([]);

    page.click('Mark a Break');
    await page.opened();

    expect(page.element.querySelector('dialog')!.hasAttribute('open')).toBe(true);
    expect(page.field('kind').value).toBe('injury');
    expect(page.field('start').value).toBe('2026-09-18');
    expect(page.field('end').value).toBe('2026-09-18');
    page.fill('kind', 'holiday');
    page.fill('end', '2026-09-25');
    page.fill('note', 'Crete');
    page.submit();

    const request = page.http.expectOne((r) => r.method === 'POST' && r.url === '/api/breaks');
    expect(request.request.body).toEqual({ kind: 'holiday', start: '2026-09-18', end: '2026-09-25', note: 'Crete' });
    request.flush({ id: 9, kind: 'holiday', start: '2026-09-18', end: '2026-09-25', note: 'Crete' });
    await page.answer([{ id: 9, kind: 'holiday', start: '2026-09-18', end: '2026-09-25', note: 'Crete' }]);

    expect(page.element.querySelector('dialog')!.hasAttribute('open')).toBe(false);
    expect(rows(page.element)).toEqual([['Holiday', '18 Sept – 25 Sept 2026', '8 days', 'Crete']]);
  });

  it('starts a new Break on the first day of a period without today', async () => {
    const page = await render([], '2026-07-01', '2026-07-31');

    page.click('Mark a Break');
    await page.opened();

    expect(page.field('start').value).toBe('2026-07-01');
  });

  it('changes a Break', async () => {
    const page = await render();

    page.click('Edit', page.element.querySelector('.list li')!);
    await page.opened();

    expect(page.field('note').value).toBe('Calf strain');
    page.fill('end', '2026-09-20');
    page.submit();

    const request = page.http.expectOne((r) => r.method === 'PUT' && r.url === '/api/breaks/1');
    expect(request.request.body).toEqual({ kind: 'injury', start: '2026-09-10', end: '2026-09-20', note: 'Calf strain' });
  });

  it('shows what the API found wrong next to its field', async () => {
    const page = await render();
    page.click('Mark a Break');
    await page.opened();

    page.submit();
    page.http
      .expectOne((r) => r.method === 'POST')
      .flush(
        { errors: { start: ['It overlaps the injury Break from 10 Sep 2026 to 16 Sep 2026.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    page.fixture.detectChanges();

    expect(page.field('start').getAttribute('aria-invalid')).toBe('true');
    expect(page.element.querySelector('dialog .field-error')!.textContent).toContain('It overlaps the injury Break');
    expect(page.element.querySelector('dialog')!.hasAttribute('open')).toBe(true);
  });

  it('removes a Break once confirmed', async () => {
    vi.stubGlobal('confirm', vi.fn(() => true));
    const page = await render();

    page.click('Remove', page.element.querySelectorAll('.list li')[1]);

    expect(confirm).toHaveBeenCalledWith('Remove the Illness Break of 28 Sept 2026?');
    page.http.expectOne((r) => r.method === 'DELETE' && r.url === '/api/breaks/2').flush(null);
    await page.answer([injury, earlier]);
    expect(rows(page.element).length).toBe(1);
    vi.unstubAllGlobals();
  });
});
