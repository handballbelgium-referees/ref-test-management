import { NgClass } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-audit-log-event-badge',
  imports: [NgClass, TranslatePipe],
  template: `
    <span
      class="inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap"
      [ngClass]="badgeClass()"
    >
      @if (entityType()) {
        <span class="font-normal opacity-70">{{
          'audit-logs.entityType.' + entityType() | translate
        }}</span
        ><span class="opacity-40">·</span>
      }
      {{ 'audit-logs.eventType.' + type() | translate }}
    </span>
  `,
})
export class AuditLogEventBadge {
  readonly type = input.required<string>();
  readonly entityType = input<string | null>(null);

  protected readonly badgeClass = computed(() => {
    switch (this.type()) {
      case 'RefTestCreated':
      case 'RefTestApproved':
      case 'RefTestRevived':
      case 'RefTestTitleCreated':
      case 'EntityCreated':
        return 'text-green-700 bg-green-100';
      case 'RefTestDeleted':
      case 'RefTestRejected':
      case 'RefTestExpired':
      case 'EntityDeleted':
        return 'text-red-700 bg-red-100';
      case 'RefTestSoftReset':
      case 'RefTestHardReset':
      case 'RefTestTokenRegenerated':
      case 'RefTestInvitationSent':
      case 'RefTestResultsSent':
        return 'text-yellow-700 bg-yellow-100';
      default:
        return 'text-blue-700 bg-blue-100';
    }
  });
}
