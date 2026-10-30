import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RaceCalculatorPage } from './race-calculator-page';

/** A node's text, its parts apart by a space; Angular's comments for its blocks have none. */
function text(node: Node): string {
  if (node.nodeType === Node.COMMENT_NODE) return '';
  return node.childNodes.length
    ? [...node.childNodes].map(text).filter(Boolean).join(' ')
    : (node.textContent ?? '').replace(/\s+/g, ' ').trim();
}

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
    const select = (name: string) => element.querySelector<HTMLSelectElement>(`select[name=${name}]`);
    const pick = (name: string, text: string) => {
      const picker = select(name)!;
      picker.value = [...picker.options].find((option) => option.text.trim() === text)!.value;
      picker.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };
    return {
      element,
      field,
      select,
      pick,
      choose: (name: string) => pick('distance', name),
      press(text: string) {
        [...element.querySelectorAll<HTMLButtonElement>('.pacing button')].find((button) => button.textContent!.trim() === text)!.click();
        fixture.detectChanges();
      },
      /** The Pacing Plan's rows, a stretch's or an opened Split's, as each cell's text. */
      rows: () => [...element.querySelectorAll('.pacing tbody tr')].map((row) => [...row.querySelectorAll('td')].map(text)),
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

    const select = element.querySelector<HTMLSelectElement>('select[name=distance]')!;
    expect([...select.options].map((option) => option.text)).toEqual(['5K', '10K', 'Half marathon', 'Marathon']);
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
    page.type('pace', '8:00');
    expect(page.field('time').value).toBe('49:43');

    page.type('time', '43:30');
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

  describe('Pacing Plan', () => {
    it('shows the Checkpoints without times until there is a finish time', async () => {
      const page = await render();

      expect(page.rows()).toEqual([
        ['5K', '–', '–', '–'],
        ['Finish', '–', '–', '–'],
      ]);
      expect(page.element.querySelector('.pacing .card-sub')!.textContent).toContain('Type a pace or a finish time');

      page.choose('Marathon');
      expect(page.rows().length).toBe(10);
    });

    it('keeps the share’s place hidden for an even Pace, so choosing a strategy moves nothing', async () => {
      const page = await render();

      expect(page.select('share')!.style.visibility).toBe('hidden');
      page.press('Negative Split');
      expect(page.select('share')!.style.visibility).toBe('');
      expect(page.select('share')!.selectedOptions[0].text.trim()).toBe('5% faster');
    });

    it('lists the stretches between the Checkpoints, with the clock time at each', async () => {
      const page = await render();

      page.type('pace', '5:00');

      expect(page.rows()).toEqual([
        ['5K', '25:00', '5:00 /km', '25:00'],
        ['Finish', '50:00', '5:00 /km', '25:00'],
      ]);
      expect(page.element.querySelector('.pacing .card-sub')!.textContent).toContain('Every km at 5:00 /km.');
    });

    it('fits a marathon in 10 stretches, half-way among them', async () => {
      const page = await render();
      page.choose('Marathon');

      page.type('time', '5:00:00');

      const rows = page.rows();
      expect(rows.map((row) => row[0])).toEqual(['5K', '10K', '15K', '20K', 'Half', '25K', '30K', '35K', '40K', 'Finish']);
      expect(rows[4]).toEqual(['Half', '2:30:00', '7:07 /km', '7:48']);
      expect(rows[5]).toEqual(['25K', '2:57:45', '7:07 /km', '27:45']);
      expect(rows[9]).toEqual(['Finish', '5:00:00', '7:07 /km', '15:36']);
    });

    it('opens a stretch to its km, the one half-way falls within going with the stretch after it', async () => {
      const page = await render();
      page.choose('Marathon');
      page.type('time', '5:00:00');

      page.press('25K');

      const rows = page.rows();
      expect(rows.slice(5, 11).map((row) => row[0])).toEqual(['25K', 'km 22', 'km 23', 'km 24', 'km 25', '30K']);
      expect(rows[6]).toEqual(['km 22', '2:36:25', '7:07 /km', '7:07']);
      expect(page.element.querySelector('.pacing tbody .opener[aria-expanded=true]')!.textContent!.trim()).toBe('25K');

      page.press('25K');
      expect(page.rows().length).toBe(10);
    });

    it('shows every km, and the Checkpoints only again', async () => {
      const page = await render();
      page.choose('Marathon');
      page.type('time', '5:00:00');

      page.press('Show every km');

      expect(page.rows().length).toBe(10 + 43);
      expect(page.rows().at(-1)).toEqual(['km 43', '5:00:00', '7:07 /km', '1:23']);

      page.press('Show Checkpoints only');
      expect(page.rows().length).toBe(10);
    });

    it('speeds up for a Negative Split, by the share chosen', async () => {
      const page = await render();
      page.type('pace', '5:00');

      page.press('Negative Split');
      expect(page.element.querySelector('.pacing .card-sub')!.textContent!.replace(/\s+/g, ' ')).toContain(
        'From 5:15 /km at the start to 4:45 /km at the finish: the second half 5% faster than the first.',
      );
      page.pick('share', '4% faster');

      expect(page.rows()).toEqual([
        ['5K', '25:31', '5:06 /km', '25:31'],
        ['Finish', '50:00', '4:54 /km', '24:29'],
      ]);
      page.press('Show every km');
      expect(page.rows()[1]).toEqual(['km 1', '5:11', '5:11 /km', '5:11']);
      expect(page.rows().at(-1)![2]).toBe('4:49 /km');
    });

    it('slows down for a Positive Split', async () => {
      const page = await render();
      page.type('pace', '5:00');

      page.press('Positive Split');

      expect(page.rows()[0][2]).toBe('4:53 /km');
      expect(page.select('share')!.selectedOptions[0].text.trim()).toBe('5% slower');
    });

    it('opens to miles when distances are in miles, the Checkpoints staying in km', async () => {
      const page = await render('mi');

      page.type('pace', '8:00');

      expect(page.rows()).toEqual([
        ['5K', '24:51', '8:00 /mi', '24:51'],
        ['Finish', '49:43', '8:00 /mi', '24:51'],
      ]);
      page.press('Show every mi');
      expect(page.rows().map((row) => row[0])).toEqual(['5K', 'mi 1', 'mi 2', 'mi 3', 'Finish', 'mi 4', 'mi 5', 'mi 6', 'mi 7']);
      expect(page.rows()[1]).toEqual(['mi 1', '8:00', '8:00 /mi', '8:00']);
    });
  });
});
