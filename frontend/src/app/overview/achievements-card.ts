import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Achievement, achievementProgress, closestAchievement, latestAchievement } from '../achievements/achievement';
import { AchievementMedal } from '../achievements/achievement-medal';
import { Units } from '../shared/units';
import { reloadOnSync } from '../sync/reload-on-sync';

/** The latest Achievement, "New" for a week, and the locked one closest to being earned. */
@Component({
  selector: 'app-achievements-card',
  imports: [AchievementMedal, DatePipe, RouterLink],
  templateUrl: './achievements-card.html',
  styleUrl: './achievements-card.scss',
})
export class AchievementsCard {
  protected readonly achievements = httpResource<Achievement[]>(() => '/api/achievements');
  private readonly units = inject(Units);

  protected readonly latest = computed(() => latestAchievement(this.achievements.value() ?? []));

  protected readonly next = computed(() => {
    const achievement = closestAchievement(this.achievements.value() ?? []);
    if (!achievement) return null;
    return { achievement, progress: achievementProgress(achievement, this.units)! };
  });

  constructor() {
    reloadOnSync(() => this.achievements.reload());
  }
}
