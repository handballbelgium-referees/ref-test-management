import { Pipe, type PipeTransform } from '@angular/core';

@Pipe({
  name: 'appTranslation',
})
export class TranslationPipe implements PipeTransform {
  transform(phrase: Record<string, string>, language: string): string {
    return phrase[language] ?? '';
  }
}
