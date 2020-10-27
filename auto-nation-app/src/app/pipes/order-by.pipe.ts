import { Pipe, PipeTransform } from '@angular/core';
import * as _ from 'lodash';
/*
* _.sortBy(data)
_.orderBy(data,[],[]);
* | orderBy: 'asc': 'propertyName'
*/
@Pipe({
  name: 'orderBy'
})
export class OrderByPipe implements PipeTransform {
  transform(data: any[], order = '', column = ''): any[] {
    if (!data || !order || order === '') { return data; }

    if (!column || column === '') { return _.sortBy(data); }

    return _.orderBy(data, [column], [order]);
  }
}
