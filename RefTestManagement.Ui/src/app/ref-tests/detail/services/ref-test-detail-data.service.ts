import { Injectable, signal } from '@angular/core';
import { GetRefTestByIdQuery } from '../../../../../graphql/generated';

type RefTestData = NonNullable<GetRefTestByIdQuery['refTest']>;

@Injectable()
export class RefTestDetailDataService {
  private readonly _refTest = signal<RefTestData | null>(null);
  readonly refTest = this._refTest.asReadonly();

  setRefTest(refTest: RefTestData): void {
    this._refTest.set(refTest);
  }

  clearRefTest(): void {
    this._refTest.set(null);
  }
}
