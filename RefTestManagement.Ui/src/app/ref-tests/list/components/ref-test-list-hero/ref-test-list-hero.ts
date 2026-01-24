import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Hero section component for the ref test list page.
 * Displays the page title and subtitle.
 */
@Component({
  selector: 'app-ref-test-list-hero',
  imports: [TranslatePipe],
  templateUrl: './ref-test-list-hero.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestListHero {}
