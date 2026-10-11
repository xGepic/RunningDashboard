import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { ConnectionStatus } from './connection/connection-status';
import { GoalRace } from './goal-races/goal-race';
import { daysUntil } from './shared/dates';

interface NavItem {
  label: string;
  path: string;
  /** An SVG path in a 24×24 box, stroked. */
  icon: string;
}

const navItems: NavItem[] = [
  { label: 'Overview', path: '/', icon: 'M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z' },
  { label: 'Training', path: '/training', icon: 'M3 12h4l3-7 4 14 3-7h4' },
  { label: 'Runs', path: '/runs', icon: 'M9 6h11M9 12h11M9 18h11M4.5 6h.01M4.5 12h.01M4.5 18h.01' },
  { label: 'Records', path: '/records', icon: 'M8 4h8v5a4 4 0 0 1-8 0zM12 13v4M8 20h8M8 6H5a3 3 0 0 0 3 4M16 6h3a3 3 0 0 1-3 4' },
  { label: 'Shoes', path: '/shoes', icon: 'M3 17v-6l5-3 3 3 5 1.5c3 .8 5 2 5 4.5H3zM3 20h18' },
];

@Component({
  imports: [DatePipe, RouterOutlet, RouterLink, RouterLinkActive, ConnectionStatus],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
  host: { '(document:keydown)': 'onKeydown($event)' },
})
export class App {
  private readonly router = inject(Router);

  protected readonly nav = navItems;

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** "Runs" and, on a page below it, that page's name: the header's breadcrumb. */
  protected readonly breadcrumb = computed(() => {
    const path = this.url().split(/[?#]/)[0];
    const section = [...navItems].reverse().find((item) => item.path === '/' ? path === '/' : path.startsWith(item.path));
    const sub = /^\/runs\/[^/]+$/.test(path) ? 'Run' : /^\/training\/plan\/[^/]+$/.test(path) ? 'Training Plan' : null;
    return { section, sub };
  });

  /** Goal Races change on the Training page, so the "Next up" card reloads them on every navigation. */
  private readonly goalRaces = httpResource<GoalRace[]>(() => {
    this.url();
    return '/api/goal-races';
  });

  /** The nearest Goal Race from today on. */
  protected readonly nextRace = computed(() => {
    if (!this.goalRaces.hasValue()) return null;
    const race = this.goalRaces
      .value()
      .filter((r) => daysUntil(r.date) >= 0)
      .sort((a, b) => a.date.localeCompare(b.date))[0];
    return race ? { race, days: daysUntil(race.date) } : null;
  });

  /** Keys 1 to 5 open the pages, as the sidebar shows, unless the Athlete is typing. */
  protected onKeydown(event: KeyboardEvent): void {
    if (event.ctrlKey || event.metaKey || event.altKey) return;
    const target = event.target as HTMLElement | null;
    if (target?.closest('input, select, textarea, [contenteditable="true"]')) return;
    const item = navItems[Number(event.key) - 1];
    if (item) this.router.navigateByUrl(item.path);
  }
}
