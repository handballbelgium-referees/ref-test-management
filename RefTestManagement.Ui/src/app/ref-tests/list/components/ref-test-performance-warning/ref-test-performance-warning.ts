import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Warning banner displayed when there are too many results that may impact performance.
 * Suggests using filters to narrow down results.
 */
@Component({
  selector: 'app-ref-test-performance-warning',
  imports: [TranslatePipe],
  templateUrl: './ref-test-performance-warning.html',
  host: {
    class: 'block',
  },
})
export class RefTestPerformanceWarning {}
