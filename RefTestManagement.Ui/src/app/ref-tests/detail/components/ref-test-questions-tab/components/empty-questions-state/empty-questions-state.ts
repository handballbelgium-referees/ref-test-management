import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-empty-questions-state',
  imports: [TranslatePipe],
  templateUrl: './empty-questions-state.html',
  host: { class: 'block' },
})
export class EmptyQuestionsState {}
