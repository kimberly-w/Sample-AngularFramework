import { Component, OnInit, OnDestroy, Input, Output, ViewChild, AfterViewChecked, Injector,
  InjectionToken, EventEmitter, NgZone, ElementRef } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';

import { Observable, Observer, Subject, of, from } from 'rxjs';
import { reduce, filter, mergeMap, pluck, map } from 'rxjs/operators';

import { PhonePipe } from 'src/app/pipes/phone.pipe';
import { OrderByPipe } from 'src/app/pipes/order-by.pipe';
import { ApiService } from 'src/app/services/api.service';
import { Employee } from 'src/app/models/employee';
import { Product } from 'src/app/models/product';
import { SortByColumnPipe } from 'src/app/pipes/sort-by-column.pipe';

const groupByKey = (list: any[], keyGetter: any ): any[] => {
      return list.reduce((acc, curr, index) => {
        (acc[curr[keyGetter]] = acc[curr[keyGetter]] || []).push(curr);
        return acc;
      });
};

const getKey = (list: any[], key: string): any => {
      console.log(list.slice(0, 3));
      return list.map( item => item.id === key);
}

const sumCategory = (array: any) => {
//  array.pipe(filter(val => { return val > 3; }), map((val: number, i: any) => { return val * 2; }))
//      .subscribe(val => { console.log(val)});
}

const sum = (list: any[], key: any): any[] => {
return list.map(item => item.salary)
          .reduce((acc, curr) => acc + curr , 0);
};

// map( products => products.sort(aObject: any, bObject: any) { this.compareFn(aObject, bObject)})
const compareFn = (aObject, bObject): any => {
      return aObject.name - bObject.name || bObject.designation - aObject.designation;
}

@Component({
  selector: 'app-list',
  templateUrl: './list.component.html',
  styleUrls: ['./list.component.css']
})
export class ListComponent implements OnInit {
  @Input() iProductId;
  @Output() oUpdatePrice = new EventEmitter();
  selectedRow = true;
  // @ViewChild('btn', { static: true }) button: ElementRef;
  @Input() employees: Employee[]
   = [
    // tslint:disable-next-line: max-line-length
    {id: 901, name: 'Kathy', email: '@gmail.com', phoneNumber: 8482131499 , designation: 'IT', status: true, hiredDate: '01/01/2020', salary: 95.00 },
      // tslint:disable-next-line: max-line-length
      {id: 765, name : 'David', email: 'gmail.com', phoneNumber: 9091234567 , designation: 'IT', status: true, hiredDate: '01/01/2020', salary: 10.00 },
      // tslint:disable-next-line: max-line-length
      {id: 564, name : 'Ethen', email: '@gmail.com', phoneNumber: 2125479910, designation: 'IT', status: true, hiredDate: '7/31/2017', salary: 90.00 },
      // tslint:disable-next-line: max-line-length
      {id: 834, name : 'Frank', email: '@gmail.com', phoneNumber: 64621390990 , designation: 'Bio', status: true, hiredDate: '8/15/2010', salary: 15.25},
      // tslint:disable-next-line: max-line-length
      {id: 1003, name : 'Roy', email: '@gmail.com', phoneNumber: 3036752344, designation: 'PM', status: false, hiredDate: '01/01/2020', salary: 23.45 },
// tslint:disable-next-line: max-line-length
      {id: 7764, name: 'Joyce', email: '@gmail.com', phoneNumber: 2129085512 , designation: 'Finance', status: true, hiredDate: '01/01/2010', salary: 150.20 },
// tslint:disable-next-line: max-line-length
      {id: 4566, name: 'Amy', email: '@gmail.com', phoneNumber: 2123470922, designation: 'Finance', status: false, hiredDate: '01/01/2010', salary: 710.23 },
// tslint:disable-next-line: max-line-length
      {id: 4610, name: 'Amy', email: '@gmail.com', phoneNumber: 6464684080, designation: 'Account', status: false, hiredDate: '01/01/2010', salary: 650.50 },
// tslint:disable-next-line: max-line-length
      {id: 1158, name: 'Lori', email: '@gmail.com', phoneNumber: 2129076610, designation: 'HR', status: null, hiredDate: '09/01/2015', salary: 12.56 }
  ];
constructor(private apiService: ApiService, public ngZone: NgZone, public injector: Injector) {

}

ngOnInit(): void {
    // Loads all employees from JSON server
    // this.apiService.getAllEmployees();
}

selectDetail() {
      this.selectedRow = !this.selectedRow;
}

getEmployee() {}

deleteEmployee(employee, rowId) {
  this.apiService.deleteEmployee(employee).subscribe( response => {});
}

}
