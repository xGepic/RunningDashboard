/** As returned by GET and POST /api/backup. */
export interface Backup {
  /** The backup folder, as a full path on the PC. */
  folder: string;
  /** When the newest backup was made; null when there is none. */
  lastBackupAt: string | null;
  /** Why the last backup failed; null once one succeeds. */
  lastError: string | null;
}
