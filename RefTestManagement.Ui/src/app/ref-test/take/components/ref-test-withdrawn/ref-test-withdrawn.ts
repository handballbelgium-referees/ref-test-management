import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-ref-test-withdrawn',
  imports: [TranslatePipe],
  templateUrl: './ref-test-withdrawn.html',
  host: {
    class: 'block',
  },
})
export class RefTestWithdrawn {}
