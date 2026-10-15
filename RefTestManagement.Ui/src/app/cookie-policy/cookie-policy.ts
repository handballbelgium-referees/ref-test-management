import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-cookie-policy',
  imports: [TranslatePipe],
  templateUrl: './cookie-policy.html',
  host: {
    class: 'block',
  },
})
export class CookiePolicy {}
