import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-ref-test-instructions',
  imports: [TranslatePipe],
  templateUrl: './ref-test-instructions.html',
  host: {
    class: 'block',
  },
})
export class RefTestInstructions {
  readonly hasTimeLimit = input.required<boolean>();
}
