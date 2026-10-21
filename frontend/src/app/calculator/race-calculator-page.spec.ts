import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RaceCalculatorPage } from './race-calculator-page';

describe('RaceCalculatorPage', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [RaceCalculatorPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  async function render(distanceUnit: 'km' | 'mi' = 'km') {
    const fixture = TestBed.createComponent(RaceCalculatorPage);
    fixture.detectChanges();
    for (const settings of TestBed.inject(HttpTestingController).match('/api/settings')) settings.flush({ distanceUnit });
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const field = (name: 'pace' | 'time') => element.querySelector<HTMLInputElement>(`input[name=${name}]`)!;
    return {
      element,
      field,
      choose(name: string) {
        const select = element.querySelector('select')!;
        select.value = [...select.options].find((option) => option.text === name)!.value;
        select.dispatchEvent(new Event('change'));
        fixture.detectChanges();
      },
      type(name: 'pace' | 'time', text: string) {
        field(name).value = text;
        field(name).dispatchEvent(new Event('input'));
        fixture.detectChanges();
      },
      slider: (name: 'pace' | 'time') => element.querySelector<HTMLInputElement>(`input[name=${name}-slider]`)!,
      slide(name: 'pace' | 'time', seconds: number) {
        const slider = element.querySelector<HTMLInputElement>(`input[name=${name}-slider]`)!;
        slider.value = String(seconds);
        slider.dispatchEvent(new Event('input'));
        fixture.detectChanges();
      },
      ends: (name: 'pace' | 'time') => [...element.querySelector(`input[name=${name}-slider] + .ends`)!.children].map((end) => end.textContent),
      figures: () =>[...element.querySelectorAll('.result .figure')].map((figure) => figure.textContent!.trim()),
    };
  }

  it('offers the common race distances, 10K first chosen', async () => {
    const { element } = await render();

    const select = element.querySelector('select')!;
    expect([...select.options].map((option) => option.text)).toEqual(['1 mile', '5K', '10K', '15K', '10 miles', 'Half marathon', 'Marathon', '50K']);
    expect(select.selectedOptions[0].text).toBe('10K');
  });

  it('shows no finish time or pace until one is typed', async () => {
    const page = await render();

    expect(page.figures()).toEqual(['–', '–']);
  });

  it('works out the finish time from a pace per km, again for each distance', async () => {
    const page = await render();

    page.type('pace', '4:30');
    expect(page.field('time').value).toBe('45:00');
    expect(page.figures()).toEqual(['45:00', '4:30 /km']);

    page.choose('Half marathon');
    expect(page.field('time').value).toBe('1:34:56');

    page.choose('Marathon');
    expect(page.field('time').value).toBe('3:09:53');
    expect(page.field('pace').value).toBe('4:30');
  });

  it('works out the pace from a finish time, keeping the time when the distance changes', async () => {
    const page = await render();

    page.choose('Half marathon');
    page.type('time', '1:45:00');
    expect(page.field('pace').value).toBe('4:59');
    expect(page.figures()).toEqual(['1:45:00', '4:59 /km']);

    page.choose('10K');
    expect(page.field('time').value).toBe('1:45:00');
    expect(page.field('pace').value).toBe('10:30');
  });

  it('works out from whichever field was typed in last', async () => {
    const page = await render();

    page.type('pace', '5:00');
    page.type('time', '40:00');

    expect(page.field('pace').value).toBe('4:00');
  });

  it('takes the pace per mile when distances are in miles', async () => {
    const page = await render('mi');

    expect(page.field('pace').closest('label')!.textContent).toContain('Pace (min/mi)');
    page.choose('10 miles');
    page.type('pace', '8:00');
    expect(page.field('time').value).toBe('1:20:00');

    page.type('time', '1:10:00');
    expect(page.field('pace').value).toBe('7:00');
  });

  it('slides the pace from 2:30 to 10:00 per km, filling both fields', async () => {
    const page = await render();

    expect(page.ends('pace')).toEqual(['2:30', '10:00']);
    expect(page.slider('pace').value).toBe('330');

    page.slide('pace', 300);

    expect(page.field('pace').value).toBe('5:00');
    expect(page.field('time').value).toBe('50:00');
    expect(page.slider('time').value).toBe('3000');
  });

  it('slides the finish time between the pace ends over the distance', async () => {
    const page = await render();

    page.choose('Half marathon');
    expect(page.ends('time')).toEqual(['52:45', '3:30:59']);

    page.slide('time', 6300);

    expect(page.field('time').value).toBe('1:45:00');
    expect(page.field('pace').value).toBe('4:59');
  });

  it('moves the sliders to what is typed', async () => {
    const page = await render();

    page.type('pace', '4:00');

    expect(page.slider('pace').value).toBe('240');
    expect(page.slider('time').value).toBe('2400');
  });

  it('slides the pace per mile when distances are in miles', async () => {
    const page = await render('mi');

    expect(page.ends('pace')).toEqual(['4:01', '16:06']);
  });

  it('marks a pace or time that is not a duration', async () => {
    const page = await render();

    page.type('pace', '4.30');
    expect(page.field('pace').getAttribute('aria-invalid')).toBe('true');
    expect(page.element.querySelector('.field-error')!.textContent).toContain('m:ss');
    expect(page.figures()).toEqual(['–', '–']);

    page.type('time', '1h45');
    expect(page.field('time').getAttribute('aria-invalid')).toBe('true');
    expect(page.field('pace').getAttribute('aria-invalid')).toBe('false');
    expect(page.element.querySelector('.field-error')!.textContent).toContain('h:mm:ss');
  });
});
