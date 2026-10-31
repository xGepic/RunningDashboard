import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatDistance, formatDuration, formatHoursMinutes, formatSwimPace } from '../shared/format';
import { HistoryList, requestParams } from '../shared/history-list';
import { SportSwitch } from '../shared/sport-switch';
import { Swim, SwimPage, SwimSortKey } from './swim';
import { swimTable } from './swim-export';

/** Swims per page. */
const pageSize = 10;

/** Swim history: like Ride history, all Swims ten a page, with Swim Pace per 100 m and distances in metres. */
@Component({
  selector: 'app-swims-page',
  imports: [DatePipe, DecimalPipe, RouterLink, SportSwitch],
  templateUrl: './swims-page.html',
  styleUrl: '../shared/history-list.scss',
})
export class SwimsPage extends HistoryList<SwimSortKey, Swim> {
  /** A page of all Swims, with the totals of all of them. */
  protected readonly swims = httpResource<SwimPage>(() => ({
    url: '/api/swims',
    params: requestParams({ sort: this.sort(), direction: this.direction(), page: this.page(), pageSize: String(pageSize) }),
  }));
  protected readonly list = this.swims;
  /** Swim Pace starts with the fastest. */
  protected readonly ascendingFirst: readonly SwimSortKey[] = ['pace'];
  protected readonly detailPath = '/swims';

  /** Of all Swims, for the page header; none until they are in, or when they could not be loaded. */
  protected readonly totals = computed(() => (this.swims.hasValue() ? this.swims.value().totals : null));

  protected readonly formatDistance = formatDistance;
  protected readonly formatDuration = formatDuration;
  protected readonly formatHoursMinutes = formatHoursMinutes;
  protected readonly formatSwimPace = formatSwimPace;

  /** Every Swim, in the list's sort order. */
  protected exportSwims(): void {
    this.exportCsv<Swim>('/api/swims/export', requestParams({ sort: this.sort(), direction: this.direction() }), this.datedFileName('swims'), swimTable);
  }
}
