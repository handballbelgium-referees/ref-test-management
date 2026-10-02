import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-ref-test-withdrawal-queued',
  imports: [TranslatePipe],
  templateUrl: './ref-test-withdrawal-queued.html',
  host: {
    class: 'block',
  },
})
export class RefTestWithdrawalQueued {}
