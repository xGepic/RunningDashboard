import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Units } from '../shared/units';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Achievement, achievementGroups, achievementProgress, lastEarned, latestAchievement } from './achievement';
import { AchievementMedal } from './achievement-medal';

/** Every Achievement by group: the earned ones with when and by which Run, the locked ones with how far there is to go. */
@Component({
  selector: 'app-achievements-page',
  imports: [AchievementMedal, DatePipe, DecimalPipe, RouterLink],
  templateUrl: './achievements-page.html',
  styleUrl: './achievements-page.scss',
})
export class AchievementsPage {
  protected readonly achievements = httpResource<Achievement[]>(() => '/api/achievements');
  /** Progress in the Distance Unit; the Achievements' names stay in km. */
  protected readonly units = inject(Units);

  protected readonly groups = computed(() => {
    const achievements = this.achievements.value() ?? [];
    return achievementGroups
      .map((group) => {
        const inGroup = achievements.filter((a) => a.group === group.group);
        return {
          ...group,
          earned: inGroup.filter((a) => a.earned.length > 0).length,
          achievements: inGroup.map((achievement) => ({
            achievement,
            last: lastEarned(achievement),
            progress: achievementProgress(achievement, this.units),
          })),
        };
      })
      .filter((group) => group.achievements.length > 0);
  });

  protected readonly summary = computed(() => {
    const achievements = this.achievements.value() ?? [];
    return {
      earned: achievements.filter((a) => a.earned.length > 0).length,
      total: achievements.length,
      latest: latestAchievement(achievements),
    };
  });

  constructor() {
    reloadOnSync(() => this.achievements.reload());
  }
}
