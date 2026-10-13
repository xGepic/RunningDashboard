import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { ConnectionStatus } from './connection/connection-status';
import { monthFragments } from './calendar/calendar-page';
import { RunPage } from './runs/run';
import { fullMonthNames } from './shared/dates';
import { formatDuration } from './shared/format';
import { Units } from './shared/units';
import { reloadOnSync } from './sync/reload-on-sync';

interface NavItem {
  label: string;
  path: string;
  /** An SVG path in a 24×24 box, stroked. */
  icon: string;
  /** Other pages in this section, e.g. Ride history under Runs. */
  also?: string[];
}

const navItems: NavItem[] = [
  { label: 'Overview', path: '/', icon: 'M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z' },
  { label: 'Training', path: '/training', icon: 'M3 12h4l3-7 4 14 3-7h4' },
  { label: 'Runs', path: '/runs', icon: 'M9 6h11M9 12h11M9 18h11M4.5 6h.01M4.5 12h.01M4.5 18h.01', also: ['/rides', '/swims'] },
  { label: 'Calendar', path: '/calendar', icon: 'M4 6h16v14H4zM4 10h16M8 3v4M16 3v4' },
  { label: 'Records', path: '/records', icon: 'M8 4h8v5a4 4 0 0 1-8 0zM12 13v4M8 20h8M8 6H5a3 3 0 0 0 3 4M16 6h3a3 3 0 0 1-3 4' },
  { label: 'Shoes', path: '/shoes', icon: 'M3 17v-6l5-3 3 3 5 1.5c3 .8 5 2 5 4.5H3zM3 20h18' },
];

/** At the bottom of the sidebar, apart from the pages, and without a key. */
const settingsItem: NavItem = {
  label: 'Settings',
  path: '/settings',
  icon: 'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z',
};

@Component({
  imports: [DatePipe, RouterOutlet, RouterLink, ConnectionStatus],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
  host: { '(document:keydown)': 'onKeydown($event)' },
})
export class App {
  private readonly router = inject(Router);
  /** Loads the Distance Unit as the app starts, and shows the last Run in it. */
  private readonly units = inject(Units);

  protected readonly nav = navItems;
  protected readonly settings = settingsItem;
  /** On narrow screens the tabs replace the sidebar, so Settings joins them at the end. */
  protected readonly tabs = [...navItems, settingsItem];

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /**
   * "Runs" and, on a page below it, that page's name: the header's breadcrumb. A month of the Calendar
   * (/calendar/2025#Dec) is below its year (/calendar/2025); Ride and Swim history (/rides, /swims) are below the Runs. The section
   * is also the page highlighted in the navigation.
   */
  protected readonly breadcrumb = computed(() => {
    const [path] = this.url().split(/[?#]/);
    const fragment = this.url().split('#')[1] ?? '';
    const section = [...this.tabs].reverse().find((item) =>
      item.path === '/' ? path === '/' : [item.path, ...(item.also ?? [])].some((sectionPath) => path.startsWith(sectionPath)));
    const month = monthFragments.indexOf(fragment);
    if (/^\/calendar(\/\d{4})?$/.test(path) && month >= 0) {
      const year = path.split('/')[2] ?? String(new Date().getFullYear());
      return { section, sub: `${fullMonthNames[month]} ${year}`, parent: path, dropParams: {} };
    }
    const sub = /^\/runs\/[^/]+$/.test(path)
      ? 'Run'
      : path === '/rides'
        ? 'Rides'
        : path === '/swims'
          ? 'Swims'
          : /^\/training\/plan\/[^/]+$/.test(path)
            ? 'Training Plan'
            : null;
    // Back up keeps the filters, but the Rides' and Swims' sort order and page mean nothing to the Runs (as with the switch).
    const dropParams = path === '/rides' || path === '/swims' ? { sort: null, direction: null, page: null } : {};
    return { section, sub, parent: section?.path, dropParams };
  });

  /** The newest Run: the first of the Runs list, which is newest first by default. */
  private readonly latestRuns = httpResource<RunPage>(() => '/api/runs?pageSize=1');

  /** None until there are Runs, or when they could not be loaded. */
  protected readonly lastRun = computed(() => {
    if (!this.latestRuns.hasValue()) return null;
    const run = this.latestRuns.value().items[0];
    return run ? { ...run, figures: `${this.units.distance(run.distance)} · ${formatDuration(run.movingTime)}` } : null;
  });

  constructor() {
    reloadOnSync(() => this.latestRuns.reload());
  }

  /** Keys 1 to 6 open the pages, as the sidebar shows, unless the Athlete is typing. */
  protected onKeydown(event: KeyboardEvent): void {
    if (event.ctrlKey || event.metaKey || event.altKey) return;
    const target = event.target as HTMLElement | null;
    if (target?.closest('input, select, textarea, [contenteditable="true"]')) return;
    const item = navItems[Number(event.key) - 1];
    if (item) this.router.navigateByUrl(item.path);
  }
}
