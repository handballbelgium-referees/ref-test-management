import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { applyEach, email, form, required } from '@angular/forms/signals';
import { provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import type { IUserData } from '../../create-ref-tests';
import { RefTestUserListItem } from './ref-test-user-list-item';

@Component({
  imports: [RefTestUserListItem],
  template: `
    <app-ref-test-user-list-item
      [userFormControl]="userForm.users[0]"
      [index]="0"
      [showRemove]="false"
    />
  `,
})
class UserListItemHost {
  private readonly _model = signal<{ users: IUserData[] }>({
    users: [{ firstName: '', lastName: '', email: '' }],
  });

  readonly userForm = form(this._model, (schemaPath) => {
    applyEach(schemaPath.users, (user) => {
      required(user.firstName, { message: 'ref_tests.create.form.users.first_name_required' });
      required(user.lastName, { message: 'ref_tests.create.form.users.last_name_required' });
      required(user.email, { message: 'ref_tests.create.form.users.email_required' });
      email(user.email, { message: 'ref_tests.create.form.users.email_invalid' });
    });
  });
}

describe('RefTestUserListItem', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [UserListItemHost],
      providers: [provideTranslateService({ fallbackLang: 'en' })],
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  it('associates each visible validation error and exposes invalid state', () => {
    const fixture = TestBed.createComponent(UserListItemHost);
    fixture.componentInstance.userForm().markAsTouched();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    for (const field of ['firstName', 'lastName', 'email']) {
      const input = root.querySelector(`#${field}-0`) as HTMLInputElement;
      const errorId = `${field}-error-0`;
      const error = root.querySelector(`#${errorId}`);

      expect(input.getAttribute('aria-invalid')).toBe('true');
      expect(input.getAttribute('aria-describedby')).toBe(errorId);
      expect(error).not.toBeNull();
    }
  });
});
