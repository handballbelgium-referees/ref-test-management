import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  ActivatedRoute,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, map, of, switchMap } from 'rxjs';
import { GetRefTestByIdGQL, RefTestStatus } from '../../../../graphql/generated';
import { RefTestDetailDataService } from './services/ref-test-detail-data.service';

@Component({
  selector: 'app-ref-test-detail',
  imports: [TranslatePipe, RouterLink, RouterLinkActive, RouterOutlet],
  providers: [RefTestDetailDataService],
  templateUrl: './ref-test-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly getRefTestByIdGQL = inject(GetRefTestByIdGQL);
  private readonly dataService = inject(RefTestDetailDataService);

  protected readonly RefTestStatus = RefTestStatus;

  protected readonly refTestData = toSignal(
    this.route.paramMap.pipe(
      switchMap((params) => {
        const id = params.get('id');
        if (!id) {
          this.router.navigate(['/ref-tests']);
          return of(null);
        }

        return this.getRefTestByIdGQL.fetch({ variables: { id } }).pipe(
          map((result) => {
            if (!result.data?.refTest) {
              this.router.navigate(['/ref-tests']);
              return null;
            }
            return result.data.refTest;
          }),
          catchError((error) => {
            console.error('Error loading ref test:', error);
            this.router.navigate(['/ref-tests']);
            return of(null);
          })
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    )
  );

  constructor() {
    // Update the data service whenever refTestData changes
    effect(() => {
      const data = this.refTestData();
      if (data) {
        this.dataService.setRefTest(data);
      }
    });
  }

  protected readonly loading = computed(() => !this.refTestData());

  protected getStatusClass(status: RefTestStatus): string {
    switch (status) {
      case RefTestStatus.Completed:
        return 'bg-success-100 text-success-800';
      case RefTestStatus.InProgress:
        return 'bg-blue-100 text-blue-800';
      case RefTestStatus.Expired:
        return 'bg-red-100 text-red-800';
      case RefTestStatus.Pending:
      default:
        return 'bg-yellow-100 text-yellow-800';
    }
  }

  protected navigateBack(): void {
    this.router.navigate(['/ref-tests']);
  }
}
