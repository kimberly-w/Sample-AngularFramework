import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'phone'
})
export class PhonePipe implements PipeTransform {
  transform(phoneNumber: number | string): any {
    const value = phoneNumber.toString().trim().replace(/^\+/, '');
    if (!value.match(/[0-9]/)) { return phoneNumber; }
    let city = null; let pNumber = null;
    // (123)456-7890
    switch (value.length) {
      case 10: city = value.slice(0, 3);
               pNumber = value.slice(3);
               break;
      default:
               city = value.slice(0, 3);
               pNumber = value.slice(3);
    }
    return '(' + city  + ')' + (pNumber.slice(0, 3) + '-' + pNumber.slice(3)).trim();
  }

}
