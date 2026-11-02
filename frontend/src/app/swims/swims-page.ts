import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatDistance, formatDuration, formatHoursMinutes, formatSwimPace } from '../shared/format';
import { YearlyHistoryList, requestParams } from '../shared/history-list';
import { SportSwitch } from '../shared/sport-switch';
import { Swim, SwimPage, SwimSortKey } from './swim';
import { swimTable } from './swim-export';

/** Swims per page, as many as the Runs': a year's nearly always fit on one; the pager only shows up past this. */
const pageSize = 200;

/** Swim history: like Ride history, a year's Swims at a time, 200 a page, with Swim Pace per 100 m and distances in metres. */
@Component({
  selector: 'app-swims-page',
  imports: [DatePipe, DecimalPipe, RouterLink, SportSwitch],
  templateUrl: './swims-page.html',
  styleUrl: '../shared/history-list.scss',
})
export class SwimsPage extends YearlyHistoryList<SwimSortKey, Swim> {
  /** A page of the year's Swims, with the totals of all of them. */
  protected readonly swims = httpResource<SwimPage>(() => ({
    url: '/api/swims',
    params: requestParams({
      ...this.yearParams(),
      sort: this.sort(),
      direction: this.direction(),
      page: this.page(),
      pageSize: String(pageSize),
    }),
  }));
  protected readonly list = this.swims;
  /** Swim Pace starts with the fastest. */
  protected readonly ascendingFirst: readonly SwimSortKey[] = ['pace'];
  protected readonly detailPath = '/swims';

  /** Of the year's Swims, for the page header; none until they are in, or when they could not be loaded. */
  protected readonly totals = computed(() => (this.swims.hasValue() ? this.swims.value().totals : null));

  protected readonly formatDistance = formatDistance;
  protected readonly formatDuration = formatDuration;
  protected readonly formatHoursMinutes = formatHoursMinutes;
  protected readonly formatSwimPace = formatSwimPace;

  /** The year's Swims, or those of every year, in the list's sort order. */
  protected exportSwims(scope: 'year' | 'all'): void {
    const params = requestParams({ ...(scope === 'year' ? this.yearParams() : {}), sort: this.sort(), direction: this.direction() });
    this.exportCsv<Swim>('/api/swims/export', params, this.exportFileName('swims', scope), swimTable);
  }
}
