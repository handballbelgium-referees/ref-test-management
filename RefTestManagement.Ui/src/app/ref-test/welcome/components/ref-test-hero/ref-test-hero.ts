import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-ref-test-hero',
  imports: [TranslatePipe],
  templateUrl: './ref-test-hero.html',
  host: {
    class: 'block',
  },
})
export class RefTestHero {}
