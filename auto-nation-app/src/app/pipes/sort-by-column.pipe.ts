import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'sortByColumn'
})
export class SortByColumnPipe implements PipeTransform {
  transform(data: any[], column = ''): any[] {
    data.sort((a: any, b: any) => {
      if (a[column] < b[column]) {
        return -1;
      } else if (a[column] > b[column]) {
        return 1;
      } else {
        return 0;
      }
    });
    return data;
  }
}
