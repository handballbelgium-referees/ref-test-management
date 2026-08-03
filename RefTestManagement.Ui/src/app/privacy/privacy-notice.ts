import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-privacy-notice',
  imports: [TranslatePipe],
  templateUrl: './privacy-notice.html',
  host: {
    class: 'block',
  },
})
export class PrivacyNotice {}
