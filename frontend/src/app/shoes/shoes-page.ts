import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component } from '@angular/core';
import { formatTotalDistance } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Shoe } from './shoe';

@Component({
  selector: 'app-shoes-page',
  imports: [DatePipe],
  templateUrl: './shoes-page.html',
  styleUrl: './shoes-page.scss',
})
export class ShoesPage {
  protected readonly shoes = httpResource<Shoe[]>(() => '/api/shoes');
  protected readonly formatTotalDistance = formatTotalDistance;

  constructor() {
    reloadOnSync(() => this.shoes.reload());
  }
}
